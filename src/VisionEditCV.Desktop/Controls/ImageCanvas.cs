using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using SkiaSharp;
using VisionEditCV.Desktop.Models;
using Avalonia.Skia;
using Avalonia.Rendering.SceneGraph;

namespace VisionEditCV.Desktop.Controls;

public enum CanvasMode { Select, BBox, Pan }

public class ImageCanvas : Control
{
    public static readonly StyledProperty<Bitmap?> OriginalImageProperty =
        AvaloniaProperty.Register<ImageCanvas, Bitmap?>(nameof(OriginalImage));

    public static readonly StyledProperty<Bitmap?> ProcessedImageProperty =
        AvaloniaProperty.Register<ImageCanvas, Bitmap?>(nameof(ProcessedImage));

    public static readonly StyledProperty<CanvasMode> ModeProperty =
        AvaloniaProperty.Register<ImageCanvas, CanvasMode>(nameof(Mode), CanvasMode.Select);

    public static readonly StyledProperty<ObservableCollection<BBoxEntry>> BoxesProperty =
        AvaloniaProperty.Register<ImageCanvas, ObservableCollection<BBoxEntry>>(nameof(Boxes));

    public static readonly StyledProperty<List<float[,]>?> MasksProperty =
        AvaloniaProperty.Register<ImageCanvas, List<float[,]>?>(nameof(Masks));

    public static readonly StyledProperty<bool> IsCompareModeProperty =
        AvaloniaProperty.Register<ImageCanvas, bool>(nameof(IsCompareMode));

    public static readonly StyledProperty<bool> HideMasksProperty =
        AvaloniaProperty.Register<ImageCanvas, bool>(nameof(HideMasks));

    public static readonly StyledProperty<IReadOnlyList<bool>?> MaskVisibilityProperty =
        AvaloniaProperty.Register<ImageCanvas, IReadOnlyList<bool>?>(nameof(MaskVisibility));

    private SKImage? _cachedOriginalSkImage;
    private SKImage? _cachedProcessedSkImage;
    private List<SKImage>? _cachedMaskSkImages;

