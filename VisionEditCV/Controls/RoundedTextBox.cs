using System.Drawing.Drawing2D;

namespace VisionEditCV.Controls
{
    /// <summary>
    /// A single-line text input with a custom-drawn rounded border that matches
    /// the DarkButton active style (dark fill, cyan rounded border).
    /// The inner native TextBox is inset so its edges are hidden behind the custom painting.
    /// </summary>
    [System.ComponentModel.DesignerCategory("Component")]
    public class RoundedTextBox : Control
    {
        // ── Native TextBox hosted inside ────────────────────────────────────
        private readonly TextBox _inner;

        // ── Visual properties ────────────────────────────────────────────────
        private Color _borderColor  = Color.FromArgb(0, 229, 255);
        private Color _borderFocus  = Color.FromArgb(0, 229, 255);
        private int   _cornerRadius = 10;
        private int   _borderWidth  = 1;
        private bool  _focused;

        [System.ComponentModel.Browsable(true)]
        [System.ComponentModel.DefaultValue(10)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Visible)]
        public int CornerRadius
        {
            get => _cornerRadius;
            set { _cornerRadius = value; Invalidate(); }
        }

        [System.ComponentModel.Browsable(true)]
        [System.ComponentModel.DefaultValue(1)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Visible)]
        public int BorderWidth
        {
            get => _borderWidth;
            set { _borderWidth = value; UpdateInnerBounds(); Invalidate(); }
        }

        [System.ComponentModel.Browsable(true)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Visible)]
        public Color BorderColor
        {
            get => _borderColor;
            set { _borderColor = value; Invalidate(); }
        }

        /// <summary>Placeholder text shown when the box is empty.</summary>
        [System.ComponentModel.Browsable(true)]
        [System.ComponentModel.DefaultValue("")]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Visible)]
        public string PlaceholderText
        {
            get => _inner.PlaceholderText;
            set => _inner.PlaceholderText = value;
        }

        /// <summary>The text the user has typed.</summary>
        public override string? Text
        {
            get => _inner.Text;
            set => _inner.Text = value ?? string.Empty;
        }

        public override Font? Font
        {
            get => base.Font;
            set { base.Font = value; if (value != null) _inner.Font = value; }
        }

        public RoundedTextBox()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer | ControlStyles.ResizeRedraw, true);

            _inner = new TextBox
            {
                BorderStyle  = BorderStyle.None,
                BackColor    = Color.FromArgb(30, 32, 36),
                ForeColor    = Color.FromArgb(220, 220, 220),
                Font         = new Font("Segoe UI", 9.5f),
                TabStop      = true,
            };

            _inner.GotFocus  += (s, e) => { _focused = true;  Invalidate(); };
            _inner.LostFocus += (s, e) => { _focused = false; Invalidate(); };
            _inner.TextChanged += (s, e) => OnTextChanged(e);
            _inner.KeyDown     += (s, e) => OnKeyDown(e);

            Controls.Add(_inner);

            BackColor = Color.FromArgb(30, 32, 36);
            ForeColor = Color.FromArgb(220, 220, 220);
            Cursor    = Cursors.IBeam;
            Size      = new Size(280, 36);
        }

        protected override void OnHandleCreated(EventArgs e)
        {
            base.OnHandleCreated(e);
            UpdateInnerBounds();
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            UpdateInnerBounds();
        }

        private void UpdateInnerBounds()
        {
            int pad = _borderWidth + _cornerRadius / 3 + 3;
            int innerY = (Height - _inner.PreferredHeight) / 2;
            _inner.SetBounds(pad, innerY, Math.Max(1, Width - pad * 2), _inner.PreferredHeight);
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            _inner.Focus();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            g.SmoothingMode = SmoothingMode.AntiAlias;

            // Background fill
            float half = _borderWidth / 2f;
            var bgRect = new RectangleF(half, half, Width - 1 - _borderWidth, Height - 1 - _borderWidth);
            using var bgPath = RoundedPath(bgRect, _cornerRadius);
            using var bgBrush = new SolidBrush(BackColor);
            g.FillPath(bgBrush, bgPath);

            // Border — inset by half pen width for uniform thickness
            var borderColor = _focused ? _borderFocus : Color.FromArgb(60, 80, 100);
            using var borderPath = RoundedPath(bgRect, Math.Max(0, _cornerRadius - (int)Math.Ceiling(half)));
            using var pen = new Pen(borderColor, _borderWidth);
            g.DrawPath(pen, borderPath);
        }

        private static GraphicsPath RoundedPath(RectangleF r, int radius)
        {
            float d = radius * 2f;
            var p = new GraphicsPath();
            p.AddArc(r.X,          r.Y,           d, d, 180, 90);
            p.AddArc(r.Right - d,  r.Y,           d, d, 270, 90);
            p.AddArc(r.Right - d,  r.Bottom - d,  d, d,   0, 90);
            p.AddArc(r.X,          r.Bottom - d,  d, d,  90, 90);
            p.CloseFigure();
            return p;
        }
    }
}
