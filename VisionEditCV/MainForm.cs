using System.Linq;
using VisionEditCV.Api;
using VisionEditCV.Controls;
using VisionEditCV.Models;
using VisionEditCV.Processing;

namespace VisionEditCV
{
    public partial class MainForm : Form
    {
        // ── Theme constants (shared with Designer) ───────────────────────────
        internal static readonly Color _BgMain = Color.FromArgb(18, 18, 18);
        internal static readonly Color _BgPanel = Color.FromArgb(24, 24, 24);
        internal static readonly Color _BgButton = Color.FromArgb(32, 34, 38);
        internal static readonly Color _Cyan = Color.FromArgb(0, 229, 255);
        internal static readonly Color _TextMain = Color.FromArgb(220, 220, 220);
        internal static readonly Color _TextDim = Color.FromArgb(140, 140, 160);
        internal static readonly Color _BorderColor = Color.FromArgb(45, 45, 55);

        // ── API ───────────────────────────────────────────────────────────────
        private readonly Sam3Client _client = new Sam3Client();
        private SegmentationResult? _lastResult;
        private string? _currentImagePath;
        private CancellationTokenSource? _healthCts;

        // ── Async preview debounce ────────────────────────────────────────────
        private CancellationTokenSource? _previewCts;
        private readonly SemaphoreSlim _previewSem = new SemaphoreSlim(1, 1);

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
        private Bitmap? _preEffectSnapshot = null; // clean bitmap before last sticker apply

        // ── Applied effects tracking ─────────────────────────────────────────
        private readonly List<string> _appliedEffects = new();

        // ── Right panel collapse state ───────────────────────────────────────
        private bool _rightPanelExpanded = true;
        private const int RightPanelFullWidth = 360;
        private const int RightPanelCollapsedWidth = 26;

        // ── Window Control logic ──────────────────────────────────────────
        private const int WM_NCHITTEST = 0x84;
        private const int HTCLIENT = 0x1;
        private const int HTCAPTION = 0x2;
        private const int HTLEFT = 10;
        private const int HTRIGHT = 11;
        private const int HTTOP = 12;
        private const int HTTOPLEFT = 13;
        private const int HTTOPRIGHT = 14;
        private const int HTBOTTOM = 15;
        private const int HTBOTTOMLEFT = 16;
        private const int HTBOTTOMRIGHT = 17;

        protected override void WndProc(ref Message m)
        {
            if (m.Msg == WM_NCHITTEST)
            {
                base.WndProc(ref m); // get default result first
                if ((int)m.Result == HTCLIENT)
                {
                    Point pos = PointToClient(Cursor.Position);
                    const int r = 8; // resize grip width

                    bool left = pos.X <= r;
                    bool right = pos.X >= ClientSize.Width - r;
                    bool top = pos.Y <= r;
                    bool bottom = pos.Y >= ClientSize.Height - r;

                    if (top && left) { m.Result = (IntPtr)HTTOPLEFT; return; }
                    if (top && right) { m.Result = (IntPtr)HTTOPRIGHT; return; }
                    if (bottom && left) { m.Result = (IntPtr)HTBOTTOMLEFT; return; }
                    if (bottom && right) { m.Result = (IntPtr)HTBOTTOMRIGHT; return; }
                    if (left) { m.Result = (IntPtr)HTLEFT; return; }
                    if (right) { m.Result = (IntPtr)HTRIGHT; return; }
                    if (top) { m.Result = (IntPtr)HTTOP; return; }
                    if (bottom) { m.Result = (IntPtr)HTBOTTOM; return; }
                }
                return;
            }
            base.WndProc(ref m);
        }

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
            // Set Form Border Style to None for custom Title Bar
            this.FormBorderStyle = FormBorderStyle.None;
            this.DoubleBuffered = true;
            this.SetStyle(ControlStyles.ResizeRedraw, true);

            // Server URL comes from the API client
            _txtServerUrl.Text = _client.BaseUrl;

            WireEvents();
            WireResizeHandlers();
            WireWindowTitleBar();

            this.Paint += (s, e) =>
            {
                if (WindowState != FormWindowState.Maximized)
                {
                    using var pen = new Pen(_BorderColor, 1);
                    e.Graphics.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
                }
            };

            // Run initial layout passes
            TopBarResize(_topBar, EventArgs.Empty);
            EffectSubPanelResize(_effectSubPanel, EventArgs.Empty);
            // Establish initial bottom-bar state: BBox active, prompt hidden
            SetCanvasMode(CanvasMode.BoundingBox);

            // Auto-connect to server on startup
            _ = StartServerAsync();
        }

