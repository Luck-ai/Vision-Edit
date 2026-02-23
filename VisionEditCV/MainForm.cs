using VisionEditCV.Api;
using VisionEditCV.Controls;
using VisionEditCV.Models;
using VisionEditCV.Processing;

namespace VisionEditCV
{
    public partial class MainForm : Form
    {
        // ── Theme constants (shared with Designer) ───────────────────────────
        internal static readonly Color _BgMain = Color.FromArgb(13, 13, 13);
        internal static readonly Color _BgPanel = Color.FromArgb(20, 20, 20);
        internal static readonly Color _BgButton = Color.FromArgb(30, 32, 36);
        internal static readonly Color _Cyan = Color.FromArgb(0, 229, 255);
        internal static readonly Color _TextMain = Color.FromArgb(220, 220, 220);
        internal static readonly Color _TextDim = Color.FromArgb(120, 120, 150);
        internal static readonly Color _BorderColor = Color.FromArgb(0, 229, 255);

        // ── API ───────────────────────────────────────────────────────────────
        private readonly Sam3Client _client = new Sam3Client();
        private SegmentationResult? _lastResult;
        private string? _currentImagePath;
        private CancellationTokenSource? _healthCts;

        // ── Async preview debounce ────────────────────────────────────────────
        private CancellationTokenSource? _previewCts;

        // ── Runtime state ─────────────────────────────────────────────────────
        private string _activeEffect = "";
        private bool _comparingOriginal = false;
        private bool _pixelateMode = true;
        private bool _pbTargetBg = false;
        private Bitmap? _stickerCustomBg = null;

        // ── Applied effects tracking ─────────────────────────────────────────
        private readonly List<string> _appliedEffects = new();

        // ═════════════════════════════════════════════════════════════════════
        //  Constructor
        // ═════════════════════════════════════════════════════════════════════

        public MainForm()
        {
            InitializeComponent();
            PostInit();
        }

