using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Globalization;
using Casca.PrintAgent.Contracts;

namespace Casca.PrintAgent.Services;

/// <summary>
/// Dibuja el ticket de venta (papel térmico de 80 mm) sobre un <see cref="Graphics"/>.
/// Todas las columnas se calculan midiendo el texto, no con relleno de espacios, para que nada
/// se salga del papel sin importar la fuente ni el monto.
/// </summary>
public static class SaleTicketRenderer
{
    /// <summary>Ancho del papel en centésimas de pulgada (80 mm ≈ 283).</summary>
    public const float PaperWidth = 280f;

    private const float Margin = 10f;
    private const float ContentWidth = PaperWidth - Margin * 2;

    public static void Render(Graphics graphics, PrintSaleTicketRequest ticket)
    {
        graphics.TextRenderingHint = TextRenderingHint.SingleBitPerPixelGridFit;
        graphics.SmoothingMode = SmoothingMode.None;
        graphics.InterpolationMode = InterpolationMode.NearestNeighbor;
        graphics.PixelOffsetMode = PixelOffsetMode.None;

        using var canvas = new TicketCanvas(graphics);

        // ── LOGO ─────────────────────────────────────────────────────────────
        canvas.Logo();
        canvas.Gap(6f);

        // ── ENCABEZADO DE LA TIENDA ──────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(ticket.TenantName))
            canvas.WrappedCentered(ticket.TenantName, canvas.Tenant);

        canvas.Centered(ticket.StoreName, canvas.Title);

        if (!string.IsNullOrWhiteSpace(ticket.StoreAddress))
            canvas.WrappedCentered(ticket.StoreAddress, canvas.Small);

        if (!string.IsNullOrWhiteSpace(ticket.StorePhone))
            canvas.Centered($"Tel: {ticket.StorePhone}", canvas.Small);

        if (!string.IsNullOrWhiteSpace(ticket.StoreRfc))
            canvas.Centered($"RFC: {ticket.StoreRfc}", canvas.Small);

        canvas.Separator();

        // ── DATOS DE LA VENTA ────────────────────────────────────────────────
        var saleDate = ticket.CreatedOnUtc.ToLocalTime();
        canvas.Row("Folio:", ticket.SaleNumber, canvas.Normal);
        canvas.Row("Fecha:", saleDate.ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture), canvas.Normal);
        canvas.Row("Pago:", ticket.PaymentMethodLabel, canvas.Normal);

        if (!string.IsNullOrWhiteSpace(ticket.CustomerName))
            canvas.Row("Cliente:", ticket.CustomerName, canvas.Normal);

        canvas.Separator();

        // ── PARTIDAS ─────────────────────────────────────────────────────────
        foreach (var item in ticket.Items)
        {
            canvas.Wrapped(item.ProductName, canvas.Normal);
            canvas.Row(
                $"{item.Quantity.ToString("0.##", CultureInfo.InvariantCulture)} x {Money(item.UnitPrice)}",
                Money(item.Total),
                canvas.Normal);
            canvas.Gap(2f);
        }

        canvas.Separator();

        // ── TOTALES ──────────────────────────────────────────────────────────
        canvas.Row("Subtotal:", Money(ticket.Subtotal), canvas.Normal);

        if (ticket.IvaTotal != 0)
            canvas.Row("IVA:", Money(ticket.IvaTotal), canvas.Normal);

        if (ticket.IepsTotal != 0)
            canvas.Row("IEPS:", Money(ticket.IepsTotal), canvas.Normal);

        if (ticket.RoundingAdjustment != 0)
            canvas.Row("Redondeo:", Money(ticket.RoundingAdjustment), canvas.Normal);

        canvas.Gap(2f);
        canvas.Row("TOTAL:", Money(ticket.Total), canvas.Bold);

        canvas.Separator();

        // ── NOTAS Y PIE ──────────────────────────────────────────────────────
        if (!string.IsNullOrWhiteSpace(ticket.Notes))
        {
            canvas.WrappedCentered(ticket.Notes, canvas.Small);
            canvas.Gap(4f);
        }

        canvas.Centered("¡Gracias por su compra!", canvas.Normal);
        canvas.Gap(4f);

