using VisionEditCV.Models;
using VisionEditCV.Processing;

namespace VisionEditCV.Controls
{
    public enum CanvasMode { None, BoundingBox, Prompt }

    public class MaskSelectedEventArgs : EventArgs
    {
        public int MaskIndex { get; }
        public bool Selected { get; }
        public MaskSelectedEventArgs(int idx, bool sel) { MaskIndex = idx; Selected = sel; }
    }

    /// <summary>Stores one bounding box and its foreground/background label.</summary>
    public class BBoxEntry
    {
        public RectangleF Rect  { get; set; }
        /// <summary>True = foreground (keep), False = background (exclude).</summary>
        public bool       Label { get; set; } = true;

        public BBoxEntry(RectangleF rect, bool label = true)
        {
            Rect  = rect;
            Label = label;
        }
    }

    /// <summary>
    /// Custom double-buffered control that:
    ///  - Renders the image letter-boxed (aspect-ratio preserving)
    ///  - Supports drawing MULTIPLE bounding boxes, each with a foreground/background label
    ///    · Left-click drag = draw new box (foreground by default)
    ///    · Left-click + drag handle = resize selected box
    ///    · Left-click inside box body = move selected box
    ///    · Right-click inside box = toggle its label (foreground ↔ background)
    ///    · Delete / Backspace = delete selected box
    ///  - Overlays semi-transparent coloured masks
    ///  - Allows clicking on a mask region to select/deselect it
    /// </summary>
    [System.ComponentModel.DesignerCategory("Component")]
    public class ImageCanvas : Control
    {
        // ── Theme colours ────────────────────────────────────────────────────
        private static readonly Color CyanAccent = Color.FromArgb(0, 229, 255);
        private static readonly Color RedAccent  = Color.FromArgb(255, 80,  60);
        private static readonly Color PanelBg    = Color.FromArgb(13, 13, 13);

        // ── Image state ──────────────────────────────────────────────────────
        private Bitmap? _originalBitmap;
        private Bitmap? _processedBitmap;
        private Bitmap? _displayBitmap;
        private bool    _showingOriginal = false;

        // ── Masks ────────────────────────────────────────────────────────────
        private List<float[,]> _masks       = new();
        private List<Color>    _maskColors  = new();
        private List<bool>     _maskSelected = new();
        private List<float>    _maskScores  = new();

        // ── Zoom & Pan ─────────────────────────────────────────────────────
        private float  _zoom = 1.0f;
        private PointF _panOffset = PointF.Empty;
        private bool   _panning = false;
        private PointF _panStart;
        private PointF _panOffsetStart;

        // ── Bounding boxes ───────────────────────────────────────────────────
        private readonly List<BBoxEntry> _boxes = new();

        // Which box index is currently selected (-1 = none)
        private int _selectedBox = -1;

        // Drawing a brand-new box
        private bool   _drawingNew = false;
        private PointF _drawStart;

        // Dragging a resize handle on the selected box
        private int _dragHandle = -1;   // 0-7 = corner/mid handles

        // Moving the selected box body
        private bool      _movingBox = false;
        private PointF    _moveStart;
        private RectangleF _boxAtMoveStart;

        [System.ComponentModel.Browsable(true)]
        [System.ComponentModel.DefaultValue(CanvasMode.None)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Visible)]
        public CanvasMode Mode { get; set; } = CanvasMode.None;

        // ── Events ───────────────────────────────────────────────────────────
        public event EventHandler<MaskSelectedEventArgs>? MaskSelectionChanged;
        public event EventHandler<RectangleF>?            BBoxChanged;
        public event EventHandler?                        ImageDropped;