        /// <summary>
        /// Runs after InitializeComponent() to wire events and finish setup
        /// that cannot be expressed in the designer (dynamic positioning, etc.).
        /// </summary>
        private void PostInit()
        {
            // Server URL comes from the API client
            _txtServerUrl.Text = _client.BaseUrl;

            WireEvents();
            WireResizeHandlers();
            // Establish initial bottom-bar state: BBox active, prompt hidden
            SetCanvasMode(CanvasMode.BoundingBox);

            // Auto-connect to server on startup
            _ = StartServerAsync();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Event Wiring
        // ═════════════════════════════════════════════════════════════════════

        private void WireEvents()
        {
            // Image loading
            _btnChangeImage.Click += (s, e) => OpenImageFile();
            _canvas.ImageDropped += (s, e) => OnImageLoaded();
            _canvas.Click += (s, e) =>
            {
                if (_canvas.OriginalBitmap == null) OpenImageFile();
            };

            // Effect buttons
            _btnColorGrading.Click += (s, e) => ActivateEffect("ColorGrading");
            _btnArtisticStyle.Click += (s, e) => ActivateEffect("Artistic");
            _btnStickerGen.Click += (s, e) => ActivateEffect("Sticker");
            _btnPixelBlur.Click += (s, e) => ActivateEffect("PixelBlur");
            _btnPortrait.Click += (s, e) => ActivateEffect("Portrait");
            _btnGrayscale.Click += (s, e) => ActivateEffect("Grayscale");

            // Apply effect — commits preview as new working image
            _btnApplyEffect.Click += (s, e) => ApplyCurrentEffect();

            // Reset all applied effects
            _btnResetAll.Click += (s, e) => ResetAllEffects();

            // Selection mode toggle
            _btnBBox.Click += (s, e) => SetCanvasMode(CanvasMode.BoundingBox);
            _btnPrompt.Click += (s, e) => SetCanvasMode(CanvasMode.Prompt);

            // Segment
            _btnSegment.Click += async (s, e) => await RunSegmentation();

            // Compare
            _btnCompare.Click += (s, e) =>
            {
                _comparingOriginal = !_comparingOriginal;
                _canvas.ShowOriginal(_comparingOriginal);
                _btnCompare.BackColor = _comparingOriginal ? _Cyan : _BgButton;
                _btnCompare.ForeColor = _comparingOriginal ? _BgMain : _TextMain;
            };

            // Save
            _btnSave.Click += (s, e) => SaveImage();

            // Mask selection
            _canvas.MaskSelectionChanged += (s, e) =>
            {
                _maskList.SetRowSelected(e.MaskIndex, e.Selected);
                TriggerLivePreview();
            };
            _maskList.MaskSelectionChanged += (s, e) =>
            {
                _canvas.SetMaskSelected(e.MaskIndex, e.Selected);
                TriggerLivePreview();
            };

            _canvas.BBoxChanged += (s, e) => { };
            _canvas.MouseDown += (s, e) => _canvas.Focus();

            // ── Color grading ─────────────────────────────────────────────────
            WireSlider(_cgTintStrength);
            WireSlider(_cgBrightness);
            WireSlider(_cgContrast);
            _cgTargetBg.CheckedChanged += (s, e) => TriggerLivePreview();
            _cgTintSwatch.ColorChanged += (s, e) => TriggerLivePreview();

            // ── Artistic ──────────────────────────────────────────────────────
            WireSlider(_artIntensity);

            // ── Sticker ───────────────────────────────────────────────────────
            WireSlider(_stScale);
            WireSlider(_stRotation);
            WireSlider(_stThickness);
            WireSlider(_stShadowBlur);
            _stBorderColor.ColorChanged += (s, e) => TriggerLivePreview();

            _rdoStickerOriginalBg.CheckedChanged += (s, e) => TriggerLivePreview();
            _rdoStickerColorBg.CheckedChanged += (s, e) =>
            {
                _stBgColorSwatch.Visible = _rdoStickerColorBg.Checked;
                _btnStickerUploadBg.Visible = false;
                TriggerLivePreview();
            };
            _rdoStickerImageBg.CheckedChanged += (s, e) =>
            {
                _btnStickerUploadBg.Visible = _rdoStickerImageBg.Checked;
                _stBgColorSwatch.Visible = false;
                TriggerLivePreview();
            };
            _rdoStickerTransparentBg.CheckedChanged += (s, e) => TriggerLivePreview();
            _stBgColorSwatch.ColorChanged += (s, e) => TriggerLivePreview();
            _btnStickerUploadBg.Click += (s, e) => PickStickerBackground();

            // ── Portrait ─────────────────────────────────────────────────────
            WireSlider(_ptBlurStrength);
            WireSlider(_ptFeatherAmount);

            // ── Grayscale ─────────────────────────────────────────────────────
            _gsTargetBg.CheckedChanged += (s, e) => TriggerLivePreview();

            // ── PixelBlur ─────────────────────────────────────────────────────
            _btnPixelMode.Click += (s, e) =>
            {
                _pixelateMode = true;
                _btnPixelMode.BackColor = _Cyan; _btnPixelMode.ForeColor = _BgMain;
                _btnBlurMode.BackColor = _BgButton; _btnBlurMode.ForeColor = _TextMain;
                TriggerLivePreview();
            };
            _btnBlurMode.Click += (s, e) =>
            {
                _pixelateMode = false;
                _btnBlurMode.BackColor = _Cyan; _btnBlurMode.ForeColor = _BgMain;
                _btnPixelMode.BackColor = _BgButton; _btnPixelMode.ForeColor = _TextMain;
                TriggerLivePreview();
            };
            WireSlider(_pbIntensity);
            _btnPbForeground.Click += (s, e) =>
            {
                _pbTargetBg = false;
                _btnPbForeground.BackColor = _Cyan; _btnPbForeground.ForeColor = _BgMain;
                _btnPbBackground.BackColor = _BgButton; _btnPbBackground.ForeColor = _TextMain;
                TriggerLivePreview();
            };
            _btnPbBackground.Click += (s, e) =>
            {
                _pbTargetBg = true;
                _btnPbBackground.BackColor = _Cyan; _btnPbBackground.ForeColor = _BgMain;
                _btnPbForeground.BackColor = _BgButton; _btnPbForeground.ForeColor = _TextMain;
                TriggerLivePreview();
            };

            // ── Server ────────────────────────────────────────────────────────
            _btnStartServer.Click += async (s, e) =>
            {
                // If currently connecting, cancel
                if (_healthCts != null)
                {
                    _healthCts.Cancel();
                    _healthCts.Dispose();
                    _healthCts = null;
                    SetServerStatus("Disconnected", _TextDim);
                    SetStartServerButton(connecting: false, online: false);
                    return;
                }
                // If disconnected, retry connection
                await StartServerAsync();
            };
            _txtServerUrl.TextChanged += (s, e) => _client.BaseUrl = _txtServerUrl.Text.Trim();
        }

        private void WireResizeHandlers()
        {
            // Server URL textbox/status label fill available width
            _serverPanel.Resize += (s, e) =>
            {
                int w = _serverPanel.ClientSize.Width - _serverPanel.Padding.Horizontal;
                _txtServerUrl.Width = w;
                _lblServerStatus.Width = w;
            };
        }

        private void WireSlider(SliderControl sc)
        {
            sc.ValueChanged += (s, e) => TriggerLivePreview();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Logic – Image / Canvas
        // ═════════════════════════════════════════════════════════════════════

        private void OpenImageFile()
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Open Image",
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.webp"
            };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                _canvas.LoadImage(dlg.FileName);
                _currentImagePath = dlg.FileName;
                _maskList.ClearRows();
                _lastResult = null;
                _appliedEffects.Clear();
                RebuildAppliedEffectsChips();
                OnImageLoaded();
            }
        }

