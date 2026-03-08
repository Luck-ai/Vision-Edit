namespace VisionEditCV
{
    [System.ComponentModel.DesignerCategory("Component")]
    public class DarkButton : Button
    {
        private static readonly Color DefaultHoverBg = Color.FromArgb(52, 54, 80);
        private static readonly Color DefaultBg      = Color.FromArgb(30, 32, 36); 
        private bool _hovering;
        private bool _pressing;

        [System.ComponentModel.Browsable(true)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Visible)]
        public Color? HoverBackColor { get; set; }

        [System.ComponentModel.Browsable(true)]
        [System.ComponentModel.DefaultValue(8)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Visible)]
        public int CornerRadius { get; set; } = 8;

        [System.ComponentModel.Browsable(true)]
        [System.ComponentModel.DefaultValue(false)]
        public bool RoundedLeft { get; set; }

        [System.ComponentModel.Browsable(true)]
        [System.ComponentModel.DefaultValue(false)]
        public bool RoundedRight { get; set; }

        [System.ComponentModel.Browsable(true)]
        [System.ComponentModel.DefaultValue(7)]
        public int HoverCornerRadius { get; set; } = 7;

        [System.ComponentModel.Browsable(true)]
        [System.ComponentModel.DefaultValue(0)]
        public int PillVInset { get; set; } = 0;

        [System.ComponentModel.Browsable(true)]
        [System.ComponentModel.DefaultValue(false)]
        public bool PillCenter { get; set; }

        [System.ComponentModel.Browsable(true)]
        [System.ComponentModel.DefaultValue(null)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Visible)]
        public string? Icon { get; set; }

        public DarkButton()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
            Cursor    = Cursors.Hand;
            FlatStyle = FlatStyle.Flat;
            FlatAppearance.BorderSize  = 0;
            FlatAppearance.BorderColor = Color.FromArgb(50, 50, 70);
        }

        protected override void OnMouseEnter(EventArgs e) { _hovering = true;  Invalidate(); base.OnMouseEnter(e); }
        protected override void OnMouseLeave(EventArgs e) { _hovering = false; _pressing = false; Invalidate(); base.OnMouseLeave(e); }
        protected override void OnMouseDown(MouseEventArgs e) { _pressing = true;  Invalidate(); base.OnMouseDown(e); }
        protected override void OnMouseUp(MouseEventArgs e)   { _pressing = false; Invalidate(); base.OnMouseUp(e); }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            Color baseBg = BackColor == Color.Transparent
                ? (Parent?.BackColor ?? Color.FromArgb(22, 22, 22))
                : BackColor;

            Color bg = baseBg;
            if (_pressing)
                bg = ControlPaint.Dark(baseBg, 0.15f);
            else if (_hovering)
            {
                if (HoverBackColor.HasValue)
                    bg = HoverBackColor.Value;
                else if (baseBg == DefaultBg)
                    bg = DefaultHoverBg;
                else

                    bg = ControlPaint.Light(baseBg, 0.1f);
            }

            int bw = FlatAppearance.BorderSize;

            var fillRect = new RectangleF(0, 0, Width - 1, Height - 1);

            if (RoundedLeft || RoundedRight || PillCenter)
            {
                int r = HoverCornerRadius > 0 ? HoverCornerRadius : 7;

                var pillRect = PillVInset > 0
                    ? new RectangleF(0, PillVInset, Width - 1, Height - 1 - PillVInset * 2)
                    : fillRect;

                Color outsideBg = Parent?.BackColor ?? Color.FromArgb(18, 18, 24);
                using var clearBrush = new SolidBrush(outsideBg);
                g.FillRectangle(clearBrush, fillRect);

                if (PillCenter)
                {
                    using var fill = new SolidBrush(bg);
                    g.FillRectangle(fill, pillRect);
                }
                else
                {
                    using var path = RoundedRectSelective(pillRect, r, RoundedLeft, RoundedRight);
                    using var fill = new SolidBrush(bg);
                    g.FillPath(fill, path);
                }
            }
            else
            {
                using var fillPath = RoundedRect(fillRect, CornerRadius);
                using var fill = new SolidBrush(bg);
                g.FillPath(fill, fillPath);
            }

            if (bw > 0)
            {
                float half = bw / 2f;
                var borderRect = new RectangleF(half, half, Width - 1 - bw, Height - 1 - bw);
                int borderRadius = Math.Max(0, CornerRadius - (int)Math.Ceiling(half));
                using var borderPath = RoundedRect(borderRect, borderRadius);
                using var pen = new Pen(FlatAppearance.BorderColor, bw);
                g.DrawPath(pen, borderPath);
            }

            var sf = new StringFormat
            {
                Alignment     = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags   = StringFormatFlags.NoWrap,
                Trimming      = StringTrimming.EllipsisCharacter
            };
            using var brush = new SolidBrush(ForeColor);

            if (!string.IsNullOrEmpty(Icon))
            {
                const int iconColW = 36;
                int iconLeft = Padding.Left > 0 ? Padding.Left : 10;
                var iconRect = new RectangleF(iconLeft, 0, iconColW, Height);
                using var iconFont = new Font("Segoe UI Emoji", Font.Size + 1f, FontStyle.Regular);
                var iconSf = new StringFormat
                {
                    Alignment     = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center,
                    FormatFlags   = StringFormatFlags.NoWrap
                };
                g.DrawString(Icon, iconFont, brush, iconRect, iconSf);

                int textLeft = iconLeft + iconColW + 2;
                var textRect = new RectangleF(textLeft, 0,
                    Math.Max(1, Width - textLeft - Padding.Right - 4), Height);
                var textSf = new StringFormat
                {
                    Alignment     = StringAlignment.Near,
                    LineAlignment = StringAlignment.Center,
                    FormatFlags   = StringFormatFlags.NoWrap,
                    Trimming      = StringTrimming.EllipsisCharacter
                };
                g.DrawString(Text, Font, brush, textRect, textSf);
            }
            else
            {
                sf.Alignment = TextAlign switch
                {
                    ContentAlignment.MiddleCenter => StringAlignment.Center,
                    ContentAlignment.MiddleLeft   => StringAlignment.Near,
                    ContentAlignment.MiddleRight  => StringAlignment.Far,
                    _                             => StringAlignment.Center
                };
                var textRect = new RectangleF(Padding.Left + 2, 0,
                    Width - Padding.Left - Padding.Right - 4, Height);
                g.DrawString(Text, Font, brush, textRect, sf);
            }
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

        private static System.Drawing.Drawing2D.GraphicsPath RoundedRectSelective(
            RectangleF r, int radius, bool roundLeft, bool roundRight)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            float d = radius * 2f;

            bool canRound = radius > 0 && d <= r.Width && d <= r.Height;
            if (!canRound) { path.AddRectangle(r); return path; }

            if (roundLeft)
                path.AddArc(r.X, r.Y, d, d, 180, 90);          
            else
                path.AddLine(r.X, r.Bottom, r.X, r.Y);          

            path.AddLine(r.X + (roundLeft ? d : 0), r.Y,
                         r.Right - (roundRight ? d : 0), r.Y);

            if (roundRight)
                path.AddArc(r.Right - d, r.Y, d, d, 270, 90);  

            path.AddLine(r.Right, r.Y + (roundRight ? d : 0),
                         r.Right, r.Bottom - (roundRight ? d : 0));

            if (roundRight)
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);

            path.AddLine(r.Right - (roundRight ? d : 0), r.Bottom,
                         r.X + (roundLeft ? d : 0), r.Bottom);

            if (roundLeft)
                path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);

            path.CloseFigure();
            return path;
        }
    }
}
