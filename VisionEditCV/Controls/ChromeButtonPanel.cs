namespace VisionEditCV.Controls
{
    [System.ComponentModel.DesignerCategory("Component")]
    public class ChromeButtonPanel : Control
    {
        private static readonly Color TitleBarBg   = Color.FromArgb(18,  18,  24);
        private static readonly Color PillBg        = Color.FromArgb(38,  40,  52);
        private static readonly Color HoverMin      = Color.FromArgb(55,  57,  75);
        private static readonly Color HoverMax      = Color.FromArgb(55,  57,  75);
        private static readonly Color HoverClose    = Color.FromArgb(196, 43,  43);
        private static readonly Color AccentLine    = Color.FromArgb(0,  160, 180);
        private static readonly Color IconColor     = Color.FromArgb(200, 200, 210);

        private const int PillRadius = 7;
        private const int PillVInset = 7;   

        private enum Zone { None, Min, Max, Close }
        private Zone _hovered = Zone.None;
        private Zone _pressed = Zone.None;

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
            BackColor = TitleBarBg;   
        }

        private int SlotW => Width / 3;

        private Zone HitZone(Point p)
        {
            if (p.X < 0 || p.X >= Width || p.Y < 0 || p.Y >= Height) return Zone.None;
            int slot = p.X / SlotW;
            return slot switch { 0 => Zone.Min, 1 => Zone.Max, _ => Zone.Close };
        }

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

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode     = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            int w = Width, h = Height;
            int sw = SlotW;

            g.Clear(TitleBarBg);

            float py = PillVInset;
            float ph = h - PillVInset * 2;
            float pr = PillRadius;
            var pill = new RectangleF(0, py, w - 1, ph);

            using var pillPath = RoundedPath(pill, pr);
            g.SetClip(pillPath);

            PaintSlot(g, new RectangleF(0,       py, sw,     ph), Zone.Min);
            PaintSlot(g, new RectangleF(sw,      py, sw,     ph), Zone.Max);
            PaintSlot(g, new RectangleF(sw * 2,  py, w - sw * 2, ph), Zone.Close);

            g.ResetClip();

            using var iconFont = new Font("Segoe UI Symbol", 10f, FontStyle.Regular);
            using var iconBrush = new SolidBrush(IconColor);
            var sf = new StringFormat
            {
                Alignment     = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
                FormatFlags   = StringFormatFlags.NoWrap
            };

            g.DrawString("─", iconFont, iconBrush, new RectangleF(0,      py, sw,          ph), sf);
            g.DrawString("🗖", iconFont, iconBrush, new RectangleF(sw,     py, sw,          ph), sf);
            g.DrawString("✕", iconFont, iconBrush, new RectangleF(sw * 2, py, w - sw * 2,  ph), sf);

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
