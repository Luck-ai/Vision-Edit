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
        private bool _artStylizeMode = true;
        private bool _pbTargetBg = false;
        private bool _gsTargetBgMode = false;
        private bool _cgTargetBgMode = false;
        private string _stickerBgMode = "Original"; // "Original" | "Solid" | "Image" | "Transparent"
        private Bitmap? _stickerCustomBg = null;
        private volatile bool _previewRunning = false;

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
            // Run initial layout passes
            TopBarResize(_topBar, EventArgs.Empty);
            EffectSubPanelResize(_effectSubPanel, EventArgs.Empty);
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
            // Image loading / mask management
            _btnChangeImage.Click += (s, e) => OpenImageFile();
            _btnClearMasks.Click += (s, e) => ClearAllMasks();
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

            // Apply / Reset effect
            _btnApplyEffect.Click += (s, e) => ApplyCurrentEffect();
            _btnResetEffect.Click  += (s, e) => ResetCurrentEffect();

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
            _btnCgFg.Click += (s, e) => SetCgMode(targetBg: false);
            _btnCgBg.Click += (s, e) => SetCgMode(targetBg: true);
            _cgTintSwatch.ColorChanged += (s, e) => TriggerLivePreview();

            // ── Artistic ──────────────────────────────────────────────────────
            WireSlider(_artSigmaS);
            WireSlider(_artSigmaR);
            WireSlider(_artShade);
            _btnArtStylize.Click += (s, e) => SetArtMode(stylize: true);
            _btnArtPencil.Click  += (s, e) => SetArtMode(stylize: false);

            // ── Sticker ───────────────────────────────────────────────────────
            WireSlider(_stScale);
            WireSlider(_stRotation);
            WireSlider(_stThickness);
            WireSlider(_stShadowBlur);
            _stBorderColor.ColorChanged += (s, e) => TriggerLivePreview();

            _btnStBgOriginal.Click    += (s, e) => SetStickerBgMode("Original");
            _btnStBgSolid.Click       += (s, e) => SetStickerBgMode("Solid");
            _btnStBgImage.Click       += (s, e) => SetStickerBgMode("Image");
            _btnStBgTransparent.Click += (s, e) => SetStickerBgMode("Transparent");
            _stBgColorSwatch.ColorChanged += (s, e) => TriggerLivePreview();
            _btnStickerUploadBg.Click += (s, e) => PickStickerBackground();

            // ── Portrait ─────────────────────────────────────────────────────
            WireSlider(_ptBlurStrength);
            WireSlider(_ptFeatherAmount);

            // ── Grayscale ─────────────────────────────────────────────────────
            _btnGsFg.Click += (s, e) => SetGsMode(targetBg: false);
            _btnGsBg.Click += (s, e) => SetGsMode(targetBg: true);

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

            // Re-run layout after the form is fully rendered (sizes are finalised)
            Shown += (s, e) => BeginInvoke(() =>
            {
                EffectSubPanelResize(_effectSubPanel, EventArgs.Empty);
                TopBarResize(_topBar, EventArgs.Empty);
            });
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

        private void ClearAllMasks()
        {
            _maskList.ClearRows();
            _lastResult = null;
            _canvas.ClearMasks();
            _rightPanel.Visible = false;
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

        private void SetArtMode(bool stylize)
        {
            _artStylizeMode = stylize;
            _btnArtStylize.BackColor = stylize ? _Cyan : _BgButton;
            _btnArtStylize.ForeColor = stylize ? _BgMain : _TextMain;
            _btnArtPencil.BackColor  = stylize ? _BgButton : _Cyan;
            _btnArtPencil.ForeColor  = stylize ? _TextMain : _BgMain;
            _grpArtSigmaR.Visible = stylize;
            _grpArtShade.Visible  = !stylize;
            TriggerLivePreview();
        }

        private void SetStickerBgMode(string mode)
        {
            _stickerBgMode = mode;
            // Update button visuals
            _btnStBgOriginal.BackColor    = mode == "Original"    ? _Cyan : _BgButton;
            _btnStBgOriginal.ForeColor    = mode == "Original"    ? _BgMain : _TextMain;
            _btnStBgSolid.BackColor       = mode == "Solid"       ? _Cyan : _BgButton;
            _btnStBgSolid.ForeColor       = mode == "Solid"       ? _BgMain : _TextMain;
            _btnStBgImage.BackColor       = mode == "Image"       ? _Cyan : _BgButton;
            _btnStBgImage.ForeColor       = mode == "Image"       ? _BgMain : _TextMain;
            _btnStBgTransparent.BackColor = mode == "Transparent" ? _Cyan : _BgButton;
            _btnStBgTransparent.ForeColor = mode == "Transparent" ? _BgMain : _TextMain;
            // Show/hide secondary controls
            _stBgColorSwatch.Visible    = mode == "Solid";
            _btnStickerUploadBg.Visible = mode == "Image";
            TriggerLivePreview();
        }

        private void SetCgMode(bool targetBg)
        {
            _cgTargetBgMode = targetBg;
            _btnCgFg.BackColor = !targetBg ? _Cyan : _BgButton;
            _btnCgFg.ForeColor = !targetBg ? _BgMain : _TextMain;
            _btnCgBg.BackColor = targetBg  ? _Cyan : _BgButton;
            _btnCgBg.ForeColor = targetBg  ? _BgMain : _TextMain;
            TriggerLivePreview();
        }

        private void SetGsMode(bool targetBg)
        {
            _gsTargetBgMode = targetBg;
            _btnGsFg.BackColor = !targetBg ? _Cyan : _BgButton;
            _btnGsFg.ForeColor = !targetBg ? _BgMain : _TextMain;
            _btnGsBg.BackColor = targetBg  ? _Cyan : _BgButton;
            _btnGsBg.ForeColor = targetBg  ? _BgMain : _TextMain;
            TriggerLivePreview();
        }

        private void DeactivateEffect()
        {
            _activeEffect = "";
            foreach (var btn in new[] { _btnColorGrading, _btnArtisticStyle,
                                         _btnStickerGen, _btnPixelBlur, _btnPortrait, _btnGrayscale })
            {
                btn.BackColor = _BgButton;
                btn.ForeColor = _TextMain;
                btn.FlatAppearance.BorderSize = 0;
                btn.FlatAppearance.BorderColor = _BgButton;
            }
            foreach (var p in new[] { _panelColorGrading, _panelArtistic,
                                       _panelSticker, _panelPixelBlur, _panelPortrait, _panelGrayscale })
                p.Visible = false;
            _lblNoEffect.Visible = true;
            _btnApplyEffect.Visible = false;
            _btnResetEffect.Visible = false;
        }

        private void ActivateEffect(string effect)
        {
            if (_canvas.OriginalBitmap == null)
            {
                MessageBox.Show("Please load an image before selecting an effect.",
                    "No Image", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            // Clicking the already-active effect deselects it
            if (_activeEffect == effect)
            {
                DeactivateEffect();
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
            bool showButtons = activePanel != null;
            _btnApplyEffect.Visible = showButtons;
            _btnResetEffect.Visible = showButtons;

            // Re-run proportional layout now that a panel is visible
            if (showButtons) EffectSubPanelResize(_effectSubPanel, EventArgs.Empty);

            TriggerLivePreview();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Logic – Apply effect (commit preview as new working image)
        // ═════════════════════════════════════════════════════════════════════

        private void ResetCurrentEffect()
        {
            switch (_activeEffect)
            {
                case "ColorGrading":
                    _cgTintStrength.Value = 0;
                    _cgBrightness.Value   = 0;
                    _cgContrast.Value     = 10;
                    SetCgMode(targetBg: false);
                    break;
                case "Artistic":
                    _artSigmaS.Value = 60;
                    _artSigmaR.Value = 45;
                    _artShade.Value  = 5;
                    SetArtMode(stylize: true);
                    break;
                case "Sticker":
                    _stScale.Value    = 10;
                    _stRotation.Value = 0;
                    _stThickness.Value = 15;
                    _stShadowBlur.Value = 15;
                    SetStickerBgMode("Original");
                    break;
                case "PixelBlur":
                    _pbIntensity.Value = 40;
                    _pixelateMode = true;
                    _btnPixelMode.BackColor = _Cyan; _btnPixelMode.ForeColor = _BgMain;
                    _btnBlurMode.BackColor = _BgButton; _btnBlurMode.ForeColor = _TextMain;
                    break;
                case "Portrait":
                    _ptBlurStrength.Value   = 51;
                    _ptFeatherAmount.Value  = 21;
                    break;
                case "Grayscale":
                    SetGsMode(targetBg: false);
                    break;
            }
            TriggerLivePreview();
        }

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

        // Effects that are computationally heavy — use a downscaled image for preview
        private static readonly HashSet<string> _heavyEffects = new() { "Artistic" };
        private const int PreviewMaxDim = 900;
        private const int DebounceMs = 80;
        private const int DebounceHeavyMs = 300;

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

            var effect  = _activeEffect;
            var srcFull = (Bitmap)_canvas.OriginalBitmap.Clone();
            var args    = CaptureEffectArgs();

            // Heavy effects: longer debounce and downscaled preview
            bool heavy    = _heavyEffects.Contains(effect);
            int  debounce = heavy ? DebounceHeavyMs : DebounceMs;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(debounce, token);
                    token.ThrowIfCancellationRequested();

                    // If another computation is still running, wait for it (poll every 30 ms).
                    // If a newer request arrives while waiting, the token is cancelled and we bail.
                    while (_previewRunning)
                    {
                        await Task.Delay(30, token);
                        token.ThrowIfCancellationRequested();
                    }
                    token.ThrowIfCancellationRequested();
                    _previewRunning = true;

                    try
                    {
                        Bitmap src     = heavy ? ScaleForPreview(srcFull, PreviewMaxDim) : srcFull;
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

                        if (heavy && !ReferenceEquals(src, srcFull)) src.Dispose();
                        token.ThrowIfCancellationRequested();

                        Invoke(() => { _canvas.SetProcessedBitmap(current); current.Dispose(); });
                    }
                    finally { _previewRunning = false; }
                }
                catch (OperationCanceledException) { _previewRunning = false; /* stale — discard */ }
                catch (Exception ex)
                {
                    _previewRunning = false;
                    System.Diagnostics.Debug.WriteLine($"Preview error: {ex.Message}");
                }
                finally { srcFull.Dispose(); }
            }, token);
        }

        /// <summary>Returns a copy scaled so the longest side is at most <paramref name="maxDim"/> pixels.
        /// Returns the original if it is already within that limit.</summary>
        private static Bitmap ScaleForPreview(Bitmap src, int maxDim)
        {
            if (src.Width <= maxDim && src.Height <= maxDim) return src;
            float scale = (float)maxDim / Math.Max(src.Width, src.Height);
            int   w     = Math.Max(1, (int)(src.Width  * scale));
            int   h     = Math.Max(1, (int)(src.Height * scale));
            var   bmp   = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using var g = System.Drawing.Graphics.FromImage(bmp);
            g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.Bilinear;
            g.DrawImage(src, 0, 0, w, h);
            return bmp;
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Logic – Effect parameter snapshot
        // ═════════════════════════════════════════════════════════════════════

        private record EffectArgs(
            Color TintColor, float TintStrength, int Brightness, float Contrast,
            bool CgTargetBg,
            bool ArtStylizeMode, int ArtSigmaS, float ArtSigmaR, float ArtShade,
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
            CgTargetBg: _cgTargetBgMode,
            ArtStylizeMode: _artStylizeMode,
            ArtSigmaS: _artSigmaS.Value,
            ArtSigmaR: _artSigmaR.Value / 100f,
            ArtShade: _artShade.Value / 1000f,
            StScale: _stScale.Value,
            StRotation: _stRotation.Value,
            StBorderColor: _stBorderColor.SelectedColor,
            StThickness: _stThickness.Value,
            StShadowBlur: _stShadowBlur.Value,
            StickerOriginalBg: _stickerBgMode == "Original",
            StickerSolidBg: _stickerBgMode == "Solid",
            StickerSolidColor: _stBgColorSwatch.SelectedColor,
            StickerImageBg: _stickerCustomBg != null ? (Bitmap)_stickerCustomBg.Clone() : null,
            StickerTransparentBg: _stickerBgMode == "Transparent",
            PixelateMode: _pixelateMode,
            PbTargetBg: _pbTargetBg,
            PbIntensity: _pbIntensity.Value,
            PortraitBlur: _ptBlurStrength.Value,
            PortraitFeather: _ptFeatherAmount.Value,
            GsTargetBg: _gsTargetBgMode
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

                "Artistic" => a.ArtStylizeMode
                    ? ImageEffects.StylizeMasked(image, mask, a.ArtSigmaS, a.ArtSigmaR)
                    : ImageEffects.PencilSketchMasked(image, mask, a.ArtSigmaS, a.ArtShade),

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

            int cy       = panel.ClientSize.Height / 2;
            int rightPad = panel.Padding.Right;   // 160px reserved for Apply button

            // Right slot: Reset above Apply, both vertically centred as a group
            int slotX       = panel.ClientSize.Width - rightPad + (rightPad - _btnApplyEffect.Width) / 2;
            int totalBtnH   = _btnResetEffect.Height + 6 + _btnApplyEffect.Height;
            int slotTopY    = cy - totalBtnH / 2;
            _btnResetEffect.Left = slotX;
            _btnResetEffect.Top  = slotTopY;
            _btnApplyEffect.Left = slotX;
            _btnApplyEffect.Top  = slotTopY + _btnResetEffect.Height + 6;

            // Available width for control groups (left pad + right pad already excluded by Dock=Fill padding)
            int availW = panel.ClientSize.Width - panel.Padding.Left - rightPad;
            int gap    = 14;

            foreach (var flow in new FlowLayoutPanel[] { _cgFlow, _artFlow, _stFlow, _pbFlow, _ptFlow, _gsFlow })
            {
                var groups = flow.Controls.OfType<Panel>()
                                          .Where(p => p.Visible)
                                          .ToList();
                if (groups.Count == 0) continue;

                int groupH = groups[0].Height;
                int topPad = Math.Max(0, (flow.Height - groupH) / 2);
                flow.Padding = new Padding(0, topPad, 0, 0);

                // Fixed-width groups (MinimumSize.Width > 0) keep their minimum; variable groups share remaining space
                int fixedTotal = groups.Sum(g => g.MinimumSize.Width);
                int varCount   = groups.Count(g => g.MinimumSize.Width == 0);
                int totalGaps  = gap * (groups.Count - 1);
                int remainW    = availW - fixedTotal - totalGaps;
                int varW       = varCount > 0 ? Math.Max(60, remainW / varCount) : 60;

                flow.SuspendLayout();
                for (int i = 0; i < groups.Count; i++)
                {
                    var grp = groups[i];
                    grp.Width  = grp.MinimumSize.Width > 0 ? grp.MinimumSize.Width : varW;
                    grp.Margin = new Padding(0, 0, i < groups.Count - 1 ? gap : 0, 0);

                    foreach (var s in grp.Controls.OfType<SliderControl>())
                        s.Width = grp.Width - 10;
                }
                flow.ResumeLayout(performLayout: true);
            }
        }

        private void TopBarResize(object sender, EventArgs e)
        {
            if (sender is not Panel bar) return;
            int cy  = bar.ClientSize.Height / 2;
            int w   = bar.ClientSize.Width;
            int pad = 12;

            // ── Left group (fixed positions) ─────────────────────────────────
            // label1 → _lblSelMode → _btnBBox → _btnPrompt
            int x = pad;
            label1.Left        = x; x += label1.Width + 6;
            _lblSelMode.Left   = x; x += _lblSelMode.Width + 8;
            _btnBBox.Left      = x; x += _btnBBox.Width + 4;
            _btnPrompt.Left    = x; x += _btnPrompt.Width + 8;
            int leftEdge = x; // where prompt box starts

            // ── Right group (right-anchored) ──────────────────────────────────
            // [Start Server]  [Segment]
            _btnStartServer.Left = w - pad - _btnStartServer.Width;
            _btnSegment.Left     = _btnStartServer.Left - 8 - _btnSegment.Width;
            int rightEdge = _btnSegment.Left - 8; // where prompt box ends

            // ── Prompt box stretches between the two groups ───────────────────
            _promptBox.Left  = leftEdge;
            _promptBox.Width = Math.Max(80, rightEdge - leftEdge);

            // ── Vertically center all controls ────────────────────────────────
            foreach (Control c in bar.Controls)
                c.Top = cy - c.Height / 2;
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