    static ImageCanvas()
    {
        AffectsRender<ImageCanvas>(ModeProperty, BoxesProperty, IsCompareModeProperty, HideMasksProperty, MaskVisibilityProperty);
        AffectsMeasure<ImageCanvas>(OriginalImageProperty, ProcessedImageProperty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        bool requiresInvalidate = false;

        if (change.Property == OriginalImageProperty)
        {
            _cachedOriginalSkImage?.Dispose();
            _cachedOriginalSkImage = OriginalImage?.ToSKImage();
            requiresInvalidate = true;
        }
        else if (change.Property == ProcessedImageProperty)
        {
            _cachedProcessedSkImage?.Dispose();
            _cachedProcessedSkImage = ProcessedImage?.ToSKImage();
            requiresInvalidate = true;
        }
        else if (change.Property == MasksProperty)
        {
            if (_cachedMaskSkImages != null)
            {
                foreach (var img in _cachedMaskSkImages) img.Dispose();
            }
            _cachedMaskSkImages = null;

            if (Masks != null && Masks.Count > 0)
            {
                _cachedMaskSkImages = new List<SKImage>();
                var colors = new[] { SKColors.Red, SKColors.Orange, SKColors.Yellow, SKColors.Green, SKColors.Blue, SKColors.Purple };
                for (int i = 0; i < Masks.Count; i++)
                {
                    var baseColor = colors[i % colors.Length];
                    var color = new SKColor(baseColor.Red, baseColor.Green, baseColor.Blue, 100);
                    _cachedMaskSkImages.Add(CreateMaskImage(Masks[i], color));
                }
            }
            requiresInvalidate = true;
        }

        if (requiresInvalidate || change.Property == ModeProperty || change.Property == BoxesProperty || 
            change.Property == IsCompareModeProperty || change.Property == HideMasksProperty || change.Property == MaskVisibilityProperty)
        {
            InvalidateVisual();
        }
    }

    private SKImage CreateMaskImage(float[,] mask, SKColor color)
    {
        int h = mask.GetLength(0);
        int w = mask.GetLength(1);

        var bitmap = new SKBitmap(new SKImageInfo(w, h, SKColorType.Bgra8888));
        unsafe
        {
            uint* ptr = (uint*)bitmap.GetPixels().ToPointer();
            uint c = (uint)((color.Alpha << 24) | (color.Red << 16) | (color.Green << 8) | color.Blue);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    *ptr++ = mask[y, x] > 0.5f ? c : 0;
                }
            }
        }
        return SKImage.FromBitmap(bitmap);
    }

    public Bitmap? OriginalImage
    {
        get => GetValue(OriginalImageProperty);
        set => SetValue(OriginalImageProperty, value);
    }

    public Bitmap? ProcessedImage
    {
        get => GetValue(ProcessedImageProperty);
        set => SetValue(ProcessedImageProperty, value);
    }

    public bool IsCompareMode
    {
        get => GetValue(IsCompareModeProperty);
        set => SetValue(IsCompareModeProperty, value);
    }

    public CanvasMode Mode
    {
        get => GetValue(ModeProperty);
        set => SetValue(ModeProperty, value);
    }

    public ObservableCollection<BBoxEntry> Boxes
    {
        get => GetValue(BoxesProperty);
        set => SetValue(BoxesProperty, value);
    }

    public List<float[,]>? Masks
    {
        get => GetValue(MasksProperty);
        set => SetValue(MasksProperty, value);
    }

    public bool HideMasks
    {
        get => GetValue(HideMasksProperty);
        set => SetValue(HideMasksProperty, value);
    }

    public IReadOnlyList<bool>? MaskVisibility
    {
        get => GetValue(MaskVisibilityProperty);
        set => SetValue(MaskVisibilityProperty, value);
    }

    private double _zoom = 1.0;
    private Point _panOffset = new Point(0, 0);
    private Point _lastMousePos;
    private bool _isPanning;
    private bool _isDrawing;
    private bool _isMoving;
    private int _dragHandle = -1;
    private int _selectedBoxIndex = -1;
    private Point _drawStart;
    private Rect _boxAtMoveStart;

    private readonly Dictionary<long, Point> _activePointers = new();
    private bool _isPinching;
    private double _initialPinchDistance;
    private double _initialZoom;
    private Point _initialMidpoint;
    private Point _initialPanOffset;
    private bool _gestureStartedAsSingleFinger;

    private DateTime _lastTapTime = DateTime.MinValue;
    private Point _lastTapPosition;
    private const double DoubleTapTimeThresholdMs = 300;
    private const double DoubleTapDistanceThreshold = 20;

    private const double HandleSize = 8.0;

    public ImageCanvas()
    {
        Boxes = new ObservableCollection<BBoxEntry>();
        ClipToBounds = true;
    }

    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        base.OnPointerWheelChanged(e);
        if (OriginalImage == null) return;

        var delta = e.Delta.Y;
        var oldZoom = _zoom;
        var factor = delta > 0 ? 1.1 : 1.0 / 1.1;
        _zoom = Math.Clamp(_zoom * factor, 0.1, 10.0);

        var mousePos = e.GetPosition(this);
        
        // Stabilize zoom-around-mouse by accounting for the centering component in GetImageRect
        var canvasSize = Bounds.Size;
        var imgSize = OriginalImage.Size;
        var baseScale = Math.Min(canvasSize.Width / imgSize.Width, canvasSize.Height / imgSize.Height);
        
        var oldCenter = new Point((canvasSize.Width - imgSize.Width * baseScale * oldZoom) / 2,
                                  (canvasSize.Height - imgSize.Height * baseScale * oldZoom) / 2);
        
        var newCenter = new Point((canvasSize.Width - imgSize.Width * baseScale * _zoom) / 2,
                                  (canvasSize.Height - imgSize.Height * baseScale * _zoom) / 2);

        var relativeMousePos = (mousePos - oldCenter - _panOffset) / oldZoom;
        _panOffset = mousePos - newCenter - relativeMousePos * _zoom;

        InvalidateVisual();
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        var pointerId = e.Pointer.Id;
        var pos = e.GetPosition(this);
        _activePointers[pointerId] = pos;

        // If two fingers are active, we initialize/update pinch-to-zoom
        if (_activePointers.Count == 2)
        {
            _gestureStartedAsSingleFinger = false;
            
            // Abort single-finger drawing if we were drawing
            if (_isDrawing && _selectedBoxIndex != -1 && _selectedBoxIndex < Boxes.Count)
            {
                Boxes.RemoveAt(_selectedBoxIndex);
                _selectedBoxIndex = -1;
            }
            _isDrawing = false;
            _isPanning = false;
            _isMoving = false;
            _dragHandle = -1;

            Point p1 = default;
            Point p2 = default;
            int index = 0;
            foreach (var kvp in _activePointers)
            {
                if (index == 0) p1 = kvp.Value;
                else if (index == 1) { p2 = kvp.Value; break; }
                index++;
            }

            _initialPinchDistance = Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));
            _initialZoom = _zoom;
            _initialMidpoint = new Point((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2);
            _initialPanOffset = _panOffset;
            _isPinching = true;

            e.Pointer.Capture(this);
            InvalidateVisual();
            return;
        }
        else if (_activePointers.Count > 2)
        {
            // Ignore extra fingers, but keep pinching
            _gestureStartedAsSingleFinger = false;
            e.Pointer.Capture(this);
            return;
        }

        // Single-finger path
        _gestureStartedAsSingleFinger = true;
        
        var properties = e.GetCurrentPoint(this).Properties;

        // Double tap check to toggle box label (foreground/background)
        var now = DateTime.UtcNow;
        var timeDiff = (now - _lastTapTime).TotalMilliseconds;
        var distDiff = Math.Sqrt(Math.Pow(pos.X - _lastTapPosition.X, 2) + Math.Pow(pos.Y - _lastTapPosition.Y, 2));
        if (timeDiff < DoubleTapTimeThresholdMs && distDiff < DoubleTapDistanceThreshold)
        {
            if (OriginalImage != null)
            {
                var doubleTapImgRect = GetImageRect();
                var hitIndex = HitTestBox(pos, doubleTapImgRect);
                if (hitIndex != -1)
                {
                    Boxes[hitIndex].Label = !Boxes[hitIndex].Label;
                    InvalidateVisual();
                }
            }
            _lastTapTime = DateTime.MinValue; // Reset
            return;
        }
        _lastTapTime = now;
        _lastTapPosition = pos;

        if (properties.IsMiddleButtonPressed || (Mode == CanvasMode.Pan && properties.IsLeftButtonPressed))
        {
            _isPanning = true;
            _lastMousePos = pos;
            e.Pointer.Capture(this);
            return;
        }

        if (OriginalImage == null) return;

        var imgRect = GetImageRect();
        var imgPos = ScreenToImage(pos, imgRect);

        if (properties.IsRightButtonPressed)
        {
            var hitIndex = HitTestBox(pos, imgRect);
            if (hitIndex != -1)
            {
                Boxes[hitIndex].Label = !Boxes[hitIndex].Label;
                InvalidateVisual();
            }
            return;
        }

        if (Mode == CanvasMode.BBox && properties.IsLeftButtonPressed)
        {
            _dragHandle = HitTestHandle(pos, imgRect);
            if (_dragHandle != -1)
            {
                e.Pointer.Capture(this);
                return;
            }

            var hitIndex = HitTestBox(pos, imgRect);
            if (hitIndex != -1)
            {
                _selectedBoxIndex = hitIndex;
                _isMoving = true;
                _lastMousePos = pos;
                _boxAtMoveStart = Boxes[hitIndex].Rect;
                e.Pointer.Capture(this);
                InvalidateVisual();
                return;
            }

            _isDrawing = true;
            _drawStart = imgPos;
            var newBox = new BBoxEntry(new Rect(_drawStart, _drawStart));
            Boxes.Add(newBox);
            _selectedBoxIndex = Boxes.Count - 1;
            e.Pointer.Capture(this);
        }
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        var pointerId = e.Pointer.Id;
        var pos = e.GetPosition(this);

        // Update active pointer position if we track it
        if (_activePointers.ContainsKey(pointerId))
        {
            _activePointers[pointerId] = pos;
        }

        if (_activePointers.Count >= 2)
        {
            if (_isPinching)
            {
                Point p1 = default;
                Point p2 = default;
                int index = 0;
                foreach (var kvp in _activePointers)
                {
                    if (index == 0) p1 = kvp.Value;
                    else if (index == 1) { p2 = kvp.Value; break; }
                    index++;
                }

                var currentMidpoint = new Point((p1.X + p2.X) / 2, (p1.Y + p2.Y) / 2);
                var currentDistance = Math.Sqrt(Math.Pow(p1.X - p2.X, 2) + Math.Pow(p1.Y - p2.Y, 2));

                double scaleFactor = 1.0;
                if (_initialPinchDistance > 1.0)
                {
                    scaleFactor = currentDistance / _initialPinchDistance;
                }

                if (OriginalImage != null)
                {
                    var oldZoom = _zoom;
                    _zoom = Math.Clamp(_initialZoom * scaleFactor, 0.1, 10.0);

                    var canvasSize = Bounds.Size;
                    var imgSize = OriginalImage.Size;
                    var baseScale = Math.Min(canvasSize.Width / imgSize.Width, canvasSize.Height / imgSize.Height);

                    var oldCenter = new Point((canvasSize.Width - imgSize.Width * baseScale * oldZoom) / 2,
                                              (canvasSize.Height - imgSize.Height * baseScale * oldZoom) / 2);

                    var newCenter = new Point((canvasSize.Width - imgSize.Width * baseScale * _zoom) / 2,
                                              (canvasSize.Height - imgSize.Height * baseScale * _zoom) / 2);

                    var relativeMidpointPos = (_initialMidpoint - oldCenter - _initialPanOffset) / oldZoom;
                    var zoomPanOffset = _initialMidpoint - newCenter - relativeMidpointPos * _zoom;

                    var dragDelta = currentMidpoint - _initialMidpoint;
                    _panOffset = zoomPanOffset + dragDelta;
                }
                InvalidateVisual();
            }
            return;
        }

        // Single finger move
        if (_gestureStartedAsSingleFinger)
        {
            if (_isPanning)
            {
                var delta = pos - _lastMousePos;
                _panOffset += delta;
                _lastMousePos = pos;
                InvalidateVisual();
                return;
            }

            if (OriginalImage == null) return;
            var imgRect = GetImageRect();

            if (_isDrawing)
            {
                var imgPos = ScreenToImage(pos, imgRect);
                var rect = new Rect(_drawStart, imgPos);
                Boxes[_selectedBoxIndex].Rect = rect;
                InvalidateVisual();
            }
            else if (_dragHandle != -1)
            {
                var imgPos = ScreenToImage(pos, imgRect);
                Boxes[_selectedBoxIndex].Rect = UpdateRectWithHandle(Boxes[_selectedBoxIndex].Rect, _dragHandle, imgPos);
                InvalidateVisual();
            }
            else if (_isMoving)
            {
                var startImgPos = ScreenToImage(_lastMousePos, imgRect);
                var currentImgPos = ScreenToImage(pos, imgRect);
                var imgDelta = currentImgPos - startImgPos;

                Boxes[_selectedBoxIndex].Rect = new Rect(
                    _boxAtMoveStart.Position + imgDelta,
                    _boxAtMoveStart.Size);
                _lastMousePos = pos; // Update last mouse pos to continue delta movement correctly
                _boxAtMoveStart = Boxes[_selectedBoxIndex].Rect; // Update start rect to current
                InvalidateVisual();
            }
        }
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        var pointerId = e.Pointer.Id;
        _activePointers.Remove(pointerId);

        e.Pointer.Capture(null);

        bool wasSingleFinger = _gestureStartedAsSingleFinger;

        if (_activePointers.Count == 0)
        {
            _isPinching = false;
            _gestureStartedAsSingleFinger = false;

            if (wasSingleFinger)
            {
                _isPanning = false;
                _isDrawing = false;
                _isMoving = false;
                _dragHandle = -1;

                if (_selectedBoxIndex != -1 && _selectedBoxIndex < Boxes.Count)
                {
                    // Clamp and validate
                    var box = Boxes[_selectedBoxIndex];
                    var clampedRect = ClampRect(box.Rect, OriginalImage!.Size);
                    if (clampedRect.Width < 1 || clampedRect.Height < 1)
                    {
                        Boxes.RemoveAt(_selectedBoxIndex);
                        _selectedBoxIndex = -1;
                    }
                    else
                    {
                        box.Rect = clampedRect;
                    }
                }
                InvalidateVisual();
            }
        }
        else if (_activePointers.Count == 1)
        {
            _isPinching = false;
            foreach (var kvp in _activePointers)
            {
                _lastMousePos = kvp.Value;
                break;
            }
        }
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        _activePointers.Clear();
        _isPinching = false;
        _isPanning = false;
        _isDrawing = false;
        _isMoving = false;
        _dragHandle = -1;
        _gestureStartedAsSingleFinger = false;
        InvalidateVisual();
    }

    private Rect GetImageRect()
    {
        if (OriginalImage == null) return new Rect();
        var canvasSize = Bounds.Size;
        var imgSize = OriginalImage.Size;

        var scale = Math.Min(canvasSize.Width / imgSize.Width, canvasSize.Height / imgSize.Height) * _zoom;
        var w = imgSize.Width * scale;
        var h = imgSize.Height * scale;

        var x = (canvasSize.Width - w) / 2 + _panOffset.X;
        var y = (canvasSize.Height - h) / 2 + _panOffset.Y;

        return new Rect(x, y, w, h);
    }

    private Point ScreenToImage(Point screenPos, Rect imgRect)
    {
        if (OriginalImage == null) return default;
        var x = (screenPos.X - imgRect.X) / imgRect.Width * OriginalImage.Size.Width;
        var y = (screenPos.Y - imgRect.Y) / imgRect.Height * OriginalImage.Size.Height;
        return new Point(x, y);
    }

    private Rect ImageToScreen(Rect imgRect, Rect displayRect)
    {
        if (OriginalImage == null) return default;
        var x = displayRect.X + imgRect.X / OriginalImage.Size.Width * displayRect.Width;
        var y = displayRect.Y + imgRect.Y / OriginalImage.Size.Height * displayRect.Height;
        var w = imgRect.Width / OriginalImage.Size.Width * displayRect.Width;
        var h = imgRect.Height / OriginalImage.Size.Height * displayRect.Height;
        return new Rect(x, y, w, h);
    }

    private int HitTestBox(Point pos, Rect imgRect)
    {
        for (int i = Boxes.Count - 1; i >= 0; i--)
        {
            var screenRect = ImageToScreen(Boxes[i].Rect, imgRect);
            if (screenRect.Contains(pos)) return i;
        }
        return -1;
    }

    private int HitTestHandle(Point pos, Rect imgRect)
    {
        if (_selectedBoxIndex == -1) return -1;
        var screenRect = ImageToScreen(Boxes[_selectedBoxIndex].Rect, imgRect);
        var handles = GetHandlePoints(screenRect);
        for (int i = 0; i < handles.Length; i++)
        {
            var handleRect = new Rect(handles[i].X - HandleSize / 2, handles[i].Y - HandleSize / 2, HandleSize, HandleSize);
            if (handleRect.Contains(pos)) return i;
        }
        return -1;
    }

    private Point[] GetHandlePoints(Rect r)
    {
        return new[]
        {
            r.TopLeft, r.TopRight, r.BottomLeft, r.BottomRight,
            new Point(r.Left + r.Width/2, r.Top),
            new Point(r.Left + r.Width/2, r.Bottom),
            new Point(r.Left, r.Top + r.Height/2),
            new Point(r.Right, r.Top + r.Height/2)
        };
    }

    private Rect UpdateRectWithHandle(Rect r, int handle, Point imgPos)
    {
        double l = r.Left, t = r.Top, ri = r.Right, b = r.Bottom;
        switch (handle)
        {
            case 0: l = imgPos.X; t = imgPos.Y; break;
            case 1: ri = imgPos.X; t = imgPos.Y; break;
            case 2: l = imgPos.X; b = imgPos.Y; break;
            case 3: ri = imgPos.X; b = imgPos.Y; break;
            case 4: t = imgPos.Y; break;
            case 5: b = imgPos.Y; break;
            case 6: l = imgPos.X; break;
            case 7: ri = imgPos.X; break;
        }
        return new Rect(new Point(Math.Min(l, ri), Math.Min(t, b)), new Point(Math.Max(l, ri), Math.Max(t, b)));
    }

    private Rect ClampRect(Rect r, Size s)
    {
        double l = Math.Clamp(r.Left, 0, s.Width);
        double t = Math.Clamp(r.Top, 0, s.Height);
        double ri = Math.Clamp(r.Right, 0, s.Width);
        double b = Math.Clamp(r.Bottom, 0, s.Height);
        return new Rect(new Point(l, t), new Point(ri, b));
    }

    public override void Render(DrawingContext context)
    {
        Size imgSize = OriginalImage?.Size ?? new Size(1, 1);
        context.Custom(new ImageCanvasCustomDrawOperation(
            Bounds,
            _cachedOriginalSkImage,
            _cachedProcessedSkImage,
            _cachedMaskSkImages,
            Boxes,
            GetImageRect(),
            _selectedBoxIndex,
            Mode,
            _zoom,
            IsCompareMode,
            HideMasks,
            MaskVisibility,
            imgSize));
    }
}