        private void WireWindowTitleBar()
        {
            _chromeButtons.CloseClicked += (s, e) => Application.Exit();
            _chromeButtons.MaximizeClicked += (s, e) =>
            {
                WindowState = WindowState == FormWindowState.Maximized
                    ? FormWindowState.Normal
                    : FormWindowState.Maximized;
            };
            _chromeButtons.MinimizeClicked += (s, e) => WindowState = FormWindowState.Minimized;

            _windowTitleBar.MouseDown += (s, e) =>
            {
                if (e.Button == MouseButtons.Left)
                {
                    ReleaseCapture();
                    SendMessage(Handle, 0xA1, 0x2, 0);
                }
            };

            _windowTitleBar.DoubleClick += (s, e) =>
            {
                WindowState = WindowState == FormWindowState.Maximized
                    ? FormWindowState.Normal
                    : FormWindowState.Maximized;
            };
        }

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool ReleaseCapture();
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern int SendMessage(IntPtr hWnd, int Msg, int wParam, int lParam);

        // ═════════════════════════════════════════════════════════════════════
        //  Event Wiring
        // ═════════════════════════════════════════════════════════════════════

        private void WireEvents()
        {
            // Image loading / mask management
            _btnChangeImage.Click += (s, e) => OpenImageFile();
            _btnClearMasks.Click += (s, e) => ClearAllMasks();
            _btnToggleRight.Click += (s, e) => ToggleRightPanel();
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
            _btnResetEffect.Click += (s, e) => ResetCurrentEffect();

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
            _btnArtPencil.Click += (s, e) => SetArtMode(stylize: false);

            // ── Sticker ───────────────────────────────────────────────────────
            WireSlider(_stScale);
            WireSlider(_stRotation);
            WireSlider(_stThickness);
            WireSlider(_stShadowBlur);
            _stBorderColor.ColorChanged += (s, e) => TriggerLivePreview();

            _cmbStBgMode.SelectedIndexChanged += (s, e) =>
            {
                string mode = _cmbStBgMode.SelectedIndex switch
                {
                    1 => "Solid",
                    2 => "Image",
                    3 => "Transparent",
                    _ => "Original"
                };
                SetStickerBgMode(mode);
            };
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
                TitleBarChipsResize(_windowTitleBar, EventArgs.Empty);
            });

            // Keep layouts correct on every resize
            Resize += (s, e) =>
            {
                TopBarResize(_topBar, EventArgs.Empty);
                if (_effectSubPanel.Visible)
                    EffectSubPanelResize(_effectSubPanel, EventArgs.Empty);
                TitleBarChipsResize(_windowTitleBar, EventArgs.Empty);
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
                _preEffectSnapshot?.Dispose();
                _preEffectSnapshot = null;
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

        private void ToggleRightPanel()
        {
            _rightPanelExpanded = !_rightPanelExpanded;
            _rightPanel.Width = _rightPanelExpanded ? RightPanelFullWidth : RightPanelCollapsedWidth;
            _maskListTitle.Visible = _rightPanelExpanded;
            _maskList.Visible = _rightPanelExpanded;
            _btnClearMasks.Visible = _rightPanelExpanded;
            _btnToggleRight.Text = _rightPanelExpanded ? "›" : "‹";
            RightPanelResize(_rightPanel, EventArgs.Empty);
        }

        private void RightPanelResize(object sender, EventArgs e)
        {
            // Toggle strip always fills the full left edge, ignoring panel padding
            _btnToggleRight.Location = new Point(0, 0);
            _btnToggleRight.Size = new Size(26, _rightPanel.Height);
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
                    string prompt = _promptBox!.Text.Trim();
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
                _canvas.ClearBoxes();
                _maskList.Populate(_canvas.MaskColors, _canvas.MaskScores);
                _rightPanelExpanded = true;
                _rightPanel.Width = RightPanelFullWidth;
                _rightPanel.Visible = true;
                _btnToggleRight.Text = "›";
                _maskListTitle.Visible = true;
                _maskList.Visible = true;
                _btnClearMasks.Visible = true;
                RightPanelResize(_rightPanel, EventArgs.Empty);
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
            _btnArtPencil.BackColor = stylize ? _BgButton : _Cyan;
            _btnArtPencil.ForeColor = stylize ? _TextMain : _BgMain;
            _grpArtSigmaR.Visible = stylize;
            _grpArtShade.Visible = !stylize;
            TriggerLivePreview();
        }

        private void SetStickerBgMode(string mode)
        {
            _stickerBgMode = mode;
            // Sync dropdown selection without re-firing the event
            int idx = mode switch { "Solid" => 1, "Image" => 2, "Transparent" => 3, _ => 0 };
            if (_cmbStBgMode.SelectedIndex != idx)
                _cmbStBgMode.SelectedIndex = idx;
            // Hide both side controls first so layout runs with nothing visible
            _stBgColorSwatch.Visible = false;
            _btnStickerUploadBg.Visible = false;
            // Re-run layout to size the combo to full width before showing anything
            EffectSubPanelResize(_effectSubPanel, EventArgs.Empty);
            // Now show whichever control belongs to this mode (already correctly positioned)
            _stBgColorSwatch.Visible = mode == "Solid";
            _btnStickerUploadBg.Visible = mode == "Image";
            TriggerLivePreview();
        }

        private void SetCgMode(bool targetBg)
        {
            _cgTargetBgMode = targetBg;
            _btnCgFg.BackColor = !targetBg ? _Cyan : _BgButton;
            _btnCgFg.ForeColor = !targetBg ? _BgMain : _TextMain;
            _btnCgBg.BackColor = targetBg ? _Cyan : _BgButton;
            _btnCgBg.ForeColor = targetBg ? _BgMain : _TextMain;
            TriggerLivePreview();
        }

        private void SetGsMode(bool targetBg)
        {
            _gsTargetBgMode = targetBg;
            _btnGsFg.BackColor = !targetBg ? _Cyan : _BgButton;
            _btnGsFg.ForeColor = !targetBg ? _BgMain : _TextMain;
            _btnGsBg.BackColor = targetBg ? _Cyan : _BgButton;
            _btnGsBg.ForeColor = targetBg ? _BgMain : _TextMain;
            TriggerLivePreview();
        }

        private void DeactivateEffect()
        {
            // Cancel any in-flight preview task first
            _previewCts?.Cancel();
            _previewCts?.Dispose();
            _previewCts = null;

            _activeEffect = "";
            _preEffectSnapshot?.Dispose();
            _preEffectSnapshot = null;
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
            _effectSubPanel.Visible = false;

            // Clear the live preview and any transformed mask override
            _canvas.SetProcessedBitmap(null);
            _canvas.SetDisplayMaskOverride(null);
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

            bool showButtons = activePanel != null;

            // Show effect bar below image FIRST so the panel has a valid width for layout
            _effectSubPanel.Visible = showButtons;

            // Show Apply/Reset buttons for all real effects
            _btnApplyEffect.Visible = showButtons;
            _btnResetEffect.Visible = showButtons;

            // Re-run proportional layout now that panel is visible and has correct width
            if (showButtons)
            {
                _effectSubPanel.PerformLayout();
                EffectSubPanelResize(_effectSubPanel, EventArgs.Empty);
                // Deferred second pass in case WinForms finalises the size asynchronously
                BeginInvoke(() => EffectSubPanelResize(_effectSubPanel, EventArgs.Empty));
            }

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
                    _cgBrightness.Value = 0;
                    _cgContrast.Value = 10;
                    SetCgMode(targetBg: false);
                    break;
                case "Artistic":
                    _artSigmaS.Value = 60;
                    _artSigmaR.Value = 45;
                    _artShade.Value = 5;
                    SetArtMode(stylize: true);
                    break;
                case "Sticker":
                    _stScale.Value = 10;
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
                    _ptBlurStrength.Value = 51;
                    _ptFeatherAmount.Value = 21;
                    break;
                case "Grayscale":
                    SetGsMode(targetBg: false);
                    break;
            }
            TriggerLivePreview();
        }

        private async void ApplyCurrentEffect()
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

            // Snapshot everything needed on the UI thread before going async
            var effect = _activeEffect;
            var args = CaptureEffectArgs();
            var srcFull = (Bitmap)_canvas.OriginalBitmap.Clone();

            // For Artistic: sigmaS was tuned at preview resolution (≤900px longest side).
            // Scale it up proportionally so the full-res apply matches the preview look.
            if (effect == "Artistic")
            {
                int longestSide = Math.Max(srcFull.Width, srcFull.Height);
                if (longestSide > PreviewMaxDim)
                    args = args with { ArtSigmaSScale = (float)longestSide / PreviewMaxDim };
            }

            // Portrait and PixelBlur: kernel/block sizes were tuned at preview resolution.
            // Scale them up so the full-res apply matches the preview look.
            if (effect == "Portrait" || effect == "PixelBlur")
            {
                int longestSide = Math.Max(srcFull.Width, srcFull.Height);
                if (longestSide > PreviewMaxDim)
                {
                    float scale = (float)longestSide / PreviewMaxDim;
                    if (effect == "Portrait")
                        args = args with { PortraitScale = scale };
                    else
                        args = args with { PbScale = scale };
                }
            }

            // Disable apply button and show the loading overlay while processing
            _btnApplyEffect.Enabled = false;
            SetLoading(true, "Applying effect...");

            Bitmap result;
            try
            {
                result = await Task.Run(() =>
                {
                    Bitmap current = (Bitmap)srcFull.Clone();
                    foreach (var mask in selectedMasks)
                    {
                        Bitmap? next = ApplyEffectArgs(effect, current, mask, args);
                        if (next != null)
                        {
                            current.Dispose();
                            current = next;
                        }
                    }
                    return current;
                });
            }
            finally
            {
                srcFull.Dispose();
                SetLoading(false);
                _btnApplyEffect.Enabled = true;
            }

            // Back on the UI thread — save pre-commit snapshot for sticker, then commit
            if (_activeEffect == "Sticker")
            {
                _preEffectSnapshot?.Dispose();
                _preEffectSnapshot = (Bitmap)_canvas.OriginalBitmap.Clone();
            }
            else
            {
                // Non-sticker apply invalidates any sticker snapshot (chaining scenario)
                _preEffectSnapshot?.Dispose();
                _preEffectSnapshot = null;
            }

            _canvas.CommitProcessedAsOriginal(result);
            result.Dispose();

            // Bake the sticker transform into the stored masks so the overlay
            // contour permanently matches the new subject position.
            if (_activeEffect == "Sticker")
            {
                float scale = args.StScale / 10f;
                float rot = args.StRotation;
                int fw = _canvas.OriginalBitmap!.Width;
                int fh = _canvas.OriginalBitmap!.Height;
                var transformedMasks = _canvas.Masks
                    .Select(m => Processing.ImageEffects.TransformMaskForDisplay(
                        m, fw, fh, scale, rot))
                    .ToList();
                _canvas.ReplaceMasks(transformedMasks);
            }

            _appliedEffects.Add(GetEffectDisplayName(_activeEffect));
            RebuildAppliedEffectsChips();

            // Deactivate so the effect isn't re-applied on top of the committed result
            DeactivateEffect();
        }

        // ═════════════════════════════════════════════════════════════════════
        //  Logic – Async debounced preview
        // ═════════════════════════════════════════════════════════════════════

        // Effects that are computationally heavy — use a downscaled image for preview
        private static readonly HashSet<string> _heavyEffects = new() { "Artistic", "Sticker", "Portrait", "PixelBlur" };
        private const int PreviewMaxDim = 900;
        private const int DebounceMs = 30;
        private const int DebounceHeavyMs = 200;

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
            var srcBase = (_activeEffect == "Sticker" && _preEffectSnapshot != null)
                ? _preEffectSnapshot
                : _canvas.OriginalBitmap;
            var srcFull = (Bitmap)srcBase.Clone();
            var args = CaptureEffectArgs();

            bool heavy = _heavyEffects.Contains(effect);
            int debounce = heavy ? DebounceHeavyMs : DebounceMs;

            _ = Task.Run(async () =>
            {
                try
                {
                    await Task.Delay(debounce, token);
                    token.ThrowIfCancellationRequested();

                    // Wait for any in-flight render to finish — semaphore releases immediately
                    // when the previous render completes, no polling required
                    await _previewSem.WaitAsync(token);
                    try
                    {
                        token.ThrowIfCancellationRequested();

                        Bitmap src = heavy ? ScaleForPreview(srcFull, PreviewMaxDim) : srcFull;

                        // For Sticker previews: scale border thickness to match the
                        // preview resolution so it looks the same as at full resolution.
                        var renderArgs = (effect == "Sticker" && !ReferenceEquals(src, srcFull))
                            ? args with { StThicknessScale = (float)src.Width / srcFull.Width }
                            : args;

                        Bitmap current = (Bitmap)src.Clone();

                        foreach (var mask in selectedMasks)
                        {
                            token.ThrowIfCancellationRequested();
                            Bitmap? next = ApplyEffectArgs(effect, current, mask, renderArgs);
                            if (next != null)
                            {
                                if (!ReferenceEquals(current, src)) current.Dispose();
                                current = next;
                            }
                        }

                        if (heavy && !ReferenceEquals(src, srcFull)) src.Dispose();
                        token.ThrowIfCancellationRequested();

                        // For Sticker: transform the selected masks to match the
                        // scale+rotation applied to the subject, so the overlay contour
                        // stays in sync with the transformed pixels.
                        List<float[,]>? transformedMasks = null;
                        if (effect == "Sticker")
                        {
                            float scale = args.StScale / 10f;
                            float rot = args.StRotation;
                            int fw = srcFull.Width;
                            int fh = srcFull.Height;
                            transformedMasks = selectedMasks
                                .Select(m => Processing.ImageEffects.TransformMaskForDisplay(
                                    m, fw, fh, scale, rot, previewMaxDim: 256))
                                .ToList();
                        }

                        Invoke(() =>
                        {
                            _canvas.SetProcessedBitmap(current);
                            current.Dispose();
                            _canvas.SetDisplayMaskOverride(transformedMasks);
                        });
                    }
                    finally { _previewSem.Release(); }
                }
                catch (OperationCanceledException) { /* stale request — discard */ }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Preview error: {ex.Message}");
                    if (_previewSem.CurrentCount == 0) _previewSem.Release();
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
            int w = Math.Max(1, (int)(src.Width * scale));
            int h = Math.Max(1, (int)(src.Height * scale));
            var bmp = new Bitmap(w, h, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
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
            float ArtSigmaSScale,
            int StScale, int StRotation, Color StBorderColor, int StThickness, int StShadowBlur,
            float StThicknessScale,
            bool StickerOriginalBg, bool StickerSolidBg, Color StickerSolidColor,
            Bitmap? StickerImageBg, bool StickerTransparentBg,
            bool PixelateMode, bool PbTargetBg, int PbIntensity, float PbScale,
            int PortraitBlur, int PortraitFeather, float PortraitScale,
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
            ArtSigmaSScale: 1f,
            StScale: _stScale.Value,
            StRotation: _stRotation.Value,
            StBorderColor: _stBorderColor.SelectedColor,
            StThickness: _stThickness.Value,
            StShadowBlur: _stShadowBlur.Value,
            StThicknessScale: 1f,
            StickerOriginalBg: _stickerBgMode == "Original",
            StickerSolidBg: _stickerBgMode == "Solid",
            StickerSolidColor: _stBgColorSwatch.SelectedColor,
            StickerImageBg: _stickerCustomBg != null ? (Bitmap)_stickerCustomBg.Clone() : null,
            StickerTransparentBg: _stickerBgMode == "Transparent",
            PixelateMode: _pixelateMode,
            PbTargetBg: _pbTargetBg,
            PbIntensity: _pbIntensity.Value,
            PbScale: 1f,
            PortraitBlur: _ptBlurStrength.Value,
            PortraitFeather: _ptFeatherAmount.Value,
            PortraitScale: 1f,
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
                    ? ImageEffects.StylizeMasked(image, mask,
                        Math.Clamp((int)Math.Round(a.ArtSigmaS * a.ArtSigmaSScale), 1, 200),
                        a.ArtSigmaR)
                    : ImageEffects.PencilSketchMasked(image, mask,
                        Math.Clamp((int)Math.Round(a.ArtSigmaS * a.ArtSigmaSScale), 1, 200),
                        a.ArtShade),

                "Sticker" => ApplyStickerEffect(image, mask, a),

                "PixelBlur" => ApplyPixelBlur(image, mask, a),

                "Portrait" => ImageEffects.PortraitEffect(
                    image, mask,
                    Math.Clamp((int)Math.Round(a.PortraitBlur * a.PortraitScale), 3, 501),
                    Math.Clamp((int)Math.Round(a.PortraitFeather * a.PortraitScale), 0, 501)),

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
            int scaledThickness = Math.Max(1, (int)Math.Round(a.StThickness * a.StThicknessScale));
            Bitmap sticker = ImageEffects.ExtractSticker(
                image, mask,
                threshold: 0.5f,
                contourThickness: scaledThickness,
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
            {
                // Resize uploaded background to exactly match source image dimensions
                // so CompositeSticker always works on matching canvas sizes
                bg = new Bitmap(image.Width, image.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
                using var g2 = Graphics.FromImage(bg);
                g2.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g2.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
                g2.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g2.DrawImage(a.StickerImageBg, 0, 0, image.Width, image.Height);
            }
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
                int pixelSize = Math.Max(2, (int)Math.Round(intensity * 64 / 100f * a.PbScale));
                return ImageEffects.PixelateMasked(image, workMask, pixelSize);
            }
            else
            {
                int k = Math.Max(3, (int)Math.Round(intensity * 51 / 100f * a.PbScale));
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
            // Dispose existing chip controls before clearing (they own Font objects)
            foreach (Control c in _appliedEffectsPanel.Controls)
                c.Dispose();
            _appliedEffectsPanel.Controls.Clear();

            if (_appliedEffects.Count == 0)
            {
                _appliedEffectsPanel.Visible = false;
                _btnResetAll.Visible = false;
                return;
            }

            var chipFont = new Font("Segoe UI", 9f, FontStyle.Bold);
            foreach (var name in _appliedEffects)
            {
                int textW = TextRenderer.MeasureText(name, chipFont).Width;
                var chip = new VisionEditCV.Controls.ChipLabel
                {
                    Text = name,
                    Font = chipFont,
                    Size = new Size(textW + 28, 32),
                    Margin = new Padding(0, 0, 6, 0),
                    CornerRadius = 10,
                };
                _appliedEffectsPanel.Controls.Add(chip);
            }

            // Defer to after the message pump so the title bar has its final
            // ClientSize, and the FlowLayoutPanel has completed its own layout
            // pass — otherwise chips can appear at (0,0) or stay invisible.
            BeginInvoke(() =>
            {
                TitleBarChipsResize(_windowTitleBar, EventArgs.Empty);
                _appliedEffectsPanel.Visible = true;
                _btnResetAll.Visible = true;
                // Ensure the chips panel and button sit above all other title-bar
                // children (dock layout can silently push non-docked controls under
                // docked siblings during the initial layout pass).
                _appliedEffectsPanel.BringToFront();
                _btnResetAll.BringToFront();
                _windowTitleBar.Refresh();
            });
        }

        private void ResetAllEffects()
        {
            // Cancel any in-flight preview before touching bitmaps
            _previewCts?.Cancel();
            _previewCts?.Dispose();
            _previewCts = null;

            _appliedEffects.Clear();
            _preEffectSnapshot?.Dispose();
            _preEffectSnapshot = null;
            RebuildAppliedEffectsChips();

            // Reload the original image from disk without clearing masks or zoom
            if (_currentImagePath != null)
            {
                _canvas.RestoreOriginalFromFile(_currentImagePath);
                _canvas.RestoreOriginalMasks();
                _canvas.SetDisplayMaskOverride(null);
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

        private void SetLoading(bool loading, string message = "Segmenting... (server may take 6–7 min to start)")
        {
            if (InvokeRequired) { Invoke(() => SetLoading(loading, message)); return; }
            _loadingLabel.Text = message;
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

            // Re-run top bar layout so the button resizes to fit its new text
            TopBarResize(_topBar, EventArgs.Empty);
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
            if (panel.Width <= 0 || panel.Height <= 0) return;

            const int pad = 14;
            const int gap = 16; // gap between groups
            const int bGap = 4;
            const int minBtnW = 60;
            const int minSlidW = 120;

            // ── Apply / Reset — fixed right margin, stacked vertically ──────────
            int cy = panel.Height / 2;
            const int applyW = 68;
            const int resetW = 68;
            const int btnGap = 6;
            const int btnH = 40;
            // Reserve a fixed column of 96px on the right for the two buttons
            const int btnColW = 96;
            int stackH = btnH * 2 + btnGap;
            _btnApplyEffect.Width = applyW;
            _btnApplyEffect.Height = btnH;
            _btnResetEffect.Width = resetW;
            _btnResetEffect.Height = btnH;
            // Centre the buttons within the reserved column
            int btnLeft = panel.Width - btnColW + (btnColW - applyW) / 2;
            _btnApplyEffect.Left = btnLeft;
            _btnResetEffect.Left = btnLeft;
            _btnApplyEffect.Top = cy - stackH / 2;
            _btnResetEffect.Top = _btnApplyEffect.Top + btnH + btnGap;

            int availH = panel.Height - panel.Padding.Vertical;
            // Flow fills everything to the left of the reserved button column
            int flowW = panel.Width - btnColW - pad * 2;

            foreach (var flow in new FlowLayoutPanel[] { _cgFlow, _artFlow, _stFlow, _pbFlow, _ptFlow, _gsFlow })
            {
                flow.FlowDirection = FlowDirection.LeftToRight;
                flow.WrapContents = false;
                flow.AutoScroll = false;
                flow.HorizontalScroll.Enabled = false;
                flow.VerticalScroll.Enabled = false;
                flow.Height = availH;
                flow.Padding = new Padding(0);

                var groups = flow.Controls.Cast<Control>().Where(c => c.Visible).ToList();
                int count = groups.Count;
                if (count == 0) continue;

                // ── Natural width per group ──────────────────────────────────────
                int[] natural = groups.Select(grp =>
                {
                    var vb = grp.Controls.OfType<DarkButton>().Where(b => b.Visible).ToList();
                    var cmb = grp.Controls.OfType<ComboBox>().FirstOrDefault();
                    if (cmb != null)
                    {
                        // Measure widest item text + left padding (10) + arrow zone (26) + margin
                        int maxTextW = cmb.Items.Count > 0
                            ? cmb.Items.Cast<object>()
                                .Max(it => TextRenderer.MeasureText(it?.ToString() ?? "", cmb.Font).Width)
                            : 60;
                        int cmbNatural = maxTextW + 10 + 26 + 8; // text + left-pad + arrow + right-margin
                        // Always reserve side space if the group has a swatch OR upload btn (either may be hidden)
                        var hasSideCtrl = grp.Controls.OfType<ColorSwatch>().Any()
                                       || grp.Controls.OfType<DarkButton>().Any(b => b.Name == "_btnStickerUploadBg");
                        int extraW = hasSideCtrl ? 8 + 34 : 0;
                        return cmbNatural + extraW;
                    }
                    if (vb.Count == 0) return minSlidW;
                    int cols = vb.Count > 2 ? (vb.Count + 1) / 2 : vb.Count;
                    return cols * minBtnW + (cols - 1) * bGap;
                }).ToArray();

                int totalUsed = natural.Sum() + gap * (count - 1);
                int surplus = Math.Max(0, flowW - totalUsed);
                int extra = surplus / count;

                flow.Left = pad;
                flow.Top = panel.Padding.Top;
                flow.Width = flowW;

                flow.SuspendLayout();
                for (int i = 0; i < groups.Count; i++)
                {
                    var grp = groups[i];
                    // Don't stretch combo groups — the side button must stay within bounds
                    bool isComboGroup = grp.Controls.OfType<ComboBox>().Any();
                    int grpW = natural[i] + (isComboGroup ? 0 : extra);
                    grp.Width = grpW;
                    grp.Height = availH;
                    grp.Margin = new Padding(0, 0, i < count - 1 ? gap : 0, 0);

                    // ── Measure content height to centre vertically ──────────────
                    var lbl = grp.Controls.OfType<Label>().FirstOrDefault();
                    var slider = grp.Controls.OfType<SliderControl>().FirstOrDefault();
                    var combo = grp.Controls.OfType<ComboBox>().FirstOrDefault();
                    var visBtns = grp.Controls.OfType<DarkButton>()
                                      .Where(b => b.Visible && b.Name != "_btnStickerUploadBg")
                                      .OrderBy(b => b.Left).ToList();
                    var swatch = grp.Controls.OfType<ColorSwatch>().FirstOrDefault(c => c.Visible);

                    int lblH = lbl != null ? lbl.Height : 0;
                    int slidH = slider != null ? slider.Height : 0;
                    int comboH = combo != null ? combo.Height : 0;
                    int swatchH = swatch != null ? swatch.Height : 0;

                    int cols = visBtns.Count > 2 ? (visBtns.Count + 1) / 2 : visBtns.Count;
                    int btnRows = visBtns.Count > 0 ? (visBtns.Count + cols - 1) / cols : 0;
                    int btnBlockH = btnRows > 0
                        ? btnRows * visBtns[0].Height + (btnRows - 1) * bGap
                        : 0;

                    int innerContentH = Math.Max(slidH, Math.Max(btnBlockH, Math.Max(comboH, swatchH)));
                    // Total content height: label + gap + (slider | buttons | combo | swatch)
                    int innerH = lblH + (lblH > 0 && innerContentH > 0 ? 4 : 0) + innerContentH;
                    int topOff = Math.Max(0, (availH - innerH) / 2);

                    // Position label
                    if (lbl != null)
                        lbl.Top = topOff;

                    int contentY = topOff + lblH + (lblH > 0 ? 4 : 0);

                    // Slider
                    if (slider != null)
                    {
                        slider.Width = grpW - 2;
                        slider.Left = 0;
                        slider.Top = contentY;
                    }

                    // ComboBox — positioned below label; swatch/upload share the same slot to the right
                    if (combo != null)
                    {
                        // Always reserve side space if either control exists (visible or not)
                        var uploadBtn2 = grp.Controls.OfType<DarkButton>().FirstOrDefault(b => b.Name == "_btnStickerUploadBg");
                        var swatchAny = grp.Controls.OfType<ColorSwatch>().FirstOrDefault();
                        bool hasSide = swatchAny != null || uploadBtn2 != null;
                        int rightReserve = hasSide ? 8 + 34 : 0;
                        combo.Width = Math.Max(60, grpW - rightReserve);
                        combo.Left = 0;
                        combo.Top = contentY;
                        // Position swatch (always, visible or not, so it's ready when shown)
                        if (swatchAny != null)
                        {
                            swatchAny.Left = combo.Width + 8;
                            swatchAny.Top = contentY + (combo.Height - swatchAny.Height) / 2;
                        }
                        // Position upload button (always, visible or not)
                        if (uploadBtn2 != null)
                        {
                            uploadBtn2.Left = combo.Width + 8;
                            uploadBtn2.Top = contentY + (combo.Height - uploadBtn2.Height) / 2;
                        }
                    }
                    else
                    {
                        // ColorSwatch (no combo)
                        if (swatch != null)
                        {
                            swatch.Left = 0;
                            swatch.Top = contentY;
                        }
                    }

                    // Buttons (2-row wrap when count > 2)
                    if (visBtns.Count > 0)
                    {
                        int bW = Math.Max(40, (grpW - bGap * (cols - 1)) / cols);
                        for (int bi = 0; bi < visBtns.Count; bi++)
                        {
                            int col = bi % cols;
                            int row = bi / cols;
                            visBtns[bi].Width = bW;
                            visBtns[bi].Left = col * (bW + bGap);
                            visBtns[bi].Top = contentY + row * (visBtns[bi].Height + bGap);
                        }
                    }
                }
                flow.ResumeLayout(performLayout: true);
            }
        }

        private void EffectSubPanelPaint(object sender, PaintEventArgs e)
        {
            using var pen = new Pen(Color.FromArgb(55, 58, 70), 1);
            e.Graphics.DrawLine(pen, 0, 0, ((Control)sender).Width, 0);
        }

        private void TitleBarChipsResize(object? sender, EventArgs e)
        {
            if (sender is not Panel bar) return;
            // Run even when hidden — positions must be correct before first show.
            if (_appliedEffects.Count == 0) return;

            // Chips sit in the title bar just after the app title label (DockStyle.Left).
            // Read its actual width at runtime so the chips never overlap the title text.
            // Window chrome buttons (─ 🗖 ✕) are DockStyle.Right — sum their widths to find
            // their left edge reliably (c.Left can be stale before dock layout settles).
            const int padL = 12;
            const int gap = 8;
            const int padR = 24; // breathing room before nav buttons
            const int chipH = 32;

            int titleW = _lblWindowTitle.Width;  // real width, respects runtime layout

            int dockRightW = 0;
            foreach (Control c in bar.Controls)
                if (c.Dock == DockStyle.Right)
                    dockRightW += c.Width;

            int rightEdge = bar.ClientSize.Width - dockRightW - padR;
            int x = titleW + padL;
            int cy = bar.ClientSize.Height / 2;

            // Chips flow panel — explicit height so centering works
            int chipsW = Math.Max(0, rightEdge - x - gap - _btnResetAll.Width);
            _appliedEffectsPanel.Width = chipsW;
            _appliedEffectsPanel.Height = chipH;
            _appliedEffectsPanel.Left = x;
            _appliedEffectsPanel.Top = cy - chipH / 2;

            // Reset All button — immediately right of chips
            _btnResetAll.Left = x + chipsW + gap;
            _btnResetAll.Top = cy - _btnResetAll.Height / 2;
        }

        private void TitleBarPaint(object sender, PaintEventArgs e)
        {
            if (sender is not Control bar) return;
            // Single cyan accent line at the bottom — the only decoration
            using var pen = new Pen(Color.FromArgb(0, 160, 180), 1);
            e.Graphics.DrawLine(pen, 0, bar.Height - 1, bar.Width, bar.Height - 1);
        }

        private void TopBarResize(object sender, EventArgs e)
        {
            if (sender is not Panel bar) return;
            int cy = bar.ClientSize.Height / 2;
            int w = bar.ClientSize.Width;
            int pad = 24;
            int gap = 10;

            // ── Left group ────────────────────────────────────────────────────
            int x = pad;
            _lblSelMode.Left = x;
            _lblSelMode.Width = 36;
            x += _lblSelMode.Width + gap;

            _btnBBox.Left = x;
            _btnBBox.Width = TextRenderer.MeasureText(_btnBBox.Text, _btnBBox.Font).Width + 32;
            x += _btnBBox.Width + gap;

            _btnPrompt.Left = x;
            _btnPrompt.Width = TextRenderer.MeasureText(_btnPrompt.Text, _btnPrompt.Font).Width + 32;
            x += _btnPrompt.Width + pad;

            int leftEdge = x;

            // ── Right group — size buttons to their current text ─────────────
            _btnStartServer.Width = TextRenderer.MeasureText(_btnStartServer.Text, _btnStartServer.Font).Width + 72;
            _btnStartServer.Left = w - pad - _btnStartServer.Width;

            _btnSegment.Width = TextRenderer.MeasureText(_btnSegment.Text, _btnSegment.Font).Width + 72;
            _btnSegment.Left = _btnStartServer.Left - gap - _btnSegment.Width;

            int rightEdge = _btnSegment.Left - pad;

            // ── Prompt box stretches between the two groups ───────────────────
            _promptBox.Left = leftEdge;
            _promptBox.Width = Math.Max(120, rightEdge - leftEdge);

            // ── Vertically center all controls ────────────────────────────────
            foreach (Control c in bar.Controls)
            {
                if (c == _serverPanel) continue;
                c.Top = cy - c.Height / 2;
            }
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

        private void _btnColorGrading_Click(object sender, EventArgs e)
        {

        }
    }
}
