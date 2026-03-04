namespace VisionEditCV
{
    /// <summary>
    /// A fully owner-drawn ComboBox styled to match the dark UI theme —
    /// rounded corners, cyan accent border/arrow, dark background, hover glow.
    /// Supports DropDownList mode only (no free-text entry).
    /// </summary>
    [System.ComponentModel.DesignerCategory("Component")]
    public class DarkComboBox : ComboBox
    {
        // ── Theme colours (match MainForm constants) ──────────────────────────
        private static readonly Color BgNormal  = Color.FromArgb(30, 32, 36);
        private static readonly Color BgHover   = Color.FromArgb(42, 45, 52);
        private static readonly Color BgOpen    = Color.FromArgb(22, 24, 28);
        private static readonly Color Cyan      = Color.FromArgb(0, 229, 255);
        private static readonly Color TextMain  = Color.FromArgb(220, 220, 220);
        private static readonly Color BorderNormal = Color.FromArgb(60, 63, 78);

        private bool _hovering;

        public int CornerRadius { get; set; } = 8;

        public DarkComboBox()
        {
            DrawMode      = DrawMode.OwnerDrawFixed;
            DropDownStyle = ComboBoxStyle.DropDownList;
            FlatStyle     = FlatStyle.Flat;
            BackColor     = BgNormal;
            ForeColor     = TextMain;
            Font          = new Font("Segoe UI", 8.5f);
            ItemHeight    = 28;
            Cursor        = Cursors.Hand;

            SetStyle(ControlStyles.UserPaint        |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
        }

        // ── Hover tracking ────────────────────────────────────────────────────

        protected override void OnMouseEnter(EventArgs e)
        {
            _hovering = true;
            Invalidate();
            base.OnMouseEnter(e);
        }

        protected override void OnMouseLeave(EventArgs e)
        {
            _hovering = false;
            Invalidate();
            base.OnMouseLeave(e);
        }

        // ── Main control face paint ───────────────────────────────────────────

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            bool open = DroppedDown;
            Color bg  = open ? BgOpen : (_hovering ? BgHover : BgNormal);
            Color borderColor = (open || _hovering) ? Cyan : BorderNormal;

            var rect = new RectangleF(0, 0, Width - 1, Height - 1);

            // Background
            using var fillPath = RoundedRect(rect, CornerRadius);
            using var fillBrush = new SolidBrush(bg);
            g.FillPath(fillBrush, fillPath);

            // Border
            float borderW = open ? 1.5f : 1f;
            using var borderPen = new Pen(borderColor, borderW);
            g.DrawPath(borderPen, fillPath);

            // Arrow area (right ~26px)
            const int arrowZoneW = 26;
            int arrowX = Width - arrowZoneW;

            // Subtle separator line before arrow
            using var sepPen = new Pen(Color.FromArgb(55, 58, 70), 1);
            g.DrawLine(sepPen, arrowX, 6, arrowX, Height - 6);

            // Draw the chevron arrow centred in the arrow zone
            DrawArrow(g, arrowX + arrowZoneW / 2, Height / 2, open, borderColor);

            // Selected item text
            string text = SelectedIndex >= 0 && SelectedIndex < Items.Count
                ? Items[SelectedIndex]?.ToString() ?? ""
                : "";

            using var textBrush = new SolidBrush(TextMain);
            var sf = new StringFormat
            {
                Alignment     = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                FormatFlags   = StringFormatFlags.NoWrap,
                Trimming      = StringTrimming.EllipsisCharacter
            };
            var textRect = new RectangleF(10, 0, arrowX - 14, Height);
            g.DrawString(text, Font, textBrush, textRect, sf);
        }

        private static void DrawArrow(Graphics g, int cx, int cy, bool open, Color color)
        {
            // Small chevron: 8px wide, 5px tall
            const int hw = 5;
            const int hh = 3;
            using var pen = new Pen(color, 1.8f) { LineJoin = System.Drawing.Drawing2D.LineJoin.Round };
            if (open)
            {
                // Chevron pointing up
                g.DrawLines(pen, new[]
                {
                    new PointF(cx - hw, cy + hh),
                    new PointF(cx,      cy - hh),
                    new PointF(cx + hw, cy + hh)
                });
            }
            else
            {
                // Chevron pointing down
                g.DrawLines(pen, new[]
                {
                    new PointF(cx - hw, cy - hh),
                    new PointF(cx,      cy + hh),
                    new PointF(cx + hw, cy - hh)
                });
            }
        }

        // ── Dropdown item paint ───────────────────────────────────────────────

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            if (e.Index < 0) return;

            var g = e.Graphics;
            g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            bool selected = (e.State & DrawItemState.Selected) != 0;
            Color itemBg  = selected ? Color.FromArgb(35, 80, 90) : Color.FromArgb(22, 24, 28);
            Color itemFg  = selected ? Cyan : TextMain;

            using var bgBrush = new SolidBrush(itemBg);
            g.FillRectangle(bgBrush, e.Bounds);

            if (selected)
            {
                // Thin cyan left accent bar
                using var accentBrush = new SolidBrush(Cyan);
                g.FillRectangle(accentBrush, e.Bounds.X, e.Bounds.Y, 3, e.Bounds.Height);
            }

            string text = Items[e.Index]?.ToString() ?? "";
            using var textBrush = new SolidBrush(itemFg);
            var sf = new StringFormat
            {
                Alignment     = StringAlignment.Near,
                LineAlignment = StringAlignment.Center,
                FormatFlags   = StringFormatFlags.NoWrap
            };
            var textRect = new Rectangle(e.Bounds.X + 10, e.Bounds.Y, e.Bounds.Width - 12, e.Bounds.Height);
            g.DrawString(text, Font, textBrush, textRect, sf);
        }

        // ── Invalidate on open/close so face repaints ─────────────────────────

        protected override void OnDropDown(EventArgs e)    { Invalidate(); base.OnDropDown(e); }
        protected override void OnDropDownClosed(EventArgs e) { Invalidate(); base.OnDropDownClosed(e); }
        protected override void OnSelectedIndexChanged(EventArgs e) { Invalidate(); base.OnSelectedIndexChanged(e); }

        // ── Helpers ───────────────────────────────────────────────────────────

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
