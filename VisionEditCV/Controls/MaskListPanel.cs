namespace VisionEditCV.Controls
{
    /// <summary>
    /// Scrollable dark panel displaying one card per segmentation mask.
    /// Each card has a colored swatch, mask name, score bar, and a toggle indicator.
    /// Selection state is bidirectionally synced with ImageCanvas.
    /// </summary>
    [System.ComponentModel.DesignerCategory("Component")]
    public class MaskListPanel : Panel
    {
        // ── Theme ────────────────────────────────────────────────────────────
        private static readonly Color BgColor     = Color.FromArgb(20, 20, 20);
        private static readonly Color CardBg      = Color.FromArgb(32, 33, 48);
        private static readonly Color CardHover   = Color.FromArgb(42, 44, 64);
        private static readonly Color CardSelected = Color.FromArgb(0, 50, 70);
        private static readonly Color CardSelectedBorder = Color.FromArgb(0, 229, 255);
        private static readonly Color TextColor   = Color.FromArgb(220, 220, 220);
        private static readonly Color DimText     = Color.FromArgb(130, 135, 160);
        private static readonly Color AccentCyan  = Color.FromArgb(0, 229, 255);
        private static readonly Color ScoreBarBg  = Color.FromArgb(45, 46, 62);

        // ── State ────────────────────────────────────────────────────────────
        private readonly List<MaskCard> _rows = new();

        // ── Events ───────────────────────────────────────────────────────────
        public event EventHandler<MaskSelectedEventArgs>? MaskSelectionChanged;

        public MaskListPanel()
        {
            BackColor  = BgColor;
            AutoScroll = true;
            Padding    = new Padding(8, 4, 8, 4);
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void Populate(List<Color> colors, List<float> scores)
        {
            ClearRows();
            for (int i = 0; i < colors.Count; i++)
            {
                float score = i < scores.Count ? scores[i] : 0f;
                var card = new MaskCard(i, colors[i], score);
                card.CheckChanged += OnRowCheckChanged;
                _rows.Add(card);
                Controls.Add(card);
            }
            LayoutRows();
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

        /// <summary>Sync a row's checked state from the canvas without firing the event.</summary>
        public void SetRowSelected(int index, bool selected)
        {
            if (index < 0 || index >= _rows.Count) return;
            _rows[index].SetCheckedSilent(selected);
            _rows[index].Invalidate();
        }

        // ── Layout ────────────────────────────────────────────────────────────

        private void LayoutRows()
        {
            int y = Padding.Top;
            int w = ClientSize.Width - Padding.Left - Padding.Right
                    - SystemInformation.VerticalScrollBarWidth;
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

        // ── Event handler ─────────────────────────────────────────────────────

        private void OnRowCheckChanged(object? sender, MaskSelectedEventArgs e)
        {
            MaskSelectionChanged?.Invoke(this, e);
        }

        // ── Inner card control ──────────────────────────────────────────────

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
                Height     = 62;
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

                // Card background with rounded corners
                var bg = _checked ? CardSelected : (_hovering ? CardHover : CardBg);
                using var bgBrush = new SolidBrush(bg);
                var rect = new Rectangle(0, 0, Width - 1, Height - 1);
                GraphicsExtensions.FillRoundedRect(g, bgBrush, rect, 8);

                // Selected border glow
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

                // ── Left: Colored circle swatch with number ──
                int swatchSize = 32;
                int swatchX = 12;
                int swatchY = (Height - swatchSize) / 2;
                using var swatchBrush = new SolidBrush(_maskColor);
                g.FillEllipse(swatchBrush, swatchX, swatchY, swatchSize, swatchSize);
                using var ringPen = new Pen(Color.FromArgb(80, 255, 255, 255), 1.2f);
                g.DrawEllipse(ringPen, swatchX, swatchY, swatchSize, swatchSize);

                // Number inside swatch
                using var numFont  = new Font("Segoe UI", 9f, FontStyle.Bold);
                using var numBrush = new SolidBrush(Color.White);
                var numStr  = (_index + 1).ToString();
                var numSize = g.MeasureString(numStr, numFont);
                g.DrawString(numStr, numFont, numBrush,
                    swatchX + (swatchSize - numSize.Width) / 2,
                    swatchY + (swatchSize - numSize.Height) / 2);

                // ── Right: Toggle indicator ──
                int indSize = 14;
                int indX = Width - indSize - 12;
                int indY = 12;

                // ── Middle: Text + score ──
                int textX = swatchX + swatchSize + 12;
                int rightEdge = indX - 8; // leave space before toggle

                // Mask name row: label + score percentage
                string label = $"Mask {_index + 1}";
                string scoreStr = $"{(_score * 100):F0}%";
                using var labelFont  = new Font("Segoe UI", 10f, FontStyle.Bold);
                using var labelBrush = new SolidBrush(_checked ? AccentCyan : TextColor);
                g.DrawString(label, labelFont, labelBrush, textX, 8);

                using var scoreFont  = new Font("Segoe UI", 8.5f, FontStyle.Bold);
                using var scoreBrush = new SolidBrush(_checked ? AccentCyan : DimText);
                var scoreSize = g.MeasureString(scoreStr, scoreFont);
                g.DrawString(scoreStr, scoreFont, scoreBrush,
                    rightEdge - scoreSize.Width, 10);

                // Score bar below label
                int barY = 36;
                int barH = 6;
                int barW = Math.Max(20, rightEdge - textX);
                using var barBgBrush = new SolidBrush(ScoreBarBg);
                GraphicsExtensions.FillRoundedRect(g, barBgBrush,
                    new RectangleF(textX, barY, barW, barH), 3);

                float fillW = barW * Math.Clamp(_score, 0f, 1f);
                if (fillW > 2)
                {
                    using var barFillBrush = new SolidBrush(
                        _checked ? AccentCyan : _maskColor);
                    GraphicsExtensions.FillRoundedRect(g, barFillBrush,
                        new RectangleF(textX, barY, fillW, barH), 3);
                }
                if (_checked)
                {
                    using var checkBrush = new SolidBrush(AccentCyan);
                    g.FillEllipse(checkBrush, indX, indY, indSize, indSize);
                    using var tickPen = new Pen(Color.FromArgb(13, 13, 13), 2f);
                    g.DrawLine(tickPen, indX + 3f, indY + 7f, indX + 5.5f, indY + 10.5f);
                    g.DrawLine(tickPen, indX + 5.5f, indY + 10.5f, indX + 11f, indY + 4f);
                }
                else
                {
                    using var emptyPen = new Pen(DimText, 1.4f);
                    g.DrawEllipse(emptyPen, indX, indY, indSize, indSize);
                }
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
