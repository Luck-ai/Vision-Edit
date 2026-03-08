namespace VisionEditCV.Controls
{
    [System.ComponentModel.DesignerCategory("Component")]
    public class ChipLabel : Control
    {
        private static readonly Color DefaultPillBg     = Color.FromArgb(14, 48, 52);
        private static readonly Color DefaultPillBorder = Color.FromArgb(0, 140, 160);
        private static readonly Color DefaultText       = Color.FromArgb(0, 229, 255);

        public int CornerRadius { get; set; } = 10;

        public ChipLabel()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);

            BackColor = DefaultPillBg;
            ForeColor = DefaultText;
            Font      = new Font("Segoe UI", 8.5f, FontStyle.Bold);
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            var rect = new RectangleF(0.5f, 0.5f, Width - 1f, Height - 1f);
            int r    = Math.Min(CornerRadius, (int)(Math.Min(rect.Width, rect.Height) / 2f));

            using var path = RoundedRect(rect, r);
            using (var fillBrush = new SolidBrush(BackColor))
                g.FillPath(fillBrush, path);

            using (var borderPen = new Pen(DefaultPillBorder, 1f))
                g.DrawPath(borderPen, path);

            var sf = new StringFormat
            {
                Alignment     = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags   = StringFormatFlags.NoWrap,
                Trimming      = StringTrimming.EllipsisCharacter
            };
            using var textBrush = new SolidBrush(ForeColor);
            g.DrawString(Text, Font, textBrush, new RectangleF(0, 0, Width, Height), sf);
        }

        private static System.Drawing.Drawing2D.GraphicsPath RoundedRect(RectangleF r, int radius)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            float d = radius * 2f;
            if (radius <= 0 || d > r.Width || d > r.Height)
            {
                path.AddRectangle(r);
                return path;
            }
            path.AddArc(r.X,         r.Y,          d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y,          d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0,   90);
            path.AddArc(r.X,         r.Bottom - d, d, d, 90,  90);
            path.CloseFigure();
            return path;
        }
    }
}
