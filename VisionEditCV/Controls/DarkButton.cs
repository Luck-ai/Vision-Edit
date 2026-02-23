namespace VisionEditCV
{
    /// <summary>
    /// A WinForms Button subclass with hover/press colour, rounded corners,
    /// and fully custom painting suited to a dark UI theme.
    /// </summary>
    [System.ComponentModel.DesignerCategory("Component")]
    public class DarkButton : Button
    {
        private static readonly Color HoverBg   = Color.FromArgb(52, 54, 80);
        private static readonly Color DefaultBg = Color.FromArgb(30, 32, 36); // _BgButton
        private bool _hovering;
        private bool _pressing;

        /// <summary>Corner radius in pixels. Default 8.</summary>
        [System.ComponentModel.Browsable(true)]
        [System.ComponentModel.DefaultValue(8)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Visible)]
        public int CornerRadius { get; set; } = 8;

        /// <summary>
        /// Optional single emoji/Unicode character drawn to the left of the
        /// button text in a slightly larger Segoe UI Emoji font.
        /// Leave null/empty to draw no icon.
        /// </summary>
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

            // Resolve the effective background color.
            // Transparent → paint the parent's background color instead so the
            // custom rounded path clips correctly on dark panels.
            Color baseBg = BackColor == Color.Transparent
                ? (Parent?.BackColor ?? Color.FromArgb(22, 22, 22))
                : BackColor;

            Color bg = baseBg;
            if (_pressing)
                bg = ControlPaint.Dark(baseBg, 0.15f);
            else if (_hovering && baseBg == DefaultBg)
                // Only apply hover highlight for "plain" default-state buttons.
                // Active-state buttons (teal, cyan, etc.) keep their own color on hover.
                bg = HoverBg;

            int bw = FlatAppearance.BorderSize;

            // Fill uses the full bounds (no inset needed for fill)
            var fillRect = new RectangleF(0, 0, Width - 1, Height - 1);
            using var fillPath = RoundedRect(fillRect, CornerRadius);
            using var fill = new SolidBrush(bg);
            g.FillPath(fill, fillPath);

            if (bw > 0)
            {
                // Inset the border rect by half the pen width so the stroke sits
                // entirely inside the control bounds — gives uniform thickness on all edges.
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
                // Icon column: fixed 36px on the left inside Padding.Left
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

                // Text column: starts after icon
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
            float d    = radius * 2f;
            var   path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(r.X,         r.Y,          d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y,          d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0,   90);
            path.AddArc(r.X,         r.Bottom - d, d, d, 90,  90);
            path.CloseFigure();
            return path;
        }
    }
}
