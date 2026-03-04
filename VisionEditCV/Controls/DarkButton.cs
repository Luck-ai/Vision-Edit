namespace VisionEditCV
{
    /// <summary>
    /// A WinForms Button subclass with hover/press colour, rounded corners,
    /// and fully custom painting suited to a dark UI theme.
    /// </summary>
    [System.ComponentModel.DesignerCategory("Component")]
    public class DarkButton : Button
    {
        private static readonly Color DefaultHoverBg = Color.FromArgb(52, 54, 80);
        private static readonly Color DefaultBg      = Color.FromArgb(30, 32, 36); // _BgButton
        private bool _hovering;
        private bool _pressing;

        /// <summary>Custom hover background color. If null, use DefaultHoverBg.</summary>
        [System.ComponentModel.Browsable(true)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Visible)]
        public Color? HoverBackColor { get; set; }

        /// <summary>Corner radius in pixels. Default 8.</summary>
        [System.ComponentModel.Browsable(true)]
        [System.ComponentModel.DefaultValue(8)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Visible)]
        public int CornerRadius { get; set; } = 8;

        /// <summary>
        /// When true, the left corners of the hover/press fill are rounded using
        /// <see cref="HoverCornerRadius"/>. Used for the leftmost chrome button.
        /// </summary>
        [System.ComponentModel.Browsable(true)]
        [System.ComponentModel.DefaultValue(false)]
        public bool RoundedLeft { get; set; }

        /// <summary>
        /// When true, the right corners of the hover/press fill are rounded using
        /// <see cref="HoverCornerRadius"/>. Used for the rightmost chrome button.
        /// </summary>
        [System.ComponentModel.Browsable(true)]
        [System.ComponentModel.DefaultValue(false)]
        public bool RoundedRight { get; set; }

        /// <summary>Corner radius applied to the hover fill when RoundedLeft/RoundedRight is set. Default 7.</summary>
        [System.ComponentModel.Browsable(true)]
        [System.ComponentModel.DefaultValue(7)]
        public int HoverCornerRadius { get; set; } = 7;

        /// <summary>
        /// Vertical inset (pixels) applied to the fill rect when RoundedLeft or RoundedRight
        /// is set. Used so the pill shape of chrome buttons fits inside the title bar height.
        /// Default 0 (no inset).
        /// </summary>
        [System.ComponentModel.Browsable(true)]
        [System.ComponentModel.DefaultValue(0)]
        public int PillVInset { get; set; } = 0;

        /// <summary>
        /// Set true on the middle button of a chrome pill so it uses the same vertical
        /// inset as the outer buttons but with no corner rounding.
        /// </summary>
        [System.ComponentModel.Browsable(true)]
        [System.ComponentModel.DefaultValue(false)]
        public bool PillCenter { get; set; }

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
            else if (_hovering)
            {
                if (HoverBackColor.HasValue)
                    bg = HoverBackColor.Value;
                else if (baseBg == DefaultBg)
                    bg = DefaultHoverBg;
                else
                    // Subtle brighten for custom colors
                    bg = ControlPaint.Light(baseBg, 0.1f);
            }

            int bw = FlatAppearance.BorderSize;

            var fillRect = new RectangleF(0, 0, Width - 1, Height - 1);

            // When selective rounding is active (chrome buttons inside a pill panel),
            // use RoundedRectSelective for the fill.  First clear the entire button
            // area with the panel background so the rounded corners are clean,
            // then draw the pill-portion fill on top.
            if (RoundedLeft || RoundedRight || PillCenter)
            {
                int r = HoverCornerRadius > 0 ? HoverCornerRadius : 7;
                // Apply vertical inset so the fill rect matches the pill dimensions
                var pillRect = PillVInset > 0
                    ? new RectangleF(0, PillVInset, Width - 1, Height - 1 - PillVInset * 2)
                    : fillRect;

                // 1. Clear full button area with parent (panel) background to eliminate
                //    corner artifacts outside the rounded pill shape.
                Color outsideBg = Parent?.BackColor ?? Color.FromArgb(18, 18, 24);
                using var clearBrush = new SolidBrush(outsideBg);
                g.FillRectangle(clearBrush, fillRect);

                // 2. Draw the pill fill (rounded only for left/right-edge buttons).
                if (PillCenter)
                {
                    // Middle button — plain rectangle fill with no corner rounding
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
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            float d = radius * 2f;

            // If the radius is 0 or the rectangle is too small to be rounded,
            // just return a normal rectangular path.
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

        /// <summary>
        /// Builds a GraphicsPath where only the specified sides have rounded corners.
        /// Used by chrome buttons so the hover highlight rounds only the outer edge of the pill.
        /// </summary>
        private static System.Drawing.Drawing2D.GraphicsPath RoundedRectSelective(
            RectangleF r, int radius, bool roundLeft, bool roundRight)
        {
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            float d = radius * 2f;

            // Clamp radius so arcs fit
            bool canRound = radius > 0 && d <= r.Width && d <= r.Height;
            if (!canRound) { path.AddRectangle(r); return path; }

            // Walk clockwise: TL → TR → BR → BL
            // Top-left
            if (roundLeft)
                path.AddArc(r.X, r.Y, d, d, 180, 90);          // arc from left edge to top
            else
                path.AddLine(r.X, r.Bottom, r.X, r.Y);          // left edge straight up, then snap to TL

            // Top edge
            path.AddLine(r.X + (roundLeft ? d : 0), r.Y,
                         r.Right - (roundRight ? d : 0), r.Y);

            // Top-right
            if (roundRight)
                path.AddArc(r.Right - d, r.Y, d, d, 270, 90);  // arc from top to right edge

            // Right edge
            path.AddLine(r.Right, r.Y + (roundRight ? d : 0),
                         r.Right, r.Bottom - (roundRight ? d : 0));

            // Bottom-right
            if (roundRight)
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);

            // Bottom edge
            path.AddLine(r.Right - (roundRight ? d : 0), r.Bottom,
                         r.X + (roundLeft ? d : 0), r.Bottom);

            // Bottom-left
            if (roundLeft)
                path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);

            path.CloseFigure();
            return path;
        }
    }
}