        // ── Public accessors ─────────────────────────────────────────────────
        public Bitmap?             OriginalBitmap    => _originalBitmap;
        /// <summary>All boxes in image-space coordinates.</summary>
        public IReadOnlyList<BBoxEntry> BBoxEntries  => _boxes;
        /// <summary>Legacy: first box rect (for single-box callers).</summary>
        public RectangleF BBoxInImageSpace            => _boxes.Count > 0 ? _boxes[0].Rect : RectangleF.Empty;
        public IReadOnlyList<bool>  MaskSelected     => _maskSelected;
        public List<Color>          MaskColors       => _maskColors;
        public List<float[,]>       Masks            => _masks;
        public List<float>          MaskScores       => _maskScores;

        // ── Constructor ──────────────────────────────────────────────────────

        public ImageCanvas()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer |
                     ControlStyles.AllPaintingInWmPaint  |
                     ControlStyles.UserPaint             |
                     ControlStyles.ResizeRedraw, true);
            BackColor  = PanelBg;
            AllowDrop  = true;
            DragEnter += OnDragEnter;
            DragDrop  += OnDragDrop;
        }

        // ── Public API ────────────────────────────────────────────────────────

        public void LoadImage(string path)
        {
            _originalBitmap?.Dispose();
            _processedBitmap?.Dispose();
            _displayBitmap?.Dispose();

            _originalBitmap  = new Bitmap(path);
            _processedBitmap = null;
            _displayBitmap   = null;

            ClearMasks();
            ClearBoxes();
            ResetZoom();
            Invalidate();
        }

        public void SetProcessedBitmap(Bitmap? bmp)
        {
            _processedBitmap?.Dispose();
            _processedBitmap  = bmp != null ? (Bitmap)bmp.Clone() : null;
            _showingOriginal  = false;
            RefreshDisplay();
        }

        /// <summary>
        /// Commits the given bitmap as the new OriginalBitmap so that effects
        /// can be chained.  The processed bitmap is cleared.
        /// </summary>
        public void CommitProcessedAsOriginal(Bitmap committed)
        {
            _originalBitmap?.Dispose();
            _originalBitmap = (Bitmap)committed.Clone();
            _processedBitmap?.Dispose();
            _processedBitmap = null;
            _showingOriginal = false;
            RefreshDisplay();
        }

        public void ShowOriginal(bool show)
        {
            _showingOriginal = show;
            RefreshDisplay();
        }

        public void SetMasks(SegmentationResult result)
        {
            ClearMasks();
            var rng = new Random(42);
            for (int i = 0; i < result.Masks.Count; i++)
            {
                _masks.Add(result.Masks[i]);
                _maskColors.Add(Color.FromArgb(
                    rng.Next(80, 255), rng.Next(80, 255), rng.Next(80, 255)));
                _maskSelected.Add(false);
                _maskScores.Add(i < result.Scores.Count ? result.Scores[i] : 0f);
            }
            RefreshDisplay();
        }

        public void SetMaskSelected(int index, bool selected)
        {
            if (index < 0 || index >= _maskSelected.Count) return;
            _maskSelected[index] = selected;
            RefreshDisplay();
        }

        public void ClearMasks()
        {
            _masks.Clear();
            _maskColors.Clear();
            _maskSelected.Clear();
            _maskScores.Clear();
            _processedBitmap?.Dispose();
            _processedBitmap = null;
            _displayBitmap?.Dispose();
            _displayBitmap   = null;
        }

        public void ClearBoxes()
        {
            _boxes.Clear();
            _selectedBox = -1;
            Invalidate();
        }

        public void ResetZoom()
        {
            _zoom = 1.0f;
            _panOffset = PointF.Empty;
            Invalidate();
        }

        // ── Display refresh ───────────────────────────────────────────────────

        private void RefreshDisplay()
        {
            _displayBitmap?.Dispose();
            _displayBitmap = null;

            Bitmap? base_ = _showingOriginal
                ? _originalBitmap
                : (_processedBitmap ?? _originalBitmap);

            if (base_ == null) { Invalidate(); return; }

            if (_masks.Count > 0)
            {
                _displayBitmap = ImageEffects.RenderMaskOverlays(
                    base_, _masks, _maskColors, _maskSelected, _maskScores);
            }
            else
            {
                _displayBitmap = (Bitmap)base_.Clone();
            }

            Invalidate();
        }

        // ── Paint ─────────────────────────────────────────────────────────────

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            var g = e.Graphics;
            g.Clear(PanelBg);

            if (_displayBitmap == null && _originalBitmap == null)
            {
                DrawPlaceholder(g);
                return;
            }

            Bitmap? src = _displayBitmap ?? _originalBitmap;
            if (src == null) return;

            Rectangle dest = GetLetterboxRect(src.Width, src.Height);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            g.DrawImage(src, dest);

            // Draw all boxes
            if (Mode == CanvasMode.BoundingBox)
            {
                for (int i = 0; i < _boxes.Count; i++)
                    DrawBox(g, dest, _boxes[i], i == _selectedBox);
            }
        }

        private void DrawPlaceholder(Graphics g)
        {
            using var pen   = new Pen(Color.FromArgb(60, CyanAccent), 2);
            pen.DashStyle   = System.Drawing.Drawing2D.DashStyle.Dash;
            g.DrawRectangle(pen, 20, 20, Width - 41, Height - 41);

            using var brush = new SolidBrush(Color.FromArgb(80, CyanAccent));
            using var font  = new Font("Segoe UI", 13, FontStyle.Regular);
            string msg      = "Drag & Drop or click to upload an image";
            SizeF  sz       = g.MeasureString(msg, font);
            g.DrawString(msg, font, brush, (Width - sz.Width) / 2, (Height - sz.Height) / 2);
        }

        /// <summary>
        /// Draws a single bounding box.
        /// Foreground boxes = cyan; background boxes = red/orange.
        /// The selected box gets thicker lines + filled handles.
        /// A small label badge ("FG" / "BG") is shown in the top-left corner.
        /// </summary>
        private void DrawBox(Graphics g, Rectangle imgRect, BBoxEntry entry, bool selected)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            Color boxColor = entry.Label ? CyanAccent : RedAccent;
            float lineW    = selected ? 2.5f : 1.5f;

            RectangleF sr = ImageToScreen(entry.Rect, imgRect);

            using var pen = new Pen(boxColor, lineW);
            if (!selected) pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
            g.DrawRectangle(pen, sr.X, sr.Y, sr.Width, sr.Height);

            // Resize handles — only on the selected box
            if (selected)
            {
                var handles = GetHandlePoints(sr);
                using var hBrush = new SolidBrush(boxColor);
                using var hPen   = new Pen(Color.Black, 1);
                foreach (var hp in handles)
                {
                    var hr = new RectangleF(hp.X - 5, hp.Y - 5, 10, 10);
                    g.FillRectangle(hBrush, hr);
                    g.DrawRectangle(hPen, hr.X, hr.Y, hr.Width, hr.Height);
                }
            }

            // Label badge in the top-left of the box
            string badge     = entry.Label ? "FG" : "BG";
            using var badgeFont  = new Font("Segoe UI", 7.5f, FontStyle.Bold);
            SizeF     badgeSz    = g.MeasureString(badge, badgeFont);
            float     bx         = sr.X + 3;
            float     by         = sr.Y + 2;
            using var badgeBg    = new SolidBrush(Color.FromArgb(180, entry.Label
                                        ? Color.FromArgb(0, 60, 70)
                                        : Color.FromArgb(70, 20, 10)));
            g.FillRectangle(badgeBg, bx - 1, by - 1, badgeSz.Width + 2, badgeSz.Height + 1);
            using var badgeText  = new SolidBrush(boxColor);
            g.DrawString(badge, badgeFont, badgeText, bx, by);
        }

        // ── Coordinate mapping ────────────────────────────────────────────────

        private Rectangle GetLetterboxRect(int imgW, int imgH)
        {
            float scaleX = (float)Width  / imgW;
            float scaleY = (float)Height / imgH;
            float scale  = Math.Min(scaleX, scaleY) * _zoom;
            int   dw     = (int)(imgW * scale);
            int   dh     = (int)(imgH * scale);
            int   ox     = (int)((Width  - dw) / 2f + _panOffset.X);
            int   oy     = (int)((Height - dh) / 2f + _panOffset.Y);
            return new Rectangle(ox, oy, dw, dh);
        }

        private PointF ScreenToImage(PointF screen, Rectangle imgRect)
        {
            float sx = (screen.X - imgRect.X) / (float)imgRect.Width;
            float sy = (screen.Y - imgRect.Y) / (float)imgRect.Height;
            return new PointF(
                sx * (_originalBitmap?.Width  ?? imgRect.Width),
                sy * (_originalBitmap?.Height ?? imgRect.Height));
        }

        private RectangleF ImageToScreen(RectangleF imgRect, Rectangle destRect)
        {
            float iw = _originalBitmap?.Width  ?? destRect.Width;
            float ih = _originalBitmap?.Height ?? destRect.Height;
            float x  = destRect.X + imgRect.X      / iw * destRect.Width;
            float y  = destRect.Y + imgRect.Y      / ih * destRect.Height;
            float w  = imgRect.Width               / iw * destRect.Width;
            float h  = imgRect.Height              / ih * destRect.Height;
            return new RectangleF(x, y, w, h);
        }

        private static PointF[] GetHandlePoints(RectangleF r)
        {
            float mx = r.X + r.Width  / 2;
            float my = r.Y + r.Height / 2;
            return new[]
            {
                new PointF(r.Left,  r.Top),     // 0 TL
                new PointF(mx,      r.Top),     // 1 TM
                new PointF(r.Right, r.Top),     // 2 TR
                new PointF(r.Right, my),        // 3 MR
                new PointF(r.Right, r.Bottom),  // 4 BR
                new PointF(mx,      r.Bottom),  // 5 BM
                new PointF(r.Left,  r.Bottom),  // 6 BL
                new PointF(r.Left,  my),        // 7 ML
            };
        }

        private int HitTestHandle(PointF screen, Rectangle imgRect)
        {
            if (_selectedBox < 0 || _selectedBox >= _boxes.Count) return -1;
            RectangleF sr      = ImageToScreen(_boxes[_selectedBox].Rect, imgRect);
            var        handles = GetHandlePoints(sr);
            for (int i = 0; i < handles.Length; i++)
            {
                float dx = screen.X - handles[i].X;
                float dy = screen.Y - handles[i].Y;
                if (Math.Sqrt(dx * dx + dy * dy) < 8) return i;
            }
            return -1;
        }

        /// <summary>Returns the index of the topmost box whose screen rect contains the point, or -1.</summary>
        private int HitTestBox(PointF screen, Rectangle imgRect)
        {
            for (int i = _boxes.Count - 1; i >= 0; i--)
            {
                RectangleF sr = ImageToScreen(_boxes[i].Rect, imgRect);
                if (sr.Contains(screen)) return i;
            }
            return -1;
        }

        // ── Zoom & Pan mouse handling ───────────────────────────────────────

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_originalBitmap == null) return;

            float oldZoom = _zoom;
            float factor  = e.Delta > 0 ? 1.15f : 1f / 1.15f;
            _zoom = Math.Clamp(_zoom * factor, 0.5f, 10f);

            // Keep the point under the cursor stationary
            float ratio = _zoom / oldZoom;
            _panOffset = new PointF(
                e.X - ratio * (e.X - _panOffset.X - Width  / 2f) - Width  / 2f,
                e.Y - ratio * (e.Y - _panOffset.Y - Height / 2f) - Height / 2f);

            Invalidate();
        }

        // ── Mouse ─────────────────────────────────────────────────────────────

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_originalBitmap == null) return;

            // ── Middle-click pan ──
            if (e.Button == MouseButtons.Middle)
            {
                _panning = true;
                _panStart = e.Location;
                _panOffsetStart = _panOffset;
                Capture = true;
                Cursor = Cursors.Hand;
                return;
            }

            Rectangle imgRect = GetLetterboxRect(_originalBitmap.Width, _originalBitmap.Height);

            // ── Mask click (works in any mode when masks are present) ──
            if (e.Button == MouseButtons.Left && _masks.Count > 0)
            {
                PointF imgPt       = ScreenToImage(e.Location, imgRect);
                int    clickedMask = HitTestMask(imgPt);
                if (clickedMask >= 0)
                {
                    bool newState = !_maskSelected[clickedMask];
                    _maskSelected[clickedMask] = newState;
                    MaskSelectionChanged?.Invoke(this,
                        new MaskSelectedEventArgs(clickedMask, newState));
                    RefreshDisplay();
                    return;
                }
            }

            if (Mode == CanvasMode.BoundingBox)
            {
                if (e.Button == MouseButtons.Right)
                {
                    // Right-click: toggle label of the clicked box
                    int hit = HitTestBox(e.Location, imgRect);
                    if (hit >= 0)
                    {
                        _boxes[hit].Label = !_boxes[hit].Label;
                        _selectedBox = hit;
                        Invalidate();
                        BBoxChanged?.Invoke(this, _boxes[hit].Rect);
                    }
                    return;
                }

                if (e.Button == MouseButtons.Left)
                {
                    // 1. Try handle on selected box
                    int handle = HitTestHandle(e.Location, imgRect);
                    if (handle >= 0)
                    {
                        _dragHandle = handle;
                        Capture = true;
                        return;
                    }

                    // 2. Try body of any box → select and start move
                    int hit = HitTestBox(e.Location, imgRect);
                    if (hit >= 0)
                    {
                        _selectedBox    = hit;
                        _movingBox      = true;
                        _moveStart      = e.Location;
                        _boxAtMoveStart = _boxes[hit].Rect;
                        Capture = true;
                        Invalidate();
                        return;
                    }

                    // 3. Start drawing a new box
                    _drawingNew  = true;
                    _drawStart   = ScreenToImage(e.Location, imgRect);
                    _selectedBox = -1;
                    Capture = true;
                }
            }
        }

        protected override void OnMouseMove(MouseEventArgs e)
        {
            base.OnMouseMove(e);
            if (_originalBitmap == null) return;

            // ── Middle-click pan ──
            if (_panning && (e.Button & MouseButtons.Middle) != 0)
            {
                _panOffset = new PointF(
                    _panOffsetStart.X + (e.X - _panStart.X),
                    _panOffsetStart.Y + (e.Y - _panStart.Y));
                Invalidate();
                return;
            }

            Rectangle imgRect = GetLetterboxRect(_originalBitmap.Width, _originalBitmap.Height);

            // Drawing new box
            if (_drawingNew && (e.Button & MouseButtons.Left) != 0)
            {
                PointF cur = ScreenToImage(e.Location, imgRect);
                float  x   = Math.Min(_drawStart.X, cur.X);
                float  y   = Math.Min(_drawStart.Y, cur.Y);
                float  w   = Math.Abs(cur.X - _drawStart.X);
                float  h   = Math.Abs(cur.Y - _drawStart.Y);

                // Show live preview by updating (or adding) a temporary entry
                if (_selectedBox >= 0 && _selectedBox < _boxes.Count)
                    _boxes[_selectedBox] = new BBoxEntry(new RectangleF(x, y, w, h), true);
                else
                {
                    _boxes.Add(new BBoxEntry(new RectangleF(x, y, w, h), true));
                    _selectedBox = _boxes.Count - 1;
                }
                Invalidate();
                return;
            }

            // Resize handle drag
            if (_dragHandle >= 0 && _selectedBox >= 0 && (e.Button & MouseButtons.Left) != 0)
            {
                PointF  imgPt  = ScreenToImage(e.Location, imgRect);
                var     entry  = _boxes[_selectedBox];
                entry.Rect     = UpdateRectForHandle(entry.Rect, _dragHandle, imgPt);
                Invalidate();
                return;
            }

            // Body move
            if (_movingBox && _selectedBox >= 0 && (e.Button & MouseButtons.Left) != 0)
            {
                float iw = _originalBitmap.Width;
                float ih = _originalBitmap.Height;
                float dx = (e.X - _moveStart.X) / imgRect.Width  * iw;
                float dy = (e.Y - _moveStart.Y) / imgRect.Height * ih;
                _boxes[_selectedBox].Rect = new RectangleF(
                    _boxAtMoveStart.X + dx, _boxAtMoveStart.Y + dy,
                    _boxAtMoveStart.Width,  _boxAtMoveStart.Height);
                Invalidate();
                return;
            }

            // Cursor feedback
            if (Mode == CanvasMode.BoundingBox)
            {
                int handle = HitTestHandle(e.Location, imgRect);
                if (handle >= 0)
                    Cursor = GetCursorForHandle(handle);
                else if (HitTestBox(e.Location, imgRect) >= 0)
                    Cursor = Cursors.SizeAll;
                else
                    Cursor = Cursors.Cross;
            }
            else
                Cursor = Cursors.Default;
        }

        protected override void OnMouseUp(MouseEventArgs e)
        {
            base.OnMouseUp(e);
            Capture = false;

            if (_panning) { _panning = false; Cursor = Cursors.Default; return; }

            if (_originalBitmap == null) { _drawingNew = false; _dragHandle = -1; _movingBox = false; return; }

            Rectangle imgRect = GetLetterboxRect(_originalBitmap.Width, _originalBitmap.Height);

            if (_drawingNew)
            {
                _drawingNew = false;
                if (_selectedBox >= 0 && _selectedBox < _boxes.Count)
                {
                    var clamped = ClampToImage(_boxes[_selectedBox].Rect);
                    if (clamped.Width < 2 || clamped.Height < 2)
                    {
                        // Too small — discard
                        _boxes.RemoveAt(_selectedBox);
                        _selectedBox = _boxes.Count - 1;
                    }
                    else
                    {
                        _boxes[_selectedBox].Rect = clamped;
                        BBoxChanged?.Invoke(this, clamped);
                    }
                }
                Invalidate();
                return;
            }

            if (_dragHandle >= 0)
            {
                _dragHandle = -1;
                if (_selectedBox >= 0 && _selectedBox < _boxes.Count)
                {
                    _boxes[_selectedBox].Rect = ClampToImage(_boxes[_selectedBox].Rect);
                    BBoxChanged?.Invoke(this, _boxes[_selectedBox].Rect);
                }
                Invalidate();
                return;
            }

            if (_movingBox)
            {
                _movingBox = false;
                if (_selectedBox >= 0 && _selectedBox < _boxes.Count)
                {
                    _boxes[_selectedBox].Rect = ClampToImage(_boxes[_selectedBox].Rect);
                    BBoxChanged?.Invoke(this, _boxes[_selectedBox].Rect);
                }
                Invalidate();
            }
        }

        protected override void OnKeyDown(KeyEventArgs e)
        {
            base.OnKeyDown(e);

            // Ctrl+0: reset zoom/pan
            if (e.Control && e.KeyCode == Keys.D0)
            {
                ResetZoom();
                e.Handled = true;
                return;
            }

            if (Mode != CanvasMode.BoundingBox) return;

            if ((e.KeyCode == Keys.Delete || e.KeyCode == Keys.Back)
                && _selectedBox >= 0 && _selectedBox < _boxes.Count)
            {
                _boxes.RemoveAt(_selectedBox);
                _selectedBox = _boxes.Count > 0 ? _boxes.Count - 1 : -1;
                Invalidate();
                e.Handled = true;
            }
        }

        protected override bool IsInputKey(Keys keyData)
        {
            if (keyData == Keys.Delete || keyData == Keys.Back) return true;
            return base.IsInputKey(keyData);
        }

        protected override void OnClick(EventArgs e)
        {
            base.OnClick(e);
            if (_originalBitmap == null)
                ImageDropped?.Invoke(this, EventArgs.Empty);
        }

        // ── Handle resize helpers ─────────────────────────────────────────────

        private static RectangleF UpdateRectForHandle(RectangleF r, int handle, PointF imgPt)
        {
            float l = r.Left, ri = r.Right, t = r.Top, b = r.Bottom;
            switch (handle)
            {
                case 0: l  = imgPt.X; t = imgPt.Y; break; // TL
                case 1: t  = imgPt.Y; break;               // TM
                case 2: ri = imgPt.X; t = imgPt.Y; break; // TR
                case 3: ri = imgPt.X; break;               // MR
                case 4: ri = imgPt.X; b = imgPt.Y; break; // BR
                case 5: b  = imgPt.Y; break;               // BM
                case 6: l  = imgPt.X; b = imgPt.Y; break; // BL
                case 7: l  = imgPt.X; break;               // ML
            }
            if (ri < l) (l, ri) = (ri, l);
            if (b  < t) (t, b)  = (b, t);
            return RectangleF.FromLTRB(l, t, ri, b);
        }

        private RectangleF ClampToImage(RectangleF rect)
        {
            if (_originalBitmap == null) return rect;
            float l = Math.Max(0,                         rect.Left);
            float t = Math.Max(0,                         rect.Top);
            float r = Math.Min(_originalBitmap.Width,     rect.Right);
            float b = Math.Min(_originalBitmap.Height,    rect.Bottom);
            return RectangleF.FromLTRB(l, t, r, b);
        }

        private static Cursor GetCursorForHandle(int handle) => handle switch
        {
            0 or 4 => Cursors.SizeNWSE,
            2 or 6 => Cursors.SizeNESW,
            1 or 5 => Cursors.SizeNS,
            3 or 7 => Cursors.SizeWE,
            _      => Cursors.Default
        };

        // ── Mask hit testing ──────────────────────────────────────────────────

        private int HitTestMask(PointF imgPt)
        {
            if (_originalBitmap == null) return -1;
            int px = (int)imgPt.X;
            int py = (int)imgPt.Y;
            for (int i = _masks.Count - 1; i >= 0; i--)
            {
                var m  = _masks[i];
                int mH = m.GetLength(0);
                int mW = m.GetLength(1);
                int mx = (int)(px / (float)_originalBitmap.Width  * mW);
                int my = (int)(py / (float)_originalBitmap.Height * mH);
                if (mx >= 0 && mx < mW && my >= 0 && my < mH && m[my, mx] > 0.5f)
                    return i;
            }
            return -1;
        }

        // ── Drag & Drop ───────────────────────────────────────────────────────

        private void OnDragEnter(object? sender, DragEventArgs e)
        {
            if (e.Data?.GetDataPresent(DataFormats.FileDrop) == true)
                e.Effect = DragDropEffects.Copy;
        }

        private void OnDragDrop(object? sender, DragEventArgs e)
        {
            var files = (string[]?)e.Data?.GetData(DataFormats.FileDrop);
            if (files?.Length > 0)
            {
                string ext = Path.GetExtension(files[0]).ToLowerInvariant();
                if (ext is ".jpg" or ".jpeg" or ".png" or ".bmp" or ".webp")
                {
                    LoadImage(files[0]);
                    ImageDropped?.Invoke(this, EventArgs.Empty);
                }
            }
        }
    }
}
