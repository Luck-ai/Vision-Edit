namespace VisionEditCV.Controls
{
    [System.ComponentModel.DesignerCategory("Component")]
    public class MaskListPanel : Panel
    {
        private static readonly Color BgColor     = Color.FromArgb(20, 20, 20);
        private static readonly Color CardBg      = Color.FromArgb(32, 33, 48);
        private static readonly Color CardHover   = Color.FromArgb(42, 44, 64);
        private static readonly Color CardSelected = Color.FromArgb(0, 50, 70);
        private static readonly Color CardSelectedBorder = Color.FromArgb(0, 229, 255);
        private static readonly Color TextColor   = Color.FromArgb(220, 220, 220);
        private static readonly Color AccentCyan  = Color.FromArgb(0, 229, 255);

        private readonly List<MaskCard> _rows = new();

        public event EventHandler<MaskSelectedEventArgs>? MaskSelectionChanged;

        public MaskListPanel()
        {
            BackColor  = BgColor;
            AutoScroll = true;
            Padding    = new Padding(8, 0, 8, 4);
        }

        public void Populate(List<Color> colors, List<float> scores)
        {
            SuspendLayout();
            ClearRows();

            AutoScrollPosition = new Point(0, 0);
            for (int i = 0; i < colors.Count; i++)
            {
                float score = i < scores.Count ? scores[i] : 0f;
                var card = new MaskCard(i, colors[i], score);
                card.CheckChanged += OnRowCheckChanged;
                _rows.Add(card);
                Controls.Add(card);
            }
            ResumeLayout(false);
            LayoutRows();

            AutoScrollPosition = new Point(0, 0);
        }

        public void ClearRows()
        {
            foreach (var r in _rows)
            {
                r.CheckChanged -= OnRowCheckChanged;
                Controls.Remove(r);
                r.Dispose();
            }
            _rows.Clear();
        }

        public void SetRowSelected(int index, bool selected)
        {
            if (index < 0 || index >= _rows.Count) return;
            _rows[index].SetCheckedSilent(selected);
            _rows[index].Invalidate();
        }

        private void LayoutRows()
        {
            int y = 4; 
            int w = Math.Max(1, ClientSize.Width - Padding.Left - Padding.Right
                    - (VScroll ? SystemInformation.VerticalScrollBarWidth : 0));
            foreach (var card in _rows)
            {
                card.Location = new Point(Padding.Left, y);
                card.Width    = w;
                y += card.Height + 6;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            LayoutRows();
        }

        private void OnRowCheckChanged(object? sender, MaskSelectedEventArgs e)
        {
            MaskSelectionChanged?.Invoke(this, e);
        }

        private class MaskCard : Control
        {
            private readonly int   _index;
            private readonly Color _maskColor;
            private readonly float _score;
            private bool _checked;
            private bool _hovering;

            public event EventHandler<MaskSelectedEventArgs>? CheckChanged;

            public MaskCard(int index, Color color, float score)
            {
                _index     = index;
                _maskColor = color;
                _score     = score;
                Height     = 44;
                SetStyle(ControlStyles.OptimizedDoubleBuffer |
                         ControlStyles.AllPaintingInWmPaint  |
                         ControlStyles.UserPaint, true);
                Cursor = Cursors.Hand;
            }

            public void SetCheckedSilent(bool value) => _checked = value;

            protected override void OnPaint(PaintEventArgs e)
            {
                var g = e.Graphics;
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

                var bg = _checked ? CardSelected : (_hovering ? CardHover : CardBg);
                using var bgBrush = new SolidBrush(bg);
                var rect = new Rectangle(0, 0, Width - 1, Height - 1);
                GraphicsExtensions.FillRoundedRect(g, bgBrush, rect, 8);

                if (_checked)
                {
                    using var borderPen = new Pen(CardSelectedBorder, 2f);
                    GraphicsExtensions.DrawRoundedRect(g, borderPen, rect, 8);
                }
                else if (_hovering)
                {
                    using var borderPen = new Pen(Color.FromArgb(60, 0, 229, 255), 1f);
                    GraphicsExtensions.DrawRoundedRect(g, borderPen, rect, 8);
                }

                int swatchSize = 24;
                int swatchX = 10;
                int swatchY = (Height - swatchSize) / 2;
                using var swatchBrush = new SolidBrush(_maskColor);
                g.FillEllipse(swatchBrush, swatchX, swatchY, swatchSize, swatchSize);
                using var ringPen = new Pen(Color.FromArgb(80, 255, 255, 255), 1.2f);
                g.DrawEllipse(ringPen, swatchX, swatchY, swatchSize, swatchSize);

                int textX = swatchX + swatchSize + 10;
                string label = $"Mask {_index + 1}";
                using var labelFont  = new Font("Segoe UI", 10f, FontStyle.Bold);
                using var labelBrush = new SolidBrush(_checked ? AccentCyan : TextColor);
                var labelSize = g.MeasureString(label, labelFont);
                g.DrawString(label, labelFont, labelBrush,
                    textX, (Height - labelSize.Height) / 2f);
            }

            protected override void OnMouseEnter(EventArgs e)
            {
                _hovering = true;
                Invalidate();
            }

            protected override void OnMouseLeave(EventArgs e)
            {
                _hovering = false;
                Invalidate();
            }

            protected override void OnClick(EventArgs e)
            {
                _checked = !_checked;
                Invalidate();
                CheckChanged?.Invoke(this, new MaskSelectedEventArgs(_index, _checked));
            }
        }
    }
}
