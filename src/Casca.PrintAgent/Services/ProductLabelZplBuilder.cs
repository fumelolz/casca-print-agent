using System.Globalization;
using System.Text;
using Casca.PrintAgent.Contracts;

namespace Casca.PrintAgent.Services;

public static class ProductLabelZplBuilder
{
    private const int DotsPerMm = 8;
    private const int Margin = 16;
    private const int VerticalMargin = 8;
    private const int MaxCopies = 100;
    private const int NameCharsPerLine = 24;
    private const int NameFont = 30;
    private const int SkuFont = 24;
    private const int PriceFont = 34;
    private const int CodeFont = 22;
    private const int MinBarcodeHeight = 28;
    private const int MaxBarcodeHeight = 70;
    private const int BarcodeGap = 4;

    public static string Build(PrintProductLabelRequest label)
    {
        var width = Math.Clamp(label.WidthMm, 20, 110) * DotsPerMm;
        var height = Math.Clamp(label.HeightMm, 15, 200) * DotsPerMm;
        var copies = Math.Clamp(label.Copies, 1, MaxCopies);
        var contentWidth = width - Margin * 2;
        var usesSkuAsBarcode = string.IsNullOrWhiteSpace(label.Barcode);
        var barcode = label.ShowBarcode
            ? BuildBarcode(usesSkuAsBarcode ? label.Sku : label.Barcode)
            : null;
        var skuAlreadyPrinted = barcode is not null && usesSkuAsBarcode;

        var zpl = new StringBuilder();
        zpl.Append("^XA^CI28^MNY^MTD");
        zpl.Append($"^PW{width}^LL{height}^LH0,0");

        var y = VerticalMargin;

        if (label.ShowName && !string.IsNullOrWhiteSpace(label.ProductName))
        {
            var name = Sanitize(label.ProductName);
            var lines = name.Length > NameCharsPerLine ? 2 : 1;
            zpl.Append($"^FO{Margin},{y}^A0N,{NameFont},{NameFont}^FB{contentWidth},{lines},0,C^FD{name}^FS");
            y += lines * (NameFont + 4);
        }

        if (label.ShowSku && !skuAlreadyPrinted && !string.IsNullOrWhiteSpace(label.Sku))
        {
            zpl.Append($"^FO{Margin},{y}^A0N,{SkuFont},{SkuFont}^FB{contentWidth},1,0,C^FD{Sanitize(label.Sku)}^FS");
            y += SkuFont + 4;
        }

        if (label.ShowPrice)
        {
            var price = "$" + label.Price.ToString("N2", CultureInfo.InvariantCulture);
            zpl.Append($"^FO{Margin},{y}^A0N,{PriceFont},{PriceFont}^FB{contentWidth},1,0,C^FD{price}^FS");
            y += PriceFont + 4;
        }

        if (barcode is not null)
        {
            var showReadableCode = !usesSkuAsBarcode || label.ShowSku;
            var readableCodeSpace = showReadableCode ? CodeFont + BarcodeGap : 0;
            var available = height - VerticalMargin - y - readableCodeSpace - BarcodeGap;
            var barcodeHeight = Math.Clamp(available, MinBarcodeHeight, MaxBarcodeHeight);
            var moduleWidth = barcode.Modules * 2 <= contentWidth ? 2 : 1;
            var x = Margin + Math.Max(0, (contentWidth - barcode.Modules * moduleWidth) / 2);

            zpl.Append($"^FO{x},{y}^BY{moduleWidth}^BCN,{barcodeHeight},N,N,N,N^FD{barcode.Data}^FS");
            if (showReadableCode)
            {
                y += barcodeHeight + BarcodeGap;
                zpl.Append($"^FO{Margin},{y}^A0N,{CodeFont},{CodeFont}^FB{contentWidth},1,0,C^FD{barcode.Text}^FS");
            }
        }

        zpl.Append($"^PQ{copies}^XZ");

        return zpl.ToString();
    }

    private static BarcodeLayout? BuildBarcode(string? value)
    {
        var text = new string((value ?? string.Empty)
            .Where(character => character is >= ' ' and <= '~' && character is not '^' and not '~')
            .ToArray()).Trim();

        if (text.Length == 0)
            return null;

        var onlyDigits = text.All(char.IsAsciiDigit);
        string data;
        int symbols;

        if (onlyDigits && text.Length % 2 == 0)
        {
            data = ">;" + text;
            symbols = text.Length / 2;
        }
        else if (onlyDigits && text.Length >= 3)
        {
            data = ">;" + text[..^1] + ">6" + text[^1];
            symbols = (text.Length - 1) / 2 + 2;
        }
        else
        {
            data = ">:" + text.Replace(">", ">0");
            symbols = text.Length;
        }

        return new BarcodeLayout(data, text, 11 * (symbols + 2) + 13);
    }

    private static string Sanitize(string value) =>
        value.Replace("^", " ").Replace("~", " ").Replace("\r", " ").Replace("\n", " ").Trim();

    private sealed record BarcodeLayout(string Data, string Text, int Modules);
}
