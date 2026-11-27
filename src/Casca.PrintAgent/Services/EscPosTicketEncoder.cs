using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using Casca.PrintAgent.Contracts;

namespace Casca.PrintAgent.Services;

public static class EscPosTicketEncoder
{
    private const int MaxHeightDots = 12000;
    private const int BandRows = 128;
    private const int BottomMarginRows = 8;
    private const int BlackThreshold = 128;

    public static byte[] Build(PrintSaleTicketRequest ticket)
    {
        var dots = ticket.PaperWidthMm == 58 ? 384 : 576;
        var paperWidth = SaleTicketRenderer.PaperWidthFor(ticket.PaperWidthMm);

        using var bitmap = new Bitmap(dots, MaxHeightDots, PixelFormat.Format24bppRgb);
        bitmap.SetResolution(100f, 100f);

        using (var graphics = Graphics.FromImage(bitmap))
        {
            graphics.Clear(Color.White);
            var scale = dots / paperWidth;
            graphics.ScaleTransform(scale, scale);
            SaleTicketRenderer.Render(graphics, ticket, paperWidth);
        }

        var bytesPerRow = dots / 8;
        var packed = ToMonochrome(bitmap, bytesPerRow, out var rows);

        using var output = new MemoryStream();
        output.Write([0x1B, 0x40]);

        for (var start = 0; start < rows; start += BandRows)
        {
            var bandRows = Math.Min(BandRows, rows - start);
            output.Write([
                0x1D, 0x76, 0x30, 0x00,
                (byte)(bytesPerRow & 0xFF), (byte)(bytesPerRow >> 8),
                (byte)(bandRows & 0xFF), (byte)(bandRows >> 8)
            ]);
            output.Write(packed, start * bytesPerRow, bandRows * bytesPerRow);
        }

        output.Write([0x1B, 0x64, 0x04]);
        output.Write([0x1D, 0x56, 0x42, 0x03]);
        return output.ToArray();
    }

    private static byte[] ToMonochrome(Bitmap bitmap, int bytesPerRow, out int rows)
    {
        var width = bitmap.Width;
        var height = bitmap.Height;
        var data = bitmap.LockBits(new Rectangle(0, 0, width, height), ImageLockMode.ReadOnly, PixelFormat.Format24bppRgb);

        byte[] pixels;
        try
        {
            pixels = new byte[Math.Abs(data.Stride) * height];
            Marshal.Copy(data.Scan0, pixels, 0, pixels.Length);
        }
        finally
        {
            bitmap.UnlockBits(data);
        }

        var stride = Math.Abs(data.Stride);
        var packed = new byte[bytesPerRow * height];
        var lastBlackRow = -1;

        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var index = y * stride + x * 3;
                var luminance = (pixels[index] * 29 + pixels[index + 1] * 150 + pixels[index + 2] * 77) >> 8;
                if (luminance >= BlackThreshold)
                    continue;

                packed[y * bytesPerRow + (x >> 3)] |= (byte)(0x80 >> (x & 7));
                lastBlackRow = y;
            }
        }

        rows = Math.Min(height, lastBlackRow + 1 + BottomMarginRows);
        return packed;
    }
}
