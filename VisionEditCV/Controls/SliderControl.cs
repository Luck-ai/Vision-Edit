namespace VisionEditCV
{
    /// <summary>
    /// A fully custom-painted horizontal slider that replaces the OS TrackBar.
    /// Renders a rounded track, a cyan filled portion, and a circular thumb.
    /// The current value is printed as a small label in the top-right corner.
    /// </summary>
    [System.ComponentModel.DesignerCategory("Component")]
    public class SliderControl : Control
    {
        // ── Colours (can be overridden in the Designer via properties) ────────
        private static readonly Color TrackBg    = Color.FromArgb(50, 50, 72);
        private static readonly Color TrackFill  = Color.FromArgb(0, 229, 255);
        private static readonly Color ThumbColor = Color.FromArgb(0, 200, 230);

        // ── Backing fields ────────────────────────────────────────────────────
        private int  _min   = 0;
        private int  _max   = 100;
        private int  _value = 50;
        private bool _dragging;

        // ── Properties ────────────────────────────────────────────────────────

        [System.ComponentModel.DefaultValue(0)]
        public int Minimum
        {
            get => _min;
            set { _min = value; ClampValue(); Invalidate(); }
        }

        [System.ComponentModel.DefaultValue(100)]
        public int Maximum
        {
            get => _max;
            set { _max = value; ClampValue(); Invalidate(); }
        }

        [System.ComponentModel.DefaultValue(50)]
        public int Value
        {
            get => _value;
            set
            {
                int clamped = Math.Clamp(value, _min, _max);
                if (clamped == _value) return;
                _value = clamped;
                Invalidate();
                ValueChanged?.Invoke(this, EventArgs.Empty);
            }
        }

        public event EventHandler? ValueChanged;

        // ── Constructor ───────────────────────────────────────────────────────

        public SliderControl()
        {
            SetStyle(ControlStyles.UserPaint        |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.ResizeRedraw, true);
            BackColor = Color.FromArgb(18, 18, 18);
            Cursor = Cursors.Hand;
            Size   = new Size(150, 32);
        }

        // ── Internal geometry ─────────────────────────────────────────────────

        private void ClampValue() => _value = Math.Clamp(_value, _min, _max);

        private int TrackLeft()  => 8;
        private int TrackRight() => Width - 8;
        private int TrackWidth() => Math.Max(1, TrackRight() - TrackLeft());
        private int TrackY()     => Height / 2;

        private int ThumbX()
        {
            if (_max == _min) return TrackLeft();
            float ratio = (float)(_value - _min) / (_max - _min);
            return TrackLeft() + (int)(ratio * TrackWidth());
        }

        // ── Painting ──────────────────────────────────────────────────────────

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            int ty   = TrackY();
            int tl   = TrackLeft();
            int tr   = TrackRight();
            int tx   = ThumbX();
            int trad = 7; // thumb radius

            // Track background
            using (var br = new SolidBrush(TrackBg))
                GraphicsExtensions.FillRoundedRect(g, br, new RectangleF(tl, ty - 3, tr - tl, 6), 3);

            // Filled portion
            if (tx > tl)
                using (var br = new SolidBrush(TrackFill))
                    GraphicsExtensions.FillRoundedRect(g, br, new RectangleF(tl, ty - 3, tx - tl, 6), 3);

            // Thumb circle
            using (var br = new SolidBrush(ThumbColor))
                g.FillEllipse(br, tx - trad, ty - trad, trad * 2, trad * 2);

            // Value label (right-aligned, below track)
            using var font = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            using var tb   = new SolidBrush(Color.FromArgb(0, 229, 255));
            var sf = new StringFormat
            {
                Alignment     = StringAlignment.Far,
                LineAlignment = StringAlignment.Far
            };
            g.DrawString(_value.ToString(), font, tb,
                new RectangleF(0, 0, Width - 2, Height - 1), sf);
        }

        // ── Mouse interaction ─────────────────────────────────────────────────

        protected override void OnMouseDown(MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                _dragging = true;
                UpdateFromMouse(e.X);
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            if (_dragging) UpdateFromMouse(e.X);
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            _dragging = false;
        }

        private void UpdateFromMouse(int x)
        {
            float ratio = Math.Clamp((float)(x - TrackLeft()) / TrackWidth(), 0f, 1f);
            Value = _min + (int)Math.Round(ratio * (_max - _min));
        }
    }
}
