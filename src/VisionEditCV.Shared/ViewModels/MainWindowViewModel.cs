using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Avalonia.Media.Imaging;
using VisionEditCV.Api;
using VisionEditCV.Models;
using VisionEditCV.Shared.Models;
using VisionEditCV.Shared.Controls;
using VisionEditCV.Shared.Helpers;
using VisionEditCV.Processing;
using Emgu.CV;
using Emgu.CV.CvEnum;
using Emgu.CV.Structure;
using Avalonia.Media;
using Velopack;
using Velopack.Sources;

namespace VisionEditCV.Shared.ViewModels;

public partial class MaskItemViewModel : ObservableObject
{
    [ObservableProperty] private string _name = "";
    [ObservableProperty] private Color _color;
    [ObservableProperty] private bool _isVisible = true;
}

public partial class MainWindowViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _serverUrl = "https://8000-dep-01khgcb8hf1kcdc87pbkv4bfz1-d.cloudspaces.litng.ai";

    [ObservableProperty]
    private string _statusMessage = "Disconnected";

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionButtonText))]
    [NotifyPropertyChangedFor(nameof(IsConnectionIdle))]
    [NotifyPropertyChangedFor(nameof(CanRunConnectionCheck))]
    [NotifyPropertyChangedFor(nameof(CanRunSegment))]
    private bool _isCheckingConnection;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRunSegment))]
    [NotifyPropertyChangedFor(nameof(CanApplyEffects))]
    [NotifyPropertyChangedFor(nameof(CanSaveImage))]
    [NotifyPropertyChangedFor(nameof(CanRunConnectionCheck))]
    [NotifyPropertyChangedFor(nameof(SegmentButtonText))]
    private bool _isLoading;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ImageFileName))]
    private string? _currentImagePath;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ImageDimensionsText))]
    [NotifyPropertyChangedFor(nameof(HasImage))]
    [NotifyPropertyChangedFor(nameof(CanSegment))]
    private Bitmap? _currentImage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasProcessedImage))]
    private Bitmap? _processedImage;

    public string ImageFileName => string.IsNullOrEmpty(CurrentImagePath) ? "No image loaded" : Path.GetFileName(CurrentImagePath)!;
    public string ImageDimensionsText => CurrentImage is null ? "—" : $"{CurrentImage.PixelSize.Width} × {CurrentImage.PixelSize.Height}";
    public bool HasImage => CurrentImage is not null;
    public bool HasProcessedImage => ProcessedImage is not null;
    public bool CanSaveImage => HasProcessedImage && !IsLoading;
    public bool CanApplyEffects => HasImage && Masks.Count > 0 && !IsLoading && !string.IsNullOrWhiteSpace(SelectedEffect);
    public bool CanUndoEffect => _history.Count > 0 || HasProcessedImage;
    public bool CanClearWorkspace => Boxes.Count > 0 || Masks.Count > 0 || HasProcessedImage;
    public bool CanRunConnectionCheck => IsConnectionIdle && !IsLoading;
    public string SegmentButtonText => IsLoading ? "PROCESSING..." : "SEGMENT";

    public bool CanSegment =>
        HasImage &&
        ((SelectedTool == "Bounding Box" && Boxes.Count > 0) ||
         (SelectedTool == "Prompt" && !string.IsNullOrWhiteSpace(PromptText)));
    public bool CanRunSegment => CanSegment && IsConnected && !IsLoading;

    public bool StShowSolidBackground => StBackgroundMode == 1;
    public bool StShowImageBackground => StBackgroundMode == 2;
    public string StickerBackgroundFileName =>
        string.IsNullOrEmpty(StBackgroundImagePath) ? "No file chosen" : Path.GetFileName(StBackgroundImagePath)!;

    [ObservableProperty]
    private CanvasMode _canvasMode = CanvasMode.BBox;

    [ObservableProperty]
    private ObservableCollection<BBoxEntry> _boxes = new();

    [ObservableProperty]
    private List<float[,]> _masks = new();

    [ObservableProperty]
    private ObservableCollection<MaskItemViewModel> _maskItems = new();

    [ObservableProperty]
    private ObservableCollection<bool> _maskVisibilityStates = new();

    [ObservableProperty]
    private int _selectedMaskIndex = -1;

    [ObservableProperty]
    private string? _selectedEffect;

    [ObservableProperty]
    private string _promptText = "";

    [ObservableProperty]
    private string _selectedTool = "Bounding Box";

    [ObservableProperty]
    private bool _isCompareMode;

    [ObservableProperty]
    private bool _areMasksHidden;

    [ObservableProperty]
    private bool _isMaskPanelVisible = true;

    [ObservableProperty]
    private bool _isRightPanelExpanded = true;

    [ObservableProperty]
    private int _rightPanelTabIndex;

    [ObservableProperty]
    private bool _isLeftPanelExpanded = true;

    public bool IsAdjustTab => RightPanelTabIndex == 0;
    public bool IsLayersTab => RightPanelTabIndex == 1;
    public bool IsHistoryTab => RightPanelTabIndex == 2;
    public bool IsServerTab => RightPanelTabIndex == 3;
    public bool HasMasks => MaskItems.Count > 0;
    public bool HasHistory => History.Count > 0;

    public string RightPanelSubtitle => RightPanelTabIndex switch
    {
        0 => SelectedEffect ?? "No effect selected",
        1 => MaskItems.Count == 0 ? "No masks yet" : $"{MaskItems.Count} masks",
        2 => History.Count == 0 ? "No effects applied" : $"{History.Count} step{(History.Count == 1 ? "" : "s")}",
        3 => IsConnected ? "Endpoint reachable" : "Set segmentation API URL",
        _ => ""
    };

    public string RightPanelHeaderTitle => RightPanelTabIndex switch
    {
        0 => "ADJUST",
        1 => "LAYERS",
        2 => "HISTORY",
        3 => "SERVER",
        _ => "ADJUST"
    };

    public string HideMasksButtonText => AreMasksHidden ? "Show Masks" : "Hide Masks";
    public string ConnectionButtonText => IsCheckingConnection ? "Connecting..." : IsConnected ? "Connected" : "Connect";
    public bool IsConnectionIdle => !IsCheckingConnection;

    // --- Color Grading ---
    [ObservableProperty] private int _cgBrightness = 0;
    [ObservableProperty] private double _cgContrast = 10;
    [ObservableProperty] private double _cgTintStrength = 0.0;
    [ObservableProperty] private Color _cgTintColor = Colors.Cyan;
    [ObservableProperty] private bool _cgIsForeground = true;

    // --- Artistic Style ---
    [ObservableProperty] private bool _artIsStylize = true;
    [ObservableProperty] private int _artSigmaS = 60;
    [ObservableProperty] private double _artSigmaR = 45;
    [ObservableProperty] private int _artShadeFactor = 5;

    // --- Sticker Generation ---
    [ObservableProperty] private int _stScale = 10; // x0.1
    [ObservableProperty] private int _stRotation = 0;
    [ObservableProperty] private Color _stBorderColor = Colors.Green;
    [ObservableProperty] private Color _stBackgroundColor = Colors.Black;
    [ObservableProperty] private int _stThickness = 3;
    [ObservableProperty] private int _stShadowBlur = 32;
    [ObservableProperty] private int _stBackgroundMode = 0;
    [ObservableProperty] private string? _stBackgroundImagePath;

    // --- Pixelate & Blur ---
    [ObservableProperty] private int _pbIntensity = 40;
    [ObservableProperty] private bool _pbIsPixelate = true; 
    [ObservableProperty] private bool _pbIsForeground = true;

    // --- Portrait Effect ---
    [ObservableProperty] private int _ptBlurStrength = 51;
    [ObservableProperty] private int _ptFeatherAmount = 21;

    // --- Grayscale ---
    [ObservableProperty] private bool _gsIsForeground = true;

    // Each entry is the ProcessedImage that was active before the corresponding History entry
    // was applied. null means "no processed image — original was visible".
    private readonly Stack<Bitmap?> _history = new();
    private readonly Sam3Client _client = new();
    // Guards Boxes.CollectionChanged from re-entering Segment while one is in flight.
    private bool _isAutoSegmenting;

    // --- Live Previews ---
    private System.Threading.CancellationTokenSource? _previewCts;
    private Mat? _cachedCurrentMat;
    // Stable base for the current effect's previews. Refreshed when the effect
    // changes or when an Apply commits a new stable result, so slider tweaks
    // never compound on top of an already-previewed image.
    private Mat? _previewSourceSnapshot;

    partial void OnCgBrightnessChanged(int value) => TriggerPreviewUpdate();
    partial void OnCgContrastChanged(double value) => TriggerPreviewUpdate();
    partial void OnCgTintStrengthChanged(double value) => TriggerPreviewUpdate();
    partial void OnCgTintColorChanged(Color value) => TriggerPreviewUpdate();
    partial void OnCgIsForegroundChanged(bool value) => TriggerPreviewUpdate();

    partial void OnArtIsStylizeChanged(bool value) => TriggerPreviewUpdate();
    partial void OnArtSigmaSChanged(int value) => TriggerPreviewUpdate();
    partial void OnArtSigmaRChanged(double value) => TriggerPreviewUpdate();
    partial void OnArtShadeFactorChanged(int value) => TriggerPreviewUpdate();

    partial void OnStScaleChanged(int value) => TriggerPreviewUpdate();
    partial void OnStRotationChanged(int value) => TriggerPreviewUpdate();
    partial void OnStBorderColorChanged(Color value) => TriggerPreviewUpdate();
    partial void OnStBackgroundColorChanged(Color value) => TriggerPreviewUpdate();
    partial void OnStThicknessChanged(int value) => TriggerPreviewUpdate();
    partial void OnStShadowBlurChanged(int value) => TriggerPreviewUpdate();
    partial void OnStBackgroundModeChanged(int value)
    {
        OnPropertyChanged(nameof(StShowSolidBackground));
        OnPropertyChanged(nameof(StShowImageBackground));
        TriggerPreviewUpdate();
    }

    partial void OnPbIntensityChanged(int value) => TriggerPreviewUpdate();
    partial void OnPbIsPixelateChanged(bool value) => TriggerPreviewUpdate();
    partial void OnPbIsForegroundChanged(bool value) => TriggerPreviewUpdate();

    partial void OnPtBlurStrengthChanged(int value) => TriggerPreviewUpdate();
    partial void OnPtFeatherAmountChanged(int value) => TriggerPreviewUpdate();

    partial void OnGsIsForegroundChanged(bool value) => TriggerPreviewUpdate();

    private void TriggerPreviewUpdate()
    {
        if (IsLoading || CurrentImage == null || Masks.Count == 0) return;
        if (string.IsNullOrEmpty(SelectedEffect)) return;
        
        _previewCts?.Cancel();
        _previewCts = new System.Threading.CancellationTokenSource();
        var token = _previewCts.Token;

        // Capture necessary state for background processing
        var effect = SelectedEffect;
        var masks = Masks.ToList();
        var targetIndexes = GetTargetMaskIndexes();
        var currentProcessedBmp = ProcessedImage; // We might need to convert this

        Task.Run(async () =>
        {
            try
            {
                await Task.Delay(150, token);
                if (token.IsCancellationRequested) return;

                if (_cachedCurrentMat == null)
                {
                    // Must convert on UI thread or use captured bitmap
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() => {
                        if (CurrentImage != null)
                            _cachedCurrentMat = ImageHelper.BitmapToMat(CurrentImage);
                    });
                }
                
                if (_cachedCurrentMat == null || token.IsCancellationRequested) return;

                // Always preview from the stable snapshot captured when the
                // effect was activated (or after the last Apply). Sourcing
                // from _history.Peek() let sticker/crop/rotate results from
                // the previous tick feed back in, compounding transforms.
                if (_previewSourceSnapshot == null)
                {
                    await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(CapturePreviewSourceSnapshot);
                }
                if (token.IsCancellationRequested) return;

                Mat currentSource = _previewSourceSnapshot != null
                    ? _previewSourceSnapshot.Clone()
                    : _cachedCurrentMat.Clone();

                foreach (int maskIndex in targetIndexes)
                {
                    var next = ApplyEffectToMask(currentSource, masks[maskIndex]);
                    currentSource.Dispose();
                    currentSource = next;
                }

                if (token.IsCancellationRequested)
                {
                    currentSource.Dispose();
                    return;
                }

                var bmp = ImageHelper.MatToBitmap(currentSource);
                currentSource.Dispose();

                await Avalonia.Threading.Dispatcher.UIThread.InvokeAsync(() =>
                {
                    if (!token.IsCancellationRequested)
                    {
                        var old = ProcessedImage;
                        ProcessedImage = bmp;
                        // Avoid disposing if it's in history! 
                        // But ProcessedImage here is a temporary preview bitmap.
                        // We should ensure we don't leak it.
                        // Actually, if we just set it, the previous temporary one might be lost.
                        // Let's track temporary preview bitmaps to dispose them.
                    }
                    else
                    {
                        bmp.Dispose();
                    }
                });
            }
            catch (TaskCanceledException) { }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Preview error: {ex}");
            }
        });
    }

    private List<int> GetTargetMaskIndexes()
    {
        var targetMaskIndexes = MaskItems
            .Select((item, index) => new { item.IsVisible, Index = index })
            .Where(x => x.IsVisible)
            .Select(x => x.Index)
            .Where(i => i >= 0 && i < Masks.Count)
            .ToList();

        if (targetMaskIndexes.Count == 0 && SelectedMaskIndex >= 0 && SelectedMaskIndex < Masks.Count)
            targetMaskIndexes.Add(SelectedMaskIndex);

        return targetMaskIndexes;
    }

    // --- Auto-update (Velopack) ---
    private const string UpdateRepoUrl = "https://github.com/Luck-ai/Vision-Edit";
    private readonly UpdateManager? _updateManager;
    private UpdateInfo? _pendingUpdate;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInstallUpdate))]
    [NotifyPropertyChangedFor(nameof(CanCheckForUpdate))]
    private bool _isUpdateBusy;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanInstallUpdate))]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private string _updateStatus = "";

    [ObservableProperty]
    private string? _availableVersion;

    public bool CanCheckForUpdate => _updateManager?.IsInstalled == true && !IsUpdateBusy;
    public bool CanInstallUpdate => IsUpdateAvailable && !IsUpdateBusy;

    public ObservableCollection<string> History { get; } = new();

    public MainWindowViewModel()
    {
        _client.BaseUrl = ServerUrl;
        History.CollectionChanged += (_, _) => NotifyRightPanelTabProperties();
        MaskItems.CollectionChanged += OnMaskItemsCollectionChanged;
        Boxes.CollectionChanged += OnBoxesCollectionChanged;
        CanvasMode = SelectedTool == "Bounding Box" ? CanvasMode.BBox : CanvasMode.Select;

        try
        {
            _updateManager = new UpdateManager(new GithubSource(UpdateRepoUrl, accessToken: null, prerelease: false));
        }
        catch
        {
            _updateManager = null;
        }

        UpdateStatus = _updateManager?.IsInstalled == true
            ? "Up to date"
            : "Auto-update disabled (running from a non-installed build)";

        if (_updateManager?.IsInstalled == true)
            _ = CheckForUpdatesOnStartupAsync();
    }

    private async Task CheckForUpdatesOnStartupAsync()
    {
        // Let the UI settle before doing background network work.
        await Task.Delay(TimeSpan.FromSeconds(3));
        await RunUpdateCheckAsync(silent: true);
    }

    [RelayCommand]
    private Task CheckForUpdates() => RunUpdateCheckAsync(silent: false);

    private async Task RunUpdateCheckAsync(bool silent)
    {
        if (_updateManager is null || !_updateManager.IsInstalled || IsUpdateBusy) return;

        IsUpdateBusy = true;
        if (!silent) UpdateStatus = "Checking for updates…";
        try
        {
            _pendingUpdate = await _updateManager.CheckForUpdatesAsync();
            if (_pendingUpdate is null)
            {
                IsUpdateAvailable = false;
                AvailableVersion = null;
                UpdateStatus = "Up to date";
                return;
            }

            var version = _pendingUpdate.TargetFullRelease.Version.ToString();
            UpdateStatus = $"Downloading {version}…";
            await _updateManager.DownloadUpdatesAsync(_pendingUpdate);
            AvailableVersion = version;
            IsUpdateAvailable = true;
            UpdateStatus = $"Update {version} ready — restart to install";
        }
        catch (Exception ex) when (silent)
        {
            // Stay quiet on startup — most likely the user is offline or no release exists yet.
            UpdateStatus = "Up to date";
            _pendingUpdate = null;
            IsUpdateAvailable = false;
            _ = ex;
        }
        catch (Exception ex)
        {
            UpdateStatus = $"Update check failed: {ex.Message}";
            _pendingUpdate = null;
            IsUpdateAvailable = false;
        }
        finally
        {
            IsUpdateBusy = false;
        }
    }

    [RelayCommand]
    private void ApplyUpdate()
    {
        if (_updateManager is null || _pendingUpdate is null) return;
        _updateManager.ApplyUpdatesAndRestart(_pendingUpdate);
    }

    private async void OnBoxesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        OnPropertyChanged(nameof(CanSegment));
        OnPropertyChanged(nameof(CanRunSegment));
        OnPropertyChanged(nameof(CanClearWorkspace));

        // Skip auto-segment when:
        //   - another auto-segment is already running (prevents overlapping calls)
        //   - segmentation isn't currently possible (not connected, loading, no boxes/image)
        //   - this change came from us clearing the collection
        if (_isAutoSegmenting || !CanRunSegment || e.Action == NotifyCollectionChangedAction.Reset)
            return;

        _isAutoSegmenting = true;
        try
        {
            await Segment();
        }
        finally
        {
            _isAutoSegmenting = false;
        }
    }

    [RelayCommand]
    private void SelectEffect(string effect)
    {
        // Clicking the active effect deselects it, mirroring WinForms toggle behaviour
        // so the user can dismiss the effect panel and see the last committed image.
        SelectedEffect = SelectedEffect == effect ? null : effect;
    }

    [RelayCommand]
    private void SelectTool(string tool)
    {
        SelectedTool = tool;
        CanvasMode = tool == "Bounding Box" ? CanvasMode.BBox : CanvasMode.Select;
        OnPropertyChanged(nameof(CanSegment));
        OnPropertyChanged(nameof(CanRunSegment));
    }

    partial void OnPromptTextChanged(string value)
    {
        OnPropertyChanged(nameof(CanSegment));
        OnPropertyChanged(nameof(CanRunSegment));
    }

    partial void OnSelectedEffectChanged(string? value)
    {
        // Switching effects must cancel any in-flight preview and clear the
        // previous effect's preview bitmap; otherwise the old preview lingers
        // on screen until (or instead of) the new effect's first tick lands.
        _previewCts?.Cancel();
        _previewCts?.Dispose();
        _previewCts = null;

        // Show the last committed state instead of any lingering preview from
        // the previous effect. CurrentImage is always the loaded original, so
        // nulling ProcessedImage would wipe applied effects off the canvas.
        ProcessedImage = _history.Count > 0 ? _history.Peek() : null;

        // Re-capture the stable base so the next preview starts from the last
        // committed state rather than the prior effect's transformed output.
        CapturePreviewSourceSnapshot();

        OnPropertyChanged(nameof(CanApplyEffects));
        RefreshUiState();

        // Kick off a preview right away so the user sees (and can Apply) the
        // effect at its default parameters without having to nudge a slider first.
        if (!string.IsNullOrEmpty(value)) TriggerPreviewUpdate();
    }

    private void CapturePreviewSourceSnapshot()
    {
        _previewSourceSnapshot?.Dispose();
        _previewSourceSnapshot = null;

        Bitmap? baseBitmap = _history.Count > 0 ? _history.Peek() : CurrentImage;
        if (baseBitmap == null) return;
        _previewSourceSnapshot = ImageHelper.BitmapToMat(baseBitmap);
    }

    partial void OnStBackgroundImagePathChanged(string? value)
    {
        OnPropertyChanged(nameof(StickerBackgroundFileName));
        // Picking a new background while already in Image mode wouldn't otherwise
        // refresh: the mode setter is a no-op when the value is unchanged.
        if (StBackgroundMode == 2) TriggerPreviewUpdate();
    }

    [RelayCommand]
    private async Task CheckConnection()
    {
        _client.BaseUrl = ServerUrl;
        IsCheckingConnection = true;
        IsConnected = false;
        var progress = new Progress<string>(msg => StatusMessage = msg);
        try
        {
            IsConnected = await _client.WaitForHealthAsync(progress, default);
            StatusMessage = IsConnected ? "Connected" : "Disconnected";
        }
        catch (Exception ex)
        {
            IsConnected = false;
            StatusMessage = $"Connection failed: {ex.Message}";
        }
        finally
        {
            IsCheckingConnection = false;
        }
    }

    partial void OnServerUrlChanged(string value)
    {
        _client.BaseUrl = value.Trim();
        IsConnected = false;
        OnPropertyChanged(nameof(ConnectionButtonText));
    }

    partial void OnIsConnectedChanged(bool value)
    {
        OnPropertyChanged(nameof(ConnectionButtonText));
        OnPropertyChanged(nameof(CanRunSegment));
        NotifyRightPanelTabProperties();
    }

    partial void OnRightPanelTabIndexChanged(int value) => NotifyRightPanelTabProperties();

    private void NotifyRightPanelTabProperties()
    {
        OnPropertyChanged(nameof(IsAdjustTab));
        OnPropertyChanged(nameof(IsLayersTab));
        OnPropertyChanged(nameof(IsHistoryTab));
        OnPropertyChanged(nameof(IsServerTab));
        OnPropertyChanged(nameof(HasHistory));
        OnPropertyChanged(nameof(LastHistoryStep));
        OnPropertyChanged(nameof(RightPanelSubtitle));
        OnPropertyChanged(nameof(RightPanelHeaderTitle));
    }

    [RelayCommand]
    private void SelectRightPanelTab(string tab)
    {
        RightPanelTabIndex = tab switch
        {
            "Adjust" => 0,
            "Layers" => 1,
            "History" => 2,
            "Server" => 3,
            _ => 0
        };
        IsRightPanelExpanded = true;
    }

    public string LastHistoryStep => History.Count == 0 ? "" : History[History.Count - 1];

    [RelayCommand]
    private async Task OpenImage(object? window)
    {
        if (window is not Avalonia.Controls.Window w) return;

        var files = await w.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Open Image",
            AllowMultiple = false,
            FileTypeFilter = new[] { Avalonia.Platform.Storage.FilePickerFileTypes.ImageAll }
        });

        if (files.Count == 0) return;

        var path = files[0].Path.LocalPath;
        Bitmap? loaded;
        try
        {
            loaded = new Bitmap(path);
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to open '{Path.GetFileName(path)}': {ex.Message}";
            return;
        }

        var oldCurrent = CurrentImage;
        var oldProcessed = ProcessedImage;
        // Opening a new image fully resets the canvas — drop any in-flight
        // preview, the effect selection, masks/boxes, history, and overlays
        // so the user starts the new file from a clean slate.
        _previewCts?.Cancel();
        _previewCts?.Dispose();
        _previewCts = null;
        CurrentImagePath = path;
        CurrentImage = loaded;
        ProcessedImage = null;
        _cachedCurrentMat?.Dispose();
        _cachedCurrentMat = null;
        _previewSourceSnapshot?.Dispose();
        _previewSourceSnapshot = null;
        Boxes.Clear();
        // Reassign instead of Clear — the canvas's mask cache only rebuilds when
        // the Masks property reference changes, so an in-place Clear leaves old
        // mask overlays drawn on top of a freshly loaded image.
        Masks = new List<float[,]>();
        MaskItems.Clear();
        MaskVisibilityStates.Clear();
        SelectedMaskIndex = -1;
        ClearHistory();
        AreMasksHidden = false;
        IsCompareMode = false;
        SelectedEffect = null;
        StatusMessage = $"Loaded: {Path.GetFileName(path)}";
        OnPropertyChanged(nameof(CanSegment));
        RefreshUiState();
        // Dispose previous bitmaps after the bindings have switched off them.
        oldCurrent?.Dispose();
        oldProcessed?.Dispose();
    }

    [RelayCommand]
    private async Task PickStickerBackground(object? window)
    {
        if (window is not Avalonia.Controls.Window w) return;

        var files = await w.StorageProvider.OpenFilePickerAsync(new Avalonia.Platform.Storage.FilePickerOpenOptions
        {
            Title = "Open Sticker Background",
            AllowMultiple = false,
            FileTypeFilter = new[] { Avalonia.Platform.Storage.FilePickerFileTypes.ImageAll }
        });

        if (files.Count > 0)
        {
            StBackgroundImagePath = files[0].Path.LocalPath;
            StBackgroundMode = 2;
            StatusMessage = $"Sticker background: {Path.GetFileName(StBackgroundImagePath)}";
        }
    }

    [RelayCommand]
    private void ClearMasks()
    {
        var oldProcessed = ProcessedImage;
        Boxes.Clear();
        Masks = new List<float[,]>();
        MaskItems.Clear();
        MaskVisibilityStates.Clear();
        SelectedMaskIndex = -1;
        AreMasksHidden = false;
        ProcessedImage = null;
        _cachedCurrentMat?.Dispose();
        _cachedCurrentMat = null;
        _previewSourceSnapshot?.Dispose();
        _previewSourceSnapshot = null;
        ClearHistory();
        RefreshUiState();
        oldProcessed?.Dispose();
    }

    private void ClearHistory()
    {
        while (_history.Count > 0)
            _history.Pop()?.Dispose();
        History.Clear();
    }

    [RelayCommand]
    private async Task Segment()
    {
        if (string.IsNullOrEmpty(CurrentImagePath)) return;

        IsLoading = true;
        try
        {
            if (SelectedTool == "Bounding Box" && Boxes.Count > 0)
            {
                var boxesArr = Boxes.Select(b => new float[] { 
                    (float)b.Rect.X, (float)b.Rect.Y, (float)b.Rect.Width, (float)b.Rect.Height
                }).ToArray();
                var labelsArr = Boxes.Select(b => b.Label).ToArray();

                var result = await _client.SegmentWithBBoxAsync(CurrentImagePath, boxesArr, labelsArr);
                if (result != null) UpdateMasks(result.Masks);
            }
            else if (SelectedTool == "Prompt" && !string.IsNullOrWhiteSpace(PromptText))
            {
                var result = await _client.SegmentWithTextAsync(CurrentImagePath, PromptText);
                if (result != null) UpdateMasks(result.Masks);
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
            RefreshUiState();
        }
    }

    private void UpdateMasks(List<float[,]> newMasks)
    {
        Masks = newMasks;
        MaskItems.Clear();
        var colors = new[] { Colors.Red, Colors.Orange, Colors.Yellow, Colors.Green, Colors.Blue, Colors.Purple };
        for (int i = 0; i < Masks.Count; i++)
        {
            MaskItems.Add(new MaskItemViewModel 
            { 
                Name = $"Mask {i + 1}", 
                Color = colors[i % colors.Length] 
            });
        }

        SelectedMaskIndex = MaskItems.Count > 0 ? 0 : -1;
        AreMasksHidden = false;
        RebuildMaskVisibilityStates();
        if (MaskItems.Count > 0) RightPanelTabIndex = 1;
        // Fresh segmentation — start with no effect chosen so the user picks deliberately.
        SelectedEffect = null;
        RefreshUiState();
    }

    [RelayCommand]
    private void ApplyEffect()
    {
        if (CurrentImage == null || Masks == null || Masks.Count == 0 || string.IsNullOrEmpty(SelectedEffect)) return;

        var targetMaskIndexes = GetTargetMaskIndexes();
        if (targetMaskIndexes.Count == 0) return;

        // When applying, we push the CURRENT ProcessedImage (which might be the preview) to history
        // wait, that's wrong. History should store the state BEFORE this apply.
        // But if ProcessedImage is currently showing a preview, then the state BEFORE this apply
        // was the state before the preview started.
        
        // Let's change the logic: The history stores the "stable" states.
        // We'll keep a `_stableProcessedImage` or just push the previous stable state.
        
        // Current logic in original code:
        // _history.Push(ProcessedImage);
        // ProcessedImage = nextProcessed;
        
        // If live previews are active, ProcessedImage is constantly changing.
        // So we need to push the image that was there before the current set of parameter changes.
        
        // Actually, let's keep it simple: 
        // 1. "Apply" takes the current ProcessedImage (which is the result of parameters) 
        //    and "locks" it as a historical step.
        // 2. The image that was there BEFORE we started adjusting parameters for this step
        //    should be the one pushed to history.
        
        Bitmap? baseImage = _history.Count > 0 ? _history.Peek() : null;
        // Wait, the stack logic is: Push OLD, Set NEW.
        // So _history.Peek() is the image BEFORE the current ProcessedImage.
        
        _history.Push(ProcessedImage); // This is the preview or previous stable result
        // Wait, if it's already the preview, we just keep it and add to History list.

        History.Add(SelectedEffect);
        // The committed image is now the stable base future previews must build on.
        CapturePreviewSourceSnapshot();
        RefreshUiState();
    }

    [RelayCommand]
    private void ResetEffect()
    {
        // Revert last applied effect
        UndoEffect();
    }

    [RelayCommand]
    private void ToggleCompare()
    {
        IsCompareMode = !IsCompareMode;
    }

    [RelayCommand]
    private void ToggleHideMasks()
    {
        AreMasksHidden = !AreMasksHidden;
    }

    [RelayCommand]
    private void ToggleMaskPanel()
    {
        IsMaskPanelVisible = !IsMaskPanelVisible;
    }

    [RelayCommand]
    private void ToggleLeftPanel()
    {
        IsLeftPanelExpanded = !IsLeftPanelExpanded;
    }

    [RelayCommand]
    private void ToggleRightPanel()
    {
        IsRightPanelExpanded = !IsRightPanelExpanded;
    }

    partial void OnAreMasksHiddenChanged(bool value)
    {
        OnPropertyChanged(nameof(HideMasksButtonText));
    }

    [RelayCommand]
    private void ResetAll()
    {
        var oldProcessed = ProcessedImage;
        ClearHistory();
        ProcessedImage = null;
        _cachedCurrentMat?.Dispose();
        _cachedCurrentMat = null;
        _previewSourceSnapshot?.Dispose();
        _previewSourceSnapshot = null;
        Boxes.Clear();
        Masks = new List<float[,]>();
        MaskItems.Clear();
        MaskVisibilityStates.Clear();
        SelectedMaskIndex = -1;
        AreMasksHidden = false;
        IsCompareMode = false;
        StatusMessage = "Project fully reset.";
        RefreshUiState();
        oldProcessed?.Dispose();
    }

    [RelayCommand]
    private void UndoEffect()
    {
        if (_history.Count == 0)
        {
            // No committed steps — just drop any orphan preview.
            ProcessedImage = null;
            History.Clear();
            RefreshUiState();
            return;
        }

        // _history stores the post-state of each Apply. Pop the latest committed
        // step and reveal the new top (or the original) so undo rolls back one
        // user-visible effect rather than re-asserting the state we're already in.
        _history.Pop();
        ProcessedImage = _history.Count > 0 ? _history.Peek() : null;
        if (History.Count > 0) History.RemoveAt(History.Count - 1);
        // Stable base just rolled back; refresh the preview snapshot to match.
        CapturePreviewSourceSnapshot();
        RefreshUiState();
    }

    [RelayCommand]
    private async Task SaveImage(object? window)
    {
        if (ProcessedImage == null || window is not Avalonia.Controls.Window w) return;

        var file = await w.StorageProvider.SaveFilePickerAsync(new Avalonia.Platform.Storage.FilePickerSaveOptions
        {
            Title = "Save Image",
            DefaultExtension = ".png",
            FileTypeChoices = new[] { Avalonia.Platform.Storage.FilePickerFileTypes.ImagePng }
        });

        if (file == null) return;

        try
        {
            await using var stream = await file.OpenWriteAsync();
            ProcessedImage.Save(stream);
            StatusMessage = $"Saved to {file.Name}.";
        }
        catch (Exception ex)
        {
            StatusMessage = $"Save failed: {ex.Message}";
        }
        finally
        {
            RefreshUiState();
        }
    }

    private Mat ApplyEffectToMask(Mat source, float[,] mask)
    {
        switch (SelectedEffect)
        {
            case "Color Grading":
            {
                var targetMask = CgIsForeground ? mask : InvertMask(mask);
                var tintColor = new MCvScalar(CgTintColor.B, CgTintColor.G, CgTintColor.R);
                return ImageEffects.ColorGrading(source, targetMask, tintColor, (float)CgTintStrength, CgBrightness, (float)CgContrast / 10f, false);
            }
            case "Artistic Style":
                return ArtIsStylize
                    ? ImageEffects.StylizeMasked(source, mask, ArtSigmaS, (float)ArtSigmaR / 100f)
                    : ImageEffects.PencilSketchMasked(source, mask, ArtSigmaS, (float)ArtShadeFactor / 100f);
            case "Sticker Generation":
            {
                var borderColor = new MCvScalar(StBorderColor.B, StBorderColor.G, StBorderColor.R, 255);
                using var sticker = ImageEffects.ExtractSticker(source, mask, 0.5f, StThickness, StShadowBlur, borderColor, (float)StScale / 10f, StRotation);

                if (StBackgroundMode == 1)
                {
                    using var solidBackground = ImageEffects.SolidColorBackground(
                        new MCvScalar(StBackgroundColor.B, StBackgroundColor.G, StBackgroundColor.R),
                        source.Width,
                        source.Height);
                    return ImageEffects.CompositeSticker(sticker, solidBackground);
                }

                if (StBackgroundMode == 2 && !string.IsNullOrWhiteSpace(StBackgroundImagePath) && File.Exists(StBackgroundImagePath))
                {
                    using var imageBackground = CvInvoke.Imread(StBackgroundImagePath, ImreadModes.ColorBgr);
                    using var resizedBackground = new Mat();
                    CvInvoke.Resize(imageBackground, resizedBackground, new System.Drawing.Size(source.Width, source.Height), interpolation: Inter.Linear);
                    return ImageEffects.CompositeSticker(sticker, resizedBackground);
                }

                if (StBackgroundMode == 3)
                    return ImageEffects.CropToContent(sticker);

                return ImageEffects.CompositeSticker(sticker, source);
            }
            case "Pixelation & Blur":
            {
                var targetMask = PbIsForeground ? mask : InvertMask(mask);
                return PbIsPixelate
                    ? ImageEffects.PixelateMasked(source, targetMask, Math.Max(2, PbIntensity))
                    : ImageEffects.BlurMasked(source, targetMask, Math.Max(3, PbIntensity));
            }
            case "Portrait Effect":
                return ImageEffects.PortraitEffect(source, mask, PtBlurStrength, PtFeatherAmount);
            case "Grayscale":
            {
                var targetMask = GsIsForeground ? mask : InvertMask(mask);
                return ImageEffects.ColorGrading(
                    source,
                    targetMask,
                    new MCvScalar(255, 255, 255),
                    0f,
                    0,
                    1f,
                    true);
            }
            default:
                return source.Clone();
        }
    }

    private static float[,] InvertMask(float[,] mask)
    {
        int rows = mask.GetLength(0);
        int cols = mask.GetLength(1);
        var inverted = new float[rows, cols];

        for (int r = 0; r < rows; r++)
        {
            for (int c = 0; c < cols; c++)
            {
                inverted[r, c] = 1f - mask[r, c];
            }
        }

        return inverted;
    }

    private void OnMaskItemsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.NewItems is not null)
        {
            foreach (MaskItemViewModel item in e.NewItems)
                item.PropertyChanged += OnMaskItemPropertyChanged;
        }

        if (e.OldItems is not null)
        {
            foreach (MaskItemViewModel item in e.OldItems)
                item.PropertyChanged -= OnMaskItemPropertyChanged;
        }

        RebuildMaskVisibilityStates();
    }

    private void OnMaskItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(MaskItemViewModel.IsVisible))
            RebuildMaskVisibilityStates();
    }

    private void RebuildMaskVisibilityStates()
    {
        MaskVisibilityStates = new ObservableCollection<bool>(MaskItems.Select(item => item.IsVisible));
        RefreshUiState();
    }

    partial void OnCurrentImageChanged(Bitmap? value) => RefreshUiState();

    partial void OnProcessedImageChanged(Bitmap? value) => RefreshUiState();

    partial void OnMasksChanged(List<float[,]> value) => RefreshUiState();

    private void RefreshUiState()
    {
        OnPropertyChanged(nameof(ImageDimensionsText));
        OnPropertyChanged(nameof(HasImage));
        OnPropertyChanged(nameof(HasProcessedImage));
        OnPropertyChanged(nameof(HasMasks));
        OnPropertyChanged(nameof(CanSegment));
        OnPropertyChanged(nameof(CanRunSegment));
        OnPropertyChanged(nameof(CanSaveImage));
        OnPropertyChanged(nameof(CanApplyEffects));
        OnPropertyChanged(nameof(CanUndoEffect));
        OnPropertyChanged(nameof(CanClearWorkspace));
        OnPropertyChanged(nameof(CanRunConnectionCheck));
        OnPropertyChanged(nameof(RightPanelSubtitle));
    }
}
