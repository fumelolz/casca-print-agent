using System.Globalization;
using System.Text;
using Casca.PrintAgent.Contracts;

namespace Casca.PrintAgent.Services;

public static class ProductLabelZplBuilder
{
    private const int DotsPerMm = 8;
    private const int Margin = 16;
    private const int MaxCopies = 100;

    public static string Build(PrintProductLabelRequest label)
    {
        var width = Math.Clamp(label.WidthMm, 20, 110) * DotsPerMm;
        var height = Math.Clamp(label.HeightMm, 15, 200) * DotsPerMm;
        var copies = Math.Clamp(label.Copies, 1, MaxCopies);
        var contentWidth = width - Margin * 2;
        var usesBarcode = !string.IsNullOrWhiteSpace(label.Barcode);
        var code = usesBarcode ? label.Barcode! : label.Sku;
        var readableCode = usesBarcode ? code : $"SKU {code}";
        var price = "$" + label.Price.ToString("N2", CultureInfo.InvariantCulture);

        var zpl = new StringBuilder();
        zpl.Append("^XA^CI28^MNY^MTD");
        zpl.Append($"^PW{width}^LL{height}^LH0,0");
        zpl.Append($"^FO{Margin},8^A0N,26,26^FB{contentWidth},2,0,L^FD{Sanitize(label.ProductName)}^FS");
        zpl.Append($"^FO{Margin},68^A0N,40,40^FD{price}^FS");
        zpl.Append($"^FO{Margin},112^BY2^BCN,44,N,N,N^FD{Sanitize(code)}^FS");
        zpl.Append($"^FO{Margin},162^A0N,22,22^FD{Sanitize(readableCode)}^FS");
        zpl.Append($"^PQ{copies}^XZ");

        return zpl.ToString();
    }

    private static string Sanitize(string value) =>
        value.Replace("^", " ").Replace("~", " ").Replace("\r", " ").Replace("\n", " ").Trim();
}
