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

    
    public class BBoxEntry
    {
        public RectangleF Rect  { get; set; }
        
        public bool       Label { get; set; } = true;

        public BBoxEntry(RectangleF rect, bool label = true)
        {
            Rect  = rect;
            Label = label;
        }
    }

    
    
    
    
    
    
    
    
    
    
    
    
    [System.ComponentModel.DesignerCategory("Component")]
    public class ImageCanvas : Control
    {
        
        private static readonly Color CyanAccent = Color.FromArgb(0, 229, 255);
        private static readonly Color RedAccent  = Color.FromArgb(255, 80,  60);
        private static readonly Color PanelBg    = Color.FromArgb(13, 13, 13);

        
        private Bitmap? _originalBitmap;
        private Bitmap? _fileOriginalBitmap; 
        private Bitmap? _processedBitmap;
        private Bitmap? _displayBitmap;
        private bool    _showingOriginal = false;

        
        private List<float[,]> _masks        = new();
        private List<float[,]> _originalMasks = new(); 
        private List<Color>    _maskColors   = new();
        private List<bool>     _maskSelected  = new();
        private List<float>    _maskScores   = new();

        
        
        private List<float[,]>? _displayMasksOverride = null;

        
        private float  _zoom = 1.0f;
        private PointF _panOffset = PointF.Empty;
        private bool   _panning = false;
        private PointF _panStart;
        private PointF _panOffsetStart;

        
        private readonly List<BBoxEntry> _boxes = new();

        
        private int _selectedBox = -1;

        
        private bool   _drawingNew = false;
        private PointF _drawStart;

        
        private int _dragHandle = -1;   

        
        private bool      _movingBox = false;
        private PointF    _moveStart;
        private RectangleF _boxAtMoveStart;

        [System.ComponentModel.Browsable(true)]
        [System.ComponentModel.DefaultValue(CanvasMode.None)]
        [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Visible)]
        public CanvasMode Mode { get; set; } = CanvasMode.None;

        
        public event EventHandler<MaskSelectedEventArgs>? MaskSelectionChanged;
        public event EventHandler<RectangleF>?            BBoxChanged;
        public event EventHandler?                        ImageDropped;

        
        public Bitmap?             OriginalBitmap    => _originalBitmap;
        
        public IReadOnlyList<BBoxEntry> BBoxEntries  => _boxes;
        
        public RectangleF BBoxInImageSpace            => _boxes.Count > 0 ? _boxes[0].Rect : RectangleF.Empty;
        public IReadOnlyList<bool>  MaskSelected     => _maskSelected;
        public List<Color>          MaskColors       => _maskColors;
        public List<float[,]>       Masks            => _masks;
        public bool                 HideMasks        { get; private set; } = false;
        public List<float>          MaskScores       => _maskScores;

        public void SetHideMasks(bool hide)
        {
            HideMasks = hide;
            RefreshDisplay();
        }

        

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

        

        public void LoadImage(string path)
        {
            _originalBitmap?.Dispose();
            _fileOriginalBitmap?.Dispose();
            _processedBitmap?.Dispose();
            _displayBitmap?.Dispose();

            _originalBitmap     = new Bitmap(path);
            _fileOriginalBitmap = new Bitmap(path); 
            _processedBitmap    = null;
            _displayBitmap      = null;

            ClearMasks();
            ClearBoxes();
            ResetZoom();
            Invalidate();
        }

        
        
        
        
        
        public void RestoreOriginalFromFile(string path)
        {
            _originalBitmap?.Dispose();
            _processedBitmap?.Dispose();
            _displayBitmap?.Dispose();

            _originalBitmap  = new Bitmap(path);
            _processedBitmap = null;
            _displayBitmap   = null;

            Invalidate();
        }

        public void SetProcessedBitmap(Bitmap? bmp)
        {
            _processedBitmap?.Dispose();
            _processedBitmap  = bmp != null ? (Bitmap)bmp.Clone() : null;
            _showingOriginal  = false;
            RefreshDisplay();
        }

        
        
        
        
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
                
                var orig = new float[result.Masks[i].GetLength(0), result.Masks[i].GetLength(1)];
                Array.Copy(result.Masks[i], orig, result.Masks[i].Length);
                _originalMasks.Add(orig);
                _maskColors.Add(Color.FromArgb(
                    rng.Next(80, 255), rng.Next(80, 255), rng.Next(80, 255)));
                _maskSelected.Add(false);
                _maskScores.Add(i < result.Scores.Count ? result.Scores[i] : 0f);
            }
            RefreshDisplay();
        }

        
        
        
        
        
        
        public void ReplaceMasks(List<float[,]> newMasks)
        {
            if (newMasks.Count != _masks.Count) return;
            for (int i = 0; i < _masks.Count; i++)
                _masks[i] = newMasks[i];
            _displayMasksOverride = null;
            RefreshDisplay();
        }

        public void SetMaskSelected(int index, bool selected)
        {
            if (index < 0 || index >= _maskSelected.Count) return;
            _maskSelected[index] = selected;
            RefreshDisplay();
        }

        
        
        
        
        
        public void SetDisplayMaskOverride(List<float[,]>? overrideMasks)
        {
            _displayMasksOverride = overrideMasks;
            RefreshDisplay();
        }

        public void ClearMasks()
        {
            _masks.Clear();
            _originalMasks.Clear();
            _maskColors.Clear();
            _maskSelected.Clear();
            _maskScores.Clear();
            _displayMasksOverride = null;
            _processedBitmap?.Dispose();
            _processedBitmap = null;
            _displayBitmap?.Dispose();
            _displayBitmap   = null;
        }

        
        
        
        
        public void RestoreOriginalMasks()
        {
            if (_originalMasks.Count != _masks.Count) return;
            for (int i = 0; i < _masks.Count; i++)
            {
                var orig = new float[_originalMasks[i].GetLength(0), _originalMasks[i].GetLength(1)];
                Array.Copy(_originalMasks[i], orig, _originalMasks[i].Length);
                _masks[i] = orig;
            }
            _displayMasksOverride = null;
            RefreshDisplay();
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

        

        private void RefreshDisplay()
        {
            _displayBitmap?.Dispose();
            _displayBitmap = null;

            Bitmap? base_ = _showingOriginal
                ? (_fileOriginalBitmap ?? _originalBitmap)
                : (_processedBitmap ?? _originalBitmap);

            if (base_ == null) { Invalidate(); return; }

            if (_masks.Count > 0 && !HideMasks)
            {
                var renderMasks = _displayMasksOverride ?? _masks;
                _displayBitmap = ImageEffects.RenderMaskOverlays(
                    base_, renderMasks, _maskColors, _maskSelected, _maskScores);
            }
            else
            {
                _displayBitmap = (Bitmap)base_.Clone();
            }

            Invalidate();
        }

        

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

        
        
        
        
        
        
        private void DrawBox(Graphics g, Rectangle imgRect, BBoxEntry entry, bool selected)
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

            Color boxColor = entry.Label ? CyanAccent : RedAccent;
            float lineW    = selected ? 2.5f : 1.5f;

            RectangleF sr = ImageToScreen(entry.Rect, imgRect);

            using var pen = new Pen(boxColor, lineW);
            if (!selected) pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
            g.DrawRectangle(pen, sr.X, sr.Y, sr.Width, sr.Height);

            
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
                new PointF(r.Left,  r.Top),     
                new PointF(mx,      r.Top),     
                new PointF(r.Right, r.Top),     
                new PointF(r.Right, my),        
                new PointF(r.Right, r.Bottom),  
                new PointF(mx,      r.Bottom),  
                new PointF(r.Left,  r.Bottom),  
                new PointF(r.Left,  my),        
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

        
        private int HitTestBox(PointF screen, Rectangle imgRect)
        {
            for (int i = _boxes.Count - 1; i >= 0; i--)
            {
                RectangleF sr = ImageToScreen(_boxes[i].Rect, imgRect);
                if (sr.Contains(screen)) return i;
            }
            return -1;
        }

        

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            if (_originalBitmap == null) return;

            float oldZoom = _zoom;
            float factor  = e.Delta > 0 ? 1.15f : 1f / 1.15f;
            _zoom = Math.Clamp(_zoom * factor, 0.5f, 10f);

            
            float ratio = _zoom / oldZoom;
            _panOffset = new PointF(
                e.X - ratio * (e.X - _panOffset.X - Width  / 2f) - Width  / 2f,
                e.Y - ratio * (e.Y - _panOffset.Y - Height / 2f) - Height / 2f);

            Invalidate();
        }

        

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            if (_originalBitmap == null) return;

            
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
                    
                    int handle = HitTestHandle(e.Location, imgRect);
                    if (handle >= 0)
                    {
                        _dragHandle = handle;
                        Capture = true;
                        return;
                    }

                    
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

            
            if (_panning && (e.Button & MouseButtons.Middle) != 0)
            {
                _panOffset = new PointF(
                    _panOffsetStart.X + (e.X - _panStart.X),
                    _panOffsetStart.Y + (e.Y - _panStart.Y));
                Invalidate();
                return;
            }

            Rectangle imgRect = GetLetterboxRect(_originalBitmap.Width, _originalBitmap.Height);

            
            if (_drawingNew && (e.Button & MouseButtons.Left) != 0)
            {
                PointF cur = ScreenToImage(e.Location, imgRect);
                float  x   = Math.Min(_drawStart.X, cur.X);
                float  y   = Math.Min(_drawStart.Y, cur.Y);
                float  w   = Math.Abs(cur.X - _drawStart.X);
                float  h   = Math.Abs(cur.Y - _drawStart.Y);

                
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

            
            if (_dragHandle >= 0 && _selectedBox >= 0 && (e.Button & MouseButtons.Left) != 0)
            {
                PointF  imgPt  = ScreenToImage(e.Location, imgRect);
                var     entry  = _boxes[_selectedBox];
                entry.Rect     = UpdateRectForHandle(entry.Rect, _dragHandle, imgPt);
                Invalidate();
                return;
            }

            
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

        

        private static RectangleF UpdateRectForHandle(RectangleF r, int handle, PointF imgPt)
        {
            float l = r.Left, ri = r.Right, t = r.Top, b = r.Bottom;
            switch (handle)
            {
                case 0: l  = imgPt.X; t = imgPt.Y; break; 
                case 1: t  = imgPt.Y; break;               
                case 2: ri = imgPt.X; t = imgPt.Y; break; 
                case 3: ri = imgPt.X; break;               
                case 4: ri = imgPt.X; b = imgPt.Y; break; 
                case 5: b  = imgPt.Y; break;               
                case 6: l  = imgPt.X; b = imgPt.Y; break; 
                case 7: l  = imgPt.X; break;               
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