        if (!string.IsNullOrWhiteSpace(ticket.CashierName))
            canvas.WrappedCentered($"Atendió: {ticket.CashierName}", canvas.Small);
        canvas.Gap(30f); // Espacio para que el corte no muerda el último renglón.
    }

    private static string Money(decimal amount) =>
        "$" + amount.ToString("N2", CultureInfo.InvariantCulture);

    private sealed class TicketCanvas : IDisposable
    {
        private readonly Graphics _g;
        private readonly StringFormat _format;
        // Casi al borde: cualquier espacio de más arriba lo agrega el driver o la impresora, no el ticket.
        private float _y = 2f;

        public Font Normal { get; } = new("Consolas", 9f, GraphicsUnit.Point);
        public Font Bold { get; } = new("Consolas", 11f, FontStyle.Bold, GraphicsUnit.Point);
        public Font Title { get; } = new("Consolas", 11f, FontStyle.Bold, GraphicsUnit.Point);
        public Font Tenant { get; } = new("Consolas", 14f, FontStyle.Bold, GraphicsUnit.Point);
        public Font Small { get; } = new("Consolas", 8f, GraphicsUnit.Point);

        public TicketCanvas(Graphics graphics)
        {
            _g = graphics;
            _format = new StringFormat(StringFormat.GenericTypographic)
            {
                FormatFlags = StringFormatFlags.NoWrap | StringFormatFlags.MeasureTrailingSpaces
            };
        }

        public void Gap(float height) => _y += height;

        public void Centered(string text, Font font)
        {
            var x = Margin + (ContentWidth - Width(text, font)) / 2f;
            Draw(text, font, Math.Max(Margin, x));
            _y += font.GetHeight(_g);
        }

        public void Row(string left, string right, Font font)
        {
            var rightWidth = Width(right, font);
            Draw(left, font, Margin);
            Draw(right, font, Margin + ContentWidth - rightWidth);
            _y += font.GetHeight(_g);
        }

        public void Wrapped(string text, Font font)
        {
            foreach (var line in Wrap(text, font))
            {
                Draw(line, font, Margin);
                _y += font.GetHeight(_g);
            }
        }

        public void WrappedCentered(string text, Font font)
        {
            foreach (var line in Wrap(text, font))
                Centered(line, font);
        }

        public void Separator()
        {
            _y += 4f;
            _g.DrawLine(Pens.Black, Margin, _y, Margin + ContentWidth, _y);
            _y += 6f;
        }

        /// <summary>Ícono de la bolsa de Casca Tiendas junto al nombre, centrado en la parte superior.</summary>
        public void Logo()
        {
            const float iconSize = 30f;
            const float gap = 6f;

            using var casca = new Font("Segoe UI", 12f, FontStyle.Bold, GraphicsUnit.Point);
            using var tiendas = new Font("Segoe UI", 12f, FontStyle.Regular, GraphicsUnit.Point);

            var cascaWidth = Width("Casca", casca);
            var tiendasWidth = Width("Tiendas", tiendas);
            var total = iconSize + gap + cascaWidth + tiendasWidth;
            var x = Margin + Math.Max(0f, (ContentWidth - total) / 2f);

            DrawBagIcon(x, _y, iconSize);

            var textY = _y + (iconSize - casca.GetHeight(_g)) / 2f;
            var textX = x + iconSize + gap;
            _g.DrawString("Casca", casca, Brushes.Black, textX, textY, _format);
            _g.DrawString("Tiendas", tiendas, Brushes.Black, textX + cascaWidth, textY, _format);

            _y += iconSize;
        }

        private void DrawBagIcon(float x, float y, float size)
        {
            // Mismo dibujo que el ícono de la app (viewBox 512): cuadrado redondeado con una bolsa.
            var state = _g.Save();
            _g.SmoothingMode = SmoothingMode.AntiAlias;
            _g.TranslateTransform(x, y);
            _g.ScaleTransform(size / 512f, size / 512f);

            using (var background = new GraphicsPath())
            {
                const float d = 224f;
                background.AddArc(0, 0, d, d, 180, 90);
                background.AddArc(512 - d, 0, d, d, 270, 90);
                background.AddArc(512 - d, 512 - d, d, d, 0, 90);
                background.AddArc(0, 512 - d, d, d, 90, 90);
                background.CloseFigure();
                _g.FillPath(Brushes.Black, background);
            }

            using (var body = new GraphicsPath())
            {
                body.AddArc(126, 180, 56, 56, 180, 90);
                body.AddArc(330, 180, 56, 56, 270, 90);
                body.AddArc(266, 256, 120, 120, 0, 90);
                body.AddArc(126, 256, 120, 120, 90, 90);
                body.CloseFigure();
                _g.FillPath(Brushes.White, body);
            }

            using (var handle = new GraphicsPath())
            using (var pen = new Pen(Color.White, 27f) { StartCap = LineCap.Flat, EndCap = LineCap.Flat })
            {
                handle.AddLine(205.5f, 184, 205.5f, 154);
                handle.AddArc(205.5f, 103.5f, 101f, 101f, 180, 180);
                handle.AddLine(306.5f, 154, 306.5f, 184);
                _g.DrawPath(pen, handle);
            }

            _g.FillEllipse(Brushes.Black, 214 - 13, 222 - 13, 26, 26);
            _g.FillEllipse(Brushes.Black, 298 - 13, 222 - 13, 26, 26);

            _g.Restore(state);
        }

        private void Draw(string text, Font font, float x) =>
            _g.DrawString(text, font, Brushes.Black, x, _y, _format);

        private float Width(string text, Font font) =>
            _g.MeasureString(text, font, PointF.Empty, _format).Width;

        private List<string> Wrap(string text, Font font)
        {
            var lines = new List<string>();
            var current = string.Empty;

            foreach (var word in text.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            {
                var candidate = current.Length == 0 ? word : $"{current} {word}";
                if (Width(candidate, font) <= ContentWidth)
                {
                    current = candidate;
                    continue;
                }

                if (current.Length > 0)
                    lines.Add(current);

                // Una sola palabra más ancha que el papel se parte por caracteres.
                current = word;
                while (Width(current, font) > ContentWidth && current.Length > 1)
                {
                    var cut = current.Length - 1;
                    while (cut > 1 && Width(current[..cut], font) > ContentWidth)
                        cut--;
                    lines.Add(current[..cut]);
                    current = current[cut..];
                }
            }

            if (current.Length > 0)
                lines.Add(current);

            return lines.Count > 0 ? lines : [text];
        }

        public void Dispose()
        {
            Normal.Dispose();
            Bold.Dispose();
            Title.Dispose();
            Tenant.Dispose();
            Small.Dispose();
            _format.Dispose();
        }
    }
}