public class ImageCanvasCustomDrawOperation : ICustomDrawOperation
{
    private readonly SKImage? _original;
    private readonly SKImage? _processed;
    private readonly List<SKImage>? _masks;
    private readonly ObservableCollection<BBoxEntry> _boxes;
    private readonly Rect _imageRect;
    private readonly int _selectedBoxIndex;
    private readonly CanvasMode _mode;
    private readonly double _zoom;
    private readonly bool _isCompareMode;
    private readonly bool _hideMasks;
    private readonly IReadOnlyList<bool>? _maskVisibility;
    private readonly Size _imgSize;

    public Rect Bounds { get; }

    public ImageCanvasCustomDrawOperation(
        Rect bounds,
        SKImage? original,
        SKImage? processed,
        List<SKImage>? masks,
        ObservableCollection<BBoxEntry> boxes,
        Rect imageRect,
        int selectedBoxIndex,
        CanvasMode mode,
        double zoom,
        bool isCompareMode,
        bool hideMasks,
        IReadOnlyList<bool>? maskVisibility,
        Size imgSize)
    {
        Bounds = bounds;
        _original = original;
        _processed = processed;
        _masks = masks;
        _boxes = boxes;
        _imageRect = imageRect;
        _selectedBoxIndex = selectedBoxIndex;
        _mode = mode;
        _zoom = zoom;
        _isCompareMode = isCompareMode;
        _hideMasks = hideMasks;
        _maskVisibility = maskVisibility;
        _imgSize = imgSize;
    }

