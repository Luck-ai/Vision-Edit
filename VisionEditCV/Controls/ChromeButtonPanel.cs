namespace VisionEditCV.Controls
{
    /// <summary>
    /// A single owner-drawn control that renders the three window-chrome buttons
    /// (minimize, maximize, close) as one unified pill shape — no child controls,
    /// no layering issues.
    ///
    /// Layout (left → right):  [─ Minimize][🗖 Maximize][✕ Close]
    /// Each slot is exactly 1/3 of the control width.
    ///
    /// Events: <see cref="MinimizeClicked"/>, <see cref="MaximizeClicked"/>,
    ///         <see cref="CloseClicked"/>.
    /// </summary>
    [System.ComponentModel.DesignerCategory("Component")]
    public class ChromeButtonPanel : Control
    {
        // ── Colours ──────────────────────────────────────────────────────────
        private static readonly Color TitleBarBg   = Color.FromArgb(18,  18,  24);
        private static readonly Color PillBg        = Color.FromArgb(38,  40,  52);
        private static readonly Color HoverMin      = Color.FromArgb(55,  57,  75);
        private static readonly Color HoverMax      = Color.FromArgb(55,  57,  75);
        private static readonly Color HoverClose    = Color.FromArgb(196, 43,  43);
        private static readonly Color AccentLine    = Color.FromArgb(0,  160, 180);
        private static readonly Color IconColor     = Color.FromArgb(200, 200, 210);

        // ── Pill geometry ─────────────────────────────────────────────────────
        private const int PillRadius = 7;
        private const int PillVInset = 7;   // top/bottom margin inside the control height

        // ── Hit state ────────────────────────────────────────────────────────
        private enum Zone { None, Min, Max, Close }
        private Zone _hovered = Zone.None;
        private Zone _pressed = Zone.None;

        // ── Events ────────────────────────────────────────────────────────────
        public event EventHandler? MinimizeClicked;
        public event EventHandler? MaximizeClicked;
        public event EventHandler? CloseClicked;

        public ChromeButtonPanel()
        {
            SetStyle(
                ControlStyles.UserPaint |
                ControlStyles.AllPaintingInWmPaint |
                ControlStyles.OptimizedDoubleBuffer |
                ControlStyles.ResizeRedraw,
                true);
            Cursor = Cursors.Default;
            BackColor = TitleBarBg;   // matches the title bar; panel edges are invisible
        }

        // ── Zone helpers ─────────────────────────────────────────────────────
        private int SlotW => Width / 3;

        private Zone HitZone(Point p)
        {
            if (p.X < 0 || p.X >= Width || p.Y < 0 || p.Y >= Height) return Zone.None;
            int slot = p.X / SlotW;
            return slot switch { 0 => Zone.Min, 1 => Zone.Max, _ => Zone.Close };
        }

        // ── Mouse ─────────────────────────────────────────────────────────────
        protected override void OnMouseMove(MouseEventArgs e)
        {
            var z = HitZone(e.Location);
            if (z != _hovered) { _hovered = z; Invalidate(); }
            base.OnMouseMove(e);
        }
        protected override void OnMouseLeave(EventArgs e)
        {
            _hovered = Zone.None; _pressed = Zone.None; Invalidate();
            base.OnMouseLeave(e);
        }
        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left) { _pressed = HitZone(e.Location); Invalidate(); }
            base.OnMouseDown(e);
        }
        protected override void OnMouseUp(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left && _pressed != Zone.None)
            {
                var z = HitZone(e.Location);
                if (z == _pressed)
                {
                    if (z == Zone.Min)   MinimizeClicked?.Invoke(this, EventArgs.Empty);
                    else if (z == Zone.Max)   MaximizeClicked?.Invoke(this, EventArgs.Empty);
                    else if (z == Zone.Close) CloseClicked?.Invoke(this, EventArgs.Empty);
                }
                _pressed = Zone.None; Invalidate();
            }
            base.OnMouseUp(e);
        }

        // ── Paint ─────────────────────────────────────────────────────────────
        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int w = Width, h = Height;
            int sw = SlotW;

            // 1. Fill entire control with title-bar background (makes edges invisible)
            g.Clear(TitleBarBg);

            // 2. Build pill rect — vertically centred
            float py = PillVInset;
            float ph = h - PillVInset * 2;
            float pr = PillRadius;
            var pill = new RectangleF(0, py, w - 1, ph);

            // 3. Paint each slot's background (hover/press or pill base)
            // We paint slots individually so hover only highlights one zone, then
            // we clip the whole thing to the pill path for clean rounded corners.

            // Set up clip to pill shape
            using var pillPath = RoundedPath(pill, pr);
            g.SetClip(pillPath);

            // Slot backgrounds
            PaintSlot(g, new RectangleF(0,       py, sw,     ph), Zone.Min);
            PaintSlot(g, new RectangleF(sw,      py, sw,     ph), Zone.Max);
            PaintSlot(g, new RectangleF(sw * 2,  py, w - sw * 2, ph), Zone.Close);

            g.ResetClip();

            // 4. Draw icons centred in each slot
            using var iconFont = new Font("Segoe UI Symbol", 10f, FontStyle.Regular);
            using var iconBrush = new SolidBrush(IconColor);
            var sf = new StringFormat
            {
                Alignment     = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags   = StringFormatFlags.NoWrap
            };

            // Slot rects for icon centering use full height so text is centred in pill
            g.DrawString("─", iconFont, iconBrush, new RectangleF(0,      py, sw,          ph), sf);
            g.DrawString("🗖", iconFont, iconBrush, new RectangleF(sw,     py, sw,          ph), sf);
            g.DrawString("✕", iconFont, iconBrush, new RectangleF(sw * 2, py, w - sw * 2,  ph), sf);

            // 5. Bottom accent line — full width, at the very bottom of the control
            //    This continues the title-bar cyan line seamlessly through the panel.
            using var accentPen = new Pen(AccentLine, 1);
            g.DrawLine(accentPen, 0, h - 1, w, h - 1);
        }

        private void PaintSlot(Graphics g, RectangleF rect, Zone zone)
        {
            Color bg = PillBg;
            bool active = _pressed == zone || (_pressed == Zone.None && _hovered == zone);
            if (active)
            {
                bg = zone switch
                {
                    Zone.Min   => HoverMin,
                    Zone.Max   => HoverMax,
                    Zone.Close => HoverClose,
                    _          => PillBg
                };
                if (_pressed == zone)
                    bg = ControlPaint.Dark(bg, 0.15f);
            }
            using var br = new SolidBrush(bg);
            g.FillRectangle(br, rect);
        }

        private static System.Drawing.Drawing2D.GraphicsPath RoundedPath(RectangleF r, float radius)
        {
            var p = new System.Drawing.Drawing2D.GraphicsPath();
            float d = radius * 2f;
            p.AddArc(r.X,          r.Y,           d, d, 180, 90);
            p.AddArc(r.Right - d,  r.Y,           d, d, 270, 90);
            p.AddArc(r.Right - d,  r.Bottom - d,  d, d, 0,   90);
            p.AddArc(r.X,          r.Bottom - d,  d, d, 90,  90);
            p.CloseFigure();
            return p;
        }
    }
}