        private void OnImageLoaded()
        {
            SetCanvasMode(CanvasMode.BoundingBox);
        }

        private void SetCanvasMode(CanvasMode mode)
        {
            _canvas.Mode = mode;

            bool isBbox = mode == CanvasMode.BoundingBox;

            // BBox active: dark teal fill + cyan border + cyan text
            _btnBBox.BackColor = isBbox ? Color.FromArgb(14, 48, 52) : Color.Transparent;
            _btnBBox.ForeColor = isBbox ? _Cyan : _TextMain;
            _btnBBox.FlatAppearance.BorderSize = isBbox ? 2 : 0;
            _btnBBox.FlatAppearance.BorderColor = _Cyan;

            // Prompt toggle
            _btnPrompt.BackColor = isBbox ? Color.Transparent : Color.FromArgb(14, 48, 52);
            _btnPrompt.ForeColor = isBbox ? _TextMain : _Cyan;
            _btnPrompt.FlatAppearance.BorderSize = isBbox ? 0 : 2;
            _btnPrompt.FlatAppearance.BorderColor = _Cyan;

            // Show prompt box only in Prompt mode; Segment button is always visible
            _promptBox.Visible = !isBbox;

            _canvas.Invalidate();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Logic – Segmentation
        // ═════════════════════════════════════════════════════════════════════

        private async Task RunSegmentation()
        {
            if (_currentImagePath == null || _canvas.OriginalBitmap == null)
            {
                MessageBox.Show("Please upload an image first.", "No Image",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            _client.BaseUrl = _txtServerUrl.Text.Trim();
            SetLoading(true);

            try
            {
                SegmentationResult? result;

                if (_canvas.Mode == CanvasMode.BoundingBox)
                {
                    var entries = _canvas.BBoxEntries;
                    if (entries.Count == 0)
                    {
                        MessageBox.Show("Please draw at least one bounding box on the image first.",
                            "No Bounding Box", MessageBoxButtons.OK, MessageBoxIcon.Information);
                        SetLoading(false);
                        return;
                    }

                    float[][] boxes = entries
                        .Select(e => new[] { e.Rect.X, e.Rect.Y, e.Rect.Width, e.Rect.Height })
                        .ToArray();
                    bool[] labels = entries.Select(e => e.Label).ToArray();

                    result = await _client.SegmentWithBBoxAsync(_currentImagePath, boxes, labels);
                }
                else
                {
                    string prompt = _promptBox.Text.Trim();
                    if (string.IsNullOrEmpty(prompt))
                    {
                        MessageBox.Show("Please enter a text prompt.", "No Prompt",
                            MessageBoxButtons.OK, MessageBoxIcon.Information);
                        SetLoading(false);
                        return;
                    }
                    result = await _client.SegmentWithTextAsync(_currentImagePath, prompt);
                }

                if (result == null || result.Masks.Count == 0)
                {
                    MessageBox.Show(
                        "No masks returned from server. Try a different prompt or bounding box.",
                        "No Results", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    SetLoading(false);
                    return;
                }

                _lastResult = result;
                _canvas.SetMasks(result);
                _maskList.Populate(_canvas.MaskColors, _canvas.MaskScores);
                _rightPanel.Visible = true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Segmentation Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                SetLoading(false);
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Logic – Effect activation
        // ═════════════════════════════════════════════════════════════════════

        private void ActivateEffect(string effect)
        {
            if (_canvas.OriginalBitmap == null)
            {
                MessageBox.Show("Please load an image before selecting an effect.",
                    "No Image", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            _activeEffect = effect;

            foreach (var btn in new[] { _btnColorGrading, _btnArtisticStyle,
                                         _btnStickerGen, _btnPixelBlur, _btnPortrait, _btnGrayscale })
            {
                btn.BackColor = _BgButton;
                btn.ForeColor = _TextMain;
                btn.FlatAppearance.BorderSize = 0;
                btn.FlatAppearance.BorderColor = _BgButton; // can't use Transparent here
            }

            var activeBtn = effect switch
            {
                "ColorGrading" => _btnColorGrading,
                "Artistic" => _btnArtisticStyle,
                "Sticker" => _btnStickerGen,
                "PixelBlur" => _btnPixelBlur,
                "Portrait" => _btnPortrait,
                "Grayscale" => _btnGrayscale,
                _ => (DarkButton?)null
            };
            if (activeBtn != null)
            {
                // Active state: dark teal fill + cyan text + cyan border (matches reference)
                activeBtn.BackColor = Color.FromArgb(14, 48, 52);
                activeBtn.ForeColor = _Cyan;
                activeBtn.FlatAppearance.BorderSize = 2;
                activeBtn.FlatAppearance.BorderColor = _Cyan;
            }

            var activePanel = effect switch
            {
                "ColorGrading" => _panelColorGrading,
                "Artistic" => _panelArtistic,
                "Sticker" => _panelSticker,
                "PixelBlur" => _panelPixelBlur,
                "Portrait" => _panelPortrait,
                "Grayscale" => _panelGrayscale,
                _ => (Panel?)null
            };

            foreach (var p in new[] { _panelColorGrading, _panelArtistic,
                                       _panelSticker, _panelPixelBlur, _panelPortrait, _panelGrayscale })
                p.Visible = false;

            if (activePanel != null) activePanel.Visible = true;

            // Hide placeholder when an effect is selected
            _lblNoEffect.Visible = activePanel == null;

            // Show Apply button for all real effects
            _btnApplyEffect.Visible = activePanel != null;

            TriggerLivePreview();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Logic – Apply effect (commit preview as new working image)
        // ═════════════════════════════════════════════════════════════════════

        private void ApplyCurrentEffect()
        {
            if (string.IsNullOrEmpty(_activeEffect)) return;
            if (_canvas.OriginalBitmap == null) return;

            // Cancel any pending live preview
            _previewCts?.Cancel();
            _previewCts?.Dispose();
            _previewCts = null;

            var selectedMasks = new List<float[,]>();
            for (int i = 0; i < _canvas.Masks.Count; i++)
                if (_canvas.MaskSelected[i]) selectedMasks.Add(_canvas.Masks[i]);

            if (selectedMasks.Count == 0) return;

            var args = CaptureEffectArgs();
            Bitmap current = (Bitmap)_canvas.OriginalBitmap.Clone();

            foreach (var mask in selectedMasks)
            {
                Bitmap? next = ApplyEffectArgs(_activeEffect, current, mask, args);
                if (next != null)
                {
                    current.Dispose();
                    current = next;
                }
            }

            // Commit the result as the new original so effects can be chained
            _canvas.CommitProcessedAsOriginal(current);
            current.Dispose();

            // Track this applied effect
            _appliedEffects.Add(GetEffectDisplayName(_activeEffect));
            RebuildAppliedEffectsChips();

            // Refresh the display
            TriggerLivePreview();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Logic – Async debounced preview
        // ═════════════════════════════════════════════════════════════════════

        private void TriggerLivePreview()
        {
            if (string.IsNullOrEmpty(_activeEffect)) return;
            if (_canvas.OriginalBitmap == null) return;

            // Cancel any pending preview
            _previewCts?.Cancel();
            _previewCts?.Dispose();
            _previewCts = new CancellationTokenSource();
            var token = _previewCts.Token;

            // Collect selected masks and snapshot all control state on the UI thread
            var selectedMasks = new List<float[,]>();
            for (int i = 0; i < _canvas.Masks.Count; i++)
                if (_canvas.MaskSelected[i]) selectedMasks.Add(_canvas.Masks[i]);

            if (selectedMasks.Count == 0) return;

            var effect = _activeEffect;
            var src = (Bitmap)_canvas.OriginalBitmap.Clone();
            var args = CaptureEffectArgs();

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(180, token); // debounce

                    Bitmap current = (Bitmap)src.Clone();
                    foreach (var mask in selectedMasks)
                    {
                        token.ThrowIfCancellationRequested();
                        Bitmap? next = ApplyEffectArgs(effect, current, mask, args);
                        if (next != null)
                        {
                            if (!ReferenceEquals(current, src)) current.Dispose();
                            current = next;
                        }
                    }

                    token.ThrowIfCancellationRequested();
                    var result = current;

                    Invoke(() =>
                    {
                        _canvas.SetProcessedBitmap(result);
                        result.Dispose();
                    });
                }
                catch (OperationCanceledException) { /* stale — discard */ }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Preview error: {ex.Message}");
                }
                finally
                {
                    src.Dispose();
                }
            }, token);
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Logic – Effect parameter snapshot
        // ═════════════════════════════════════════════════════════════════════

        private record EffectArgs(
            Color TintColor, float TintStrength, int Brightness, float Contrast,
            bool CgTargetBg,
            float ArtIntensity,
            int StScale, int StRotation, Color StBorderColor, int StThickness, int StShadowBlur,
            bool StickerOriginalBg, bool StickerSolidBg, Color StickerSolidColor,
            Bitmap? StickerImageBg, bool StickerTransparentBg,
            bool PixelateMode, bool PbTargetBg, int PbIntensity,
            int PortraitBlur, int PortraitFeather,
            bool GsTargetBg
        );

        private EffectArgs CaptureEffectArgs() => new EffectArgs(
            TintColor: _cgTintSwatch.SelectedColor,
            TintStrength: _cgTintStrength.Value / 100f,
            Brightness: _cgBrightness.Value,
            Contrast: _cgContrast.Value / 10f,
            CgTargetBg: _cgTargetBg.Checked,
            ArtIntensity: _artIntensity.Value / 100f,
            StScale: _stScale.Value,
            StRotation: _stRotation.Value,
            StBorderColor: _stBorderColor.SelectedColor,
            StThickness: _stThickness.Value,
            StShadowBlur: _stShadowBlur.Value,
            StickerOriginalBg: _rdoStickerOriginalBg.Checked,
            StickerSolidBg: _rdoStickerColorBg.Checked,
            StickerSolidColor: _stBgColorSwatch.SelectedColor,
            StickerImageBg: _stickerCustomBg != null ? (Bitmap)_stickerCustomBg.Clone() : null,
            StickerTransparentBg: _rdoStickerTransparentBg.Checked,
            PixelateMode: _pixelateMode,
            PbTargetBg: _pbTargetBg,
            PbIntensity: _pbIntensity.Value,
            PortraitBlur: _ptBlurStrength.Value,
            PortraitFeather: _ptFeatherAmount.Value,
            GsTargetBg: _gsTargetBg.Checked
        );

        // ═════════════════════════════════════════════════════════════════════
        //  Logic – Effect application
        // ═════════════════════════════════════════════════════════════════════

        private Bitmap? ApplyEffectArgs(string effect, Bitmap image, float[,] mask, EffectArgs a)
        {
            return effect switch
            {
                "ColorGrading" => ImageEffects.ColorGrading(
                    image, mask,
                    a.TintColor, a.TintStrength,
                    a.Brightness, a.Contrast,
                    blackAndWhite: false, a.CgTargetBg),

                "Artistic" => ImageEffects.PencilSketchMasked(
                    image, mask, a.ArtIntensity),

                "Sticker" => ApplyStickerEffect(image, mask, a),

                "PixelBlur" => ApplyPixelBlur(image, mask, a),

                "Portrait" => ImageEffects.PortraitEffect(
                    image, mask, a.PortraitBlur, a.PortraitFeather),

                "Grayscale" => ImageEffects.ColorGrading(
                    image, mask,
                    Color.White, 0f,
                    0, 1.0f,
                    blackAndWhite: true, targetBackground: a.GsTargetBg),

                _ => null
            };
        }

        private Bitmap? ApplyStickerEffect(Bitmap image, float[,] mask, EffectArgs a)
        {
            Bitmap sticker = ImageEffects.ExtractSticker(
                image, mask,
                threshold: 0.5f,
                contourThickness: a.StThickness,
                shadowBlur: a.StShadowBlur,
                borderColor: a.StBorderColor,
                scaleFactor: a.StScale / 10f,
                rotationAngle: a.StRotation);

            // Transparent background: return the BGRA sticker directly
            if (a.StickerTransparentBg)
            {
                a.StickerImageBg?.Dispose();
                return sticker;
            }

            Bitmap bg;
            if (a.StickerOriginalBg)
                bg = (Bitmap)image.Clone();
            else if (a.StickerSolidBg)
                bg = ImageEffects.SolidColorBackground(a.StickerSolidColor, image.Width, image.Height);
            else if (a.StickerImageBg != null)
                bg = (Bitmap)a.StickerImageBg.Clone();
            else
                bg = (Bitmap)image.Clone();

            var result = ImageEffects.CompositeSticker(sticker, bg);
            sticker.Dispose();
            bg.Dispose();
            a.StickerImageBg?.Dispose(); // cloned snapshot
            return result;
        }

        private Bitmap? ApplyPixelBlur(Bitmap image, float[,] mask, EffectArgs a)
        {
            float[,] workMask = a.PbTargetBg ? InvertMask(mask) : mask;
            int intensity = Math.Max(2, a.PbIntensity);

            if (a.PixelateMode)
            {
                int pixelSize = Math.Max(2, intensity * 64 / 100);
                return ImageEffects.PixelateMasked(image, workMask, pixelSize);
            }
            else
            {
                int k = Math.Max(3, intensity * 51 / 100);
                if (k % 2 == 0) k++;
                return ImageEffects.BlurMasked(image, workMask, k);
            }
        }

        private static float[,] InvertMask(float[,] mask)
        {
            int h = mask.GetLength(0), w = mask.GetLength(1);
            var inv = new float[h, w];
            for (int r = 0; r < h; r++)
                for (int c = 0; c < w; c++)
                    inv[r, c] = 1f - mask[r, c];
            return inv;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Logic – Sticker background picker
        // ═════════════════════════════════════════════════════════════════════

        private void PickStickerBackground()
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Open Background Image",
                Filter = "Image Files|*.jpg;*.jpeg;*.png;*.bmp;*.webp"
            };
            if (dlg.ShowDialog() == DialogResult.OK)
            {
                _stickerCustomBg?.Dispose();
                _stickerCustomBg = new Bitmap(dlg.FileName);
                TriggerLivePreview();
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Logic – Applied effects tracking
        // ═════════════════════════════════════════════════════════════════════

        private static string GetEffectDisplayName(string effect) => effect switch
        {
            "ColorGrading" => "Color Grading",
            "Artistic" => "Artistic Style",
            "Sticker" => "Sticker Gen",
            "PixelBlur" => "Pixelation & Blur",
            "Portrait" => "Portrait Effect",
            "Grayscale" => "Grayscale",
            _ => effect
        };

        private void RebuildAppliedEffectsChips()
        {
            _appliedEffectsPanel.Controls.Clear();

            if (_appliedEffects.Count == 0)
            {
                _appliedEffectsPanel.Visible = false;
                return;
            }

            foreach (var name in _appliedEffects)
            {
                var chip = new Label
                {
                    Text = name,
                    BackColor = Color.FromArgb(14, 48, 52),
                    ForeColor = _Cyan,
                    Font = new Font("Segoe UI", 7.5f, FontStyle.Bold),
                    AutoSize = false,
                    Size = new Size(TextRenderer.MeasureText(name, new Font("Segoe UI", 7.5f, FontStyle.Bold)).Width + 16, 24),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Margin = new Padding(2, 2, 2, 2),
                    Padding = new Padding(6, 0, 6, 0),
                };
                _appliedEffectsPanel.Controls.Add(chip);
            }

            _appliedEffectsPanel.Controls.Add(_btnResetAll);
            _appliedEffectsPanel.Visible = true;
        }

        private void ResetAllEffects()
        {
            _appliedEffects.Clear();
            RebuildAppliedEffectsChips();

            // Reload the original image from disk
            if (_currentImagePath != null)
            {
                _canvas.LoadImage(_currentImagePath);
                TriggerLivePreview();
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Logic – Save
        // ═════════════════════════════════════════════════════════════════════

        private void SaveImage()
        {
            if (_canvas.OriginalBitmap == null)
            {
                MessageBox.Show("No image to save.", "Save Image",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            using var dlg = new SaveFileDialog
            {
                Title = "Save Image",
                Filter = "PNG Image|*.png|JPEG Image|*.jpg",
                DefaultExt = "png",
                FileName = "visionedit_output"
            };

            if (dlg.ShowDialog() != DialogResult.OK) return;

            try
            {
                Bitmap output = (Bitmap)_canvas.OriginalBitmap.Clone();

                var selectedMasks = new List<float[,]>();
                for (int i = 0; i < _canvas.Masks.Count; i++)
                    if (_canvas.MaskSelected[i]) selectedMasks.Add(_canvas.Masks[i]);

                if (selectedMasks.Count > 0 && !string.IsNullOrEmpty(_activeEffect))
                {
                    var args = CaptureEffectArgs();
                    Bitmap current = output;
                    foreach (var mask in selectedMasks)
                    {
                        Bitmap? next = ApplyEffectArgs(_activeEffect, current, mask, args);
                        if (next != null)
                        {
                            if (!ReferenceEquals(current, output)) current.Dispose();
                            current = next;
                        }
                    }
                    output = current;
                }

                var format = dlg.FileName.EndsWith(".jpg", StringComparison.OrdinalIgnoreCase)
                    ? System.Drawing.Imaging.ImageFormat.Jpeg
                    : System.Drawing.Imaging.ImageFormat.Png;

                output.Save(dlg.FileName, format);
                output.Dispose();

                MessageBox.Show($"Image saved to:\n{dlg.FileName}", "Saved",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Save failed: {ex.Message}", "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Logic – Loading overlay
        // ═════════════════════════════════════════════════════════════════════

        private void SetLoading(bool loading)
        {
            if (InvokeRequired) { Invoke(() => SetLoading(loading)); return; }
            _loadingOverlay.Visible = loading;
            _btnSegment.Enabled = !loading;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Logic – Start Server
        // ═════════════════════════════════════════════════════════════════════

        private async Task StartServerAsync()
        {
            // If already polling, cancel and restart
            if (_healthCts != null)
            {
                _healthCts.Cancel();
                _healthCts.Dispose();
                _healthCts = null;
            }

            _client.BaseUrl = _txtServerUrl.Text.Trim();
            _healthCts = new CancellationTokenSource();
            var token = _healthCts.Token;

            SetStartServerButton(connecting: true);
            SetServerStatus("Connecting…", Color.FromArgb(255, 180, 0));

            var progress = new Progress<string>(msg =>
            {
                if (InvokeRequired) Invoke(() => SetServerStatus(msg, Color.FromArgb(255, 180, 0)));
                else SetServerStatus(msg, Color.FromArgb(255, 180, 0));
            });

            bool ok = false;
            try { ok = await _client.WaitForHealthAsync(progress, token); }
            catch { }

            _healthCts?.Dispose();
            _healthCts = null;

            if (ok)
            {
                SetServerStatus("Connected", Color.FromArgb(0, 220, 100));
                SetStartServerButton(connecting: false, online: true);
            }
            else
            {
                SetServerStatus("Disconnected", _TextDim);
                SetStartServerButton(connecting: false, online: false);
            }
        }

        private void SetStartServerButton(bool connecting, bool online = false)
        {
            if (InvokeRequired) { Invoke(() => SetStartServerButton(connecting, online)); return; }

            if (connecting)
            {
                _btnStartServer.Text = "Connecting…";
                _btnStartServer.BackColor = Color.FromArgb(255, 180, 0);
                _btnStartServer.ForeColor = Color.FromArgb(13, 13, 13);
            }
            else if (online)
            {
                _btnStartServer.Text = "Connected";
                _btnStartServer.BackColor = Color.FromArgb(0, 130, 60);
                _btnStartServer.ForeColor = Color.White;
            }
            else
            {
                _btnStartServer.Text = "Disconnected";
                _btnStartServer.BackColor = Color.FromArgb(80, 80, 80);
                _btnStartServer.ForeColor = Color.White;
            }
        }

        private void SetServerStatus(string message, Color color)
        {
            if (InvokeRequired) { Invoke(() => SetServerStatus(message, color)); return; }
            _lblServerStatus.Text = message;
            _lblServerStatus.ForeColor = color;
        }

        // ── Layout event handlers (wired in Designer) ────────────────────────

        private void EffectSubPanelResize(object sender, EventArgs e)
        {
            if (sender is not Panel panel) return;
            int margin = 16;
            _btnApplyEffect.Left = panel.ClientSize.Width - _btnApplyEffect.Width - margin;
            _btnApplyEffect.Top = panel.ClientSize.Height - _btnApplyEffect.Height - margin;
        }

        private void TopBarResize(object sender, EventArgs e)
        {
            if (sender is not Panel bar) return;
            int cy = bar.ClientSize.Height / 2;

            foreach (Control c in bar.Controls)
                c.Top = cy - c.Height / 2;

            _btnStartServer.Left = bar.ClientSize.Width - _btnStartServer.Width - 16;
        }

        private void PaintCenterBorder(object sender, PaintEventArgs e)
        {
            if (sender is not Panel p) return;
            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            const int inset = 12;
            var rect = new Rectangle(inset, inset, p.Width - inset * 2 - 1, p.Height - inset * 2 - 1);
            using var pen = new Pen(Color.FromArgb(0, 229, 255), 2f);
            pen.DashStyle = System.Drawing.Drawing2D.DashStyle.Custom;
            pen.DashPattern = new float[] { 6f, 4f };
            const int radius = 16;
            float d = radius * 2f;
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            path.AddArc(rect.X, rect.Y, d, d, 180, 90);
            path.AddArc(rect.Right - d, rect.Y, d, d, 270, 90);
            path.AddArc(rect.Right - d, rect.Bottom - d, d, d, 0, 90);
            path.AddArc(rect.X, rect.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            g.DrawPath(pen, path);
            path.Dispose();
        }

        private void PaintPanelDepthLeft(object sender, PaintEventArgs e) { }
        private void PaintPanelDepthRight(object sender, PaintEventArgs e) { }

        private void _btnGrayscale_Click(object sender, EventArgs e)
        {

        }

        private void _btnCompare_Click(object sender, EventArgs e)
        {

        }

        private void _lblSelMode_Click(object sender, EventArgs e)
        {

        }

        private void _lblSelMode_Click_1(object sender, EventArgs e)
        {

        }

        private void label1_Click(object sender, EventArgs e)
        {

        }
    }
}