    public void Dispose() { }

    public bool Equals(ICustomDrawOperation? other) => false;

    public bool HitTest(Point p) => true;

    public void Render(ImmediateDrawingContext context)
    {
        var lease = context.TryGetFeature<ISkiaSharpApiLeaseFeature>();
        if (lease == null) return;

        using var skiaContext = lease.Lease();
        var canvas = skiaContext.SkCanvas;

        canvas.Clear(SKColors.Black);

        var displayImage = _isCompareMode ? _original : (_processed ?? _original);
        if (displayImage != null)
        {
            // Draw image
            var dest = _imageRect.ToSKRect();
            canvas.DrawImage(displayImage, dest);

            // Draw masks
            if (!_isCompareMode && !_hideMasks && _masks != null && _masks.Count > 0)
            {
                RenderMasks(canvas, _masks, _imageRect);
            }

            // Draw boxes
            if (_mode == CanvasMode.BBox)
            {
                for (int i = 0; i < _boxes.Count; i++)
                {
                    DrawBox(canvas, _boxes[i], i == _selectedBoxIndex, _imgSize);
                }
            }
        }
    }

    private void RenderMasks(SKCanvas canvas, List<SKImage> masks, Rect destRect)
    {
        for (int i = 0; i < masks.Count; i++)
        {
            if (_maskVisibility != null && i < _maskVisibility.Count && !_maskVisibility[i])
            {
                continue;
            }
            canvas.DrawImage(masks[i], destRect.ToSKRect());
        }
    }

    private void DrawBox(SKCanvas canvas, BBoxEntry entry, bool selected, Size imgSize)
    {
        var screenRect = ImageToScreen(entry.Rect, _imageRect, imgSize).ToSKRect();
        
        using var paint = new SKPaint
        {
            Color = entry.Label ? SKColors.Cyan : SKColors.Red,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = selected ? 3 : 1,
            IsAntialias = true
        };

        if (!selected)
        {
            paint.PathEffect = SKPathEffect.CreateDash(new float[] { 4, 4 }, 0);
        }

        canvas.DrawRect(screenRect, paint);

        if (selected)
        {
            paint.Style = SKPaintStyle.Fill;
            paint.PathEffect = null;
            var handles = GetHandlePoints(screenRect);
            foreach (var h in handles)
            {
                canvas.DrawRect(SKRect.Create(h.X - 4, h.Y - 4, 8, 8), paint);
            }
        }

        // Draw label
        using var textPaint = new SKPaint
        {
            Color = SKColors.White,
            IsAntialias = true
        };
        using var font = new SKFont(SKTypeface.Default, 12);
        canvas.DrawText(entry.Label ? "FG" : "BG", screenRect.Left, screenRect.Top - 5, font, textPaint);
    }

    private Rect ImageToScreen(Rect imgRect, Rect displayRect, Size imgSize)
    {
        var x = displayRect.X + imgRect.X / imgSize.Width * displayRect.Width;
        var y = displayRect.Y + imgRect.Y / imgSize.Height * displayRect.Height;
        var w = imgRect.Width / imgSize.Width * displayRect.Width;
        var h = imgRect.Height / imgSize.Height * displayRect.Height;
        return new Rect(x, y, w, h);
    }

    private SKPoint[] GetHandlePoints(SKRect r)
    {
        return new[]
        {
            new SKPoint(r.Left, r.Top), new SKPoint(r.Right, r.Top),
            new SKPoint(r.Left, r.Bottom), new SKPoint(r.Right, r.Bottom),
            new SKPoint(r.Left + r.Width/2, r.Top),
            new SKPoint(r.Left + r.Width/2, r.Bottom),
            new SKPoint(r.Left, r.Top + r.Height/2),
            new SKPoint(r.Right, r.Top + r.Height/2)
        };
    }
}

public static class Extensions
{
    public static SKImage ToSKImage(this Bitmap bitmap)
    {
        using var ms = new System.IO.MemoryStream();
        bitmap.Save(ms);
        ms.Seek(0, System.IO.SeekOrigin.Begin);
        return SKImage.FromEncodedData(ms);
    }
}
