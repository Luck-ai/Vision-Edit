using VisionEditCV.Controls;

namespace VisionEditCV
{
    partial class MainForm
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        // ── Layout containers ────────────────────────────────────────────────
        private System.Windows.Forms.Panel _leftPanel;
        private System.Windows.Forms.Panel _centerPanel;
        private System.Windows.Forms.Panel _rightPanel;
        private System.Windows.Forms.Panel _effectSubPanel;
        private System.Windows.Forms.Panel _bottomContainer;

        // ── Left panel ───────────────────────────────────────────────────────
        private System.Windows.Forms.Label           _titleLabel;
        private System.Windows.Forms.Label           _effectsLabel;
        private System.Windows.Forms.FlowLayoutPanel _leftFlow;
        private DarkButton _btnColorGrading;
        private DarkButton _btnArtisticStyle;
        private DarkButton _btnStickerGen;
        private DarkButton _btnPixelBlur;
        private DarkButton _btnPortrait;
        private DarkButton _btnGrayscale;
        private DarkButton _btnCompare;
        private DarkButton _btnSave;
        private System.Windows.Forms.Panel _leftBottomSpacer2;

        // ── Center panel ─────────────────────────────────────────────────────
        private VisionEditCV.Controls.ImageCanvas _canvas;
        private DarkButton _btnChangeImage;
        private System.Windows.Forms.Panel _loadingOverlay;
        private System.Windows.Forms.Label _loadingLabel;

        // ── Right panel ──────────────────────────────────────────────────────
        private System.Windows.Forms.Label           _maskListTitle;
        private VisionEditCV.Controls.MaskListPanel  _maskList;

        // ── Top bar (selection mode) ────────────────────────────────────────
        private System.Windows.Forms.Panel   _topBar;

        // ── Bottom bar (plain Panel, no TableLayout) ─────────────────────────
        private System.Windows.Forms.Panel   _bottomBar;

        // Selection mode group (left side)
        private System.Windows.Forms.Label   _lblSelMode;
        private DarkButton                   _btnBBox;
        private DarkButton                   _btnPrompt;
        private VisionEditCV.Controls.RoundedTextBox _promptBox;
        private DarkButton                   _btnSegment;

        // Server group (right side)
        private System.Windows.Forms.Panel   _serverPanel;
        private System.Windows.Forms.Label   _lblServer;
        private System.Windows.Forms.TextBox _txtServerUrl;
        private System.Windows.Forms.Label   _lblServerStatus;

        // ── No-effect placeholder ───────────────────────────────────────────
        private System.Windows.Forms.Label _lblNoEffect;

        // ── Applied effects tracking ────────────────────────────────────────
        private System.Windows.Forms.FlowLayoutPanel _appliedEffectsPanel;
        private DarkButton _btnResetAll;

        // ── Effect sub-panels ────────────────────────────────────────────────
        private System.Windows.Forms.Panel _panelColorGrading;
        private System.Windows.Forms.Panel _panelArtistic;
        private System.Windows.Forms.Panel _panelSticker;
        private System.Windows.Forms.Panel _panelPixelBlur;
        private System.Windows.Forms.Panel _panelPortrait;
        private System.Windows.Forms.Panel _panelGrayscale;
        private DarkButton                 _btnApplyEffect;

        // ── Color Grading controls ───────────────────────────────────────────
        private System.Windows.Forms.FlowLayoutPanel _cgFlow;
        private System.Windows.Forms.Panel  _grpCgTint;
        private System.Windows.Forms.Label  _lblCgTint;
        private ColorSwatch                 _cgTintSwatch;
        private System.Windows.Forms.Panel  _grpCgTs;
        private System.Windows.Forms.Label  _lblCgTs;
        private SliderControl               _cgTintStrength;
        private System.Windows.Forms.Panel  _grpCgBr;
        private System.Windows.Forms.Label  _lblCgBr;
        private SliderControl               _cgBrightness;
        private System.Windows.Forms.Panel  _grpCgCo;
        private System.Windows.Forms.Label  _lblCgCo;
        private SliderControl               _cgContrast;
        private System.Windows.Forms.Panel  _grpCgTb;
        private System.Windows.Forms.Label  _lblCgTb;
        private System.Windows.Forms.CheckBox _cgTargetBg;

        // ── Grayscale controls ───────────────────────────────────────────────
        private System.Windows.Forms.FlowLayoutPanel _gsFlow;
        private System.Windows.Forms.Panel  _grpGsTb;
        private System.Windows.Forms.Label  _lblGsTb;
        private System.Windows.Forms.CheckBox _gsTargetBg;

        // ── Artistic controls ────────────────────────────────────────────────
        private System.Windows.Forms.FlowLayoutPanel _artFlow;
        private System.Windows.Forms.Panel  _grpArtInt;
        private System.Windows.Forms.Label  _lblArtInt;
        private SliderControl               _artIntensity;

        // ── Sticker controls ─────────────────────────────────────────────────
        private System.Windows.Forms.FlowLayoutPanel _stFlow;
        private System.Windows.Forms.Panel  _grpStSc;
        private System.Windows.Forms.Label  _lblStSc;
        private SliderControl               _stScale;
        private System.Windows.Forms.Panel  _grpStRot;
        private System.Windows.Forms.Label  _lblStRot;
        private SliderControl               _stRotation;
        private System.Windows.Forms.Panel  _grpStBc;
        private System.Windows.Forms.Label  _lblStBc;
        private ColorSwatch                 _stBorderColor;
        private System.Windows.Forms.Panel  _grpStBt;
        private System.Windows.Forms.Label  _lblStBt;
        private SliderControl               _stThickness;
        private System.Windows.Forms.Panel  _grpStSh;
        private System.Windows.Forms.Label  _lblStSh;
        private SliderControl               _stShadowBlur;
        private System.Windows.Forms.Panel  _grpStBg;
        private System.Windows.Forms.Label  _lblStBg;
        private System.Windows.Forms.RadioButton _rdoStickerOriginalBg;
        private System.Windows.Forms.RadioButton _rdoStickerColorBg;
        private System.Windows.Forms.RadioButton _rdoStickerImageBg;
        private System.Windows.Forms.RadioButton _rdoStickerTransparentBg;
        private ColorSwatch                      _stBgColorSwatch;
        private DarkButton                       _btnStickerUploadBg;

        // ── PixelBlur controls ───────────────────────────────────────────────
        private System.Windows.Forms.FlowLayoutPanel _pbFlow;
        private System.Windows.Forms.Panel  _grpPbMode;
        private System.Windows.Forms.Label  _lblPbMode;
        private DarkButton                  _btnPixelMode;
        private DarkButton                  _btnBlurMode;
        private System.Windows.Forms.Panel  _grpPbInt;
        private System.Windows.Forms.Label  _lblPbInt;
        private SliderControl               _pbIntensity;
        private System.Windows.Forms.Panel  _grpPbTarget;
        private System.Windows.Forms.Label  _lblPbTarget;
        private DarkButton                  _btnPbForeground;
        private DarkButton                  _btnPbBackground;

        // ── Portrait controls ────────────────────────────────────────────────
        private System.Windows.Forms.FlowLayoutPanel _ptFlow;
        private System.Windows.Forms.Panel  _grpPtBlur;
        private System.Windows.Forms.Label  _lblPtBlur;
        private SliderControl               _ptBlurStrength;
        private System.Windows.Forms.Panel  _grpPtFeather;
        private System.Windows.Forms.Label  _lblPtFeather;
        private SliderControl               _ptFeatherAmount;

        private void InitializeComponent()
        {
            _titleLabel = new Label();
            _effectsLabel = new Label();
            _btnColorGrading = new DarkButton();
            _btnArtisticStyle = new DarkButton();
            _btnStickerGen = new DarkButton();
            _btnPixelBlur = new DarkButton();
            _btnPortrait = new DarkButton();
            _leftFlow = new FlowLayoutPanel();
            _btnGrayscale = new DarkButton();
            _leftBottomSpacer2 = new Panel();
            _btnSave = new DarkButton();
            _btnCompare = new DarkButton();
            _leftPanel = new Panel();
            _leftBottomSpacer = new Panel();
            _maskListTitle = new Label();
            _maskList = new MaskListPanel();
            _rightPanel = new Panel();
            _effectSubPanel = new Panel();
            _lblNoEffect = new Label();
            _panelColorGrading = new Panel();
            _cgFlow = new FlowLayoutPanel();
            _grpCgTint = new Panel();
            _lblCgTint = new Label();
            _cgTintSwatch = new ColorSwatch();
            _grpCgTs = new Panel();
            _lblCgTs = new Label();
            _cgTintStrength = new SliderControl();
            _grpCgBr = new Panel();
            _lblCgBr = new Label();
            _cgBrightness = new SliderControl();
            _grpCgCo = new Panel();
            _lblCgCo = new Label();
            _cgContrast = new SliderControl();
            _grpCgTb = new Panel();
            _lblCgTb = new Label();
            _cgTargetBg = new CheckBox();
            _panelArtistic = new Panel();
            _artFlow = new FlowLayoutPanel();
            _grpArtInt = new Panel();
            _lblArtInt = new Label();
            _artIntensity = new SliderControl();
            _panelSticker = new Panel();
            _stFlow = new FlowLayoutPanel();
            _grpStSc = new Panel();
            _lblStSc = new Label();
            _stScale = new SliderControl();
            _grpStRot = new Panel();
            _lblStRot = new Label();
            _stRotation = new SliderControl();
            _grpStBc = new Panel();
            _lblStBc = new Label();
            _stBorderColor = new ColorSwatch();
            _grpStBt = new Panel();
            _lblStBt = new Label();
            _stThickness = new SliderControl();
            _grpStSh = new Panel();
            _lblStSh = new Label();
            _stShadowBlur = new SliderControl();
            _grpStBg = new Panel();
            _lblStBg = new Label();
            _rdoStickerOriginalBg = new RadioButton();
            _rdoStickerColorBg = new RadioButton();
            _rdoStickerImageBg = new RadioButton();
            _rdoStickerTransparentBg = new RadioButton();
            _stBgColorSwatch = new ColorSwatch();
            _btnStickerUploadBg = new DarkButton();
            _panelPixelBlur = new Panel();
            _pbFlow = new FlowLayoutPanel();
            _grpPbMode = new Panel();
            _lblPbMode = new Label();
            _btnPixelMode = new DarkButton();
            _btnBlurMode = new DarkButton();
            _grpPbInt = new Panel();
            _lblPbInt = new Label();
            _pbIntensity = new SliderControl();
            _grpPbTarget = new Panel();
            _lblPbTarget = new Label();
            _btnPbForeground = new DarkButton();
            _btnPbBackground = new DarkButton();
            _panelPortrait = new Panel();
            _ptFlow = new FlowLayoutPanel();
            _grpPtBlur = new Panel();
            _lblPtBlur = new Label();
            _ptBlurStrength = new SliderControl();
            _grpPtFeather = new Panel();
            _lblPtFeather = new Label();
            _ptFeatherAmount = new SliderControl();
            _panelGrayscale = new Panel();
            _gsFlow = new FlowLayoutPanel();
            _grpGsTb = new Panel();
            _lblGsTb = new Label();
            _gsTargetBg = new CheckBox();
            _btnApplyEffect = new DarkButton();
            _appliedEffectsPanel = new FlowLayoutPanel();
            _lblSelMode = new Label();
            _btnBBox = new DarkButton();
            _btnPrompt = new DarkButton();
            _promptBox = new RoundedTextBox();
            _btnSegment = new DarkButton();
            _serverPanel = new Panel();
            _lblServer = new Label();
            _txtServerUrl = new TextBox();
            _lblServerStatus = new Label();
            _topBar = new Panel();
            label1 = new Label();
            _btnStartServer = new DarkButton();
            _bottomBar = new Panel();
            _bottomContainer = new Panel();
            _btnResetAll = new DarkButton();
            _loadingLabel = new Label();
            _loadingOverlay = new Panel();
            _canvas = new ImageCanvas();
            _btnChangeImage = new DarkButton();
            _centerPanel = new Panel();
            _leftFlow.SuspendLayout();
            _leftPanel.SuspendLayout();
            _rightPanel.SuspendLayout();
            _effectSubPanel.SuspendLayout();
            _panelColorGrading.SuspendLayout();
            _cgFlow.SuspendLayout();
            _grpCgTint.SuspendLayout();
            _grpCgTs.SuspendLayout();
            _grpCgBr.SuspendLayout();
            _grpCgCo.SuspendLayout();
            _grpCgTb.SuspendLayout();
            _panelArtistic.SuspendLayout();
            _artFlow.SuspendLayout();
            _grpArtInt.SuspendLayout();
            _panelSticker.SuspendLayout();
            _stFlow.SuspendLayout();
            _grpStSc.SuspendLayout();
            _grpStRot.SuspendLayout();
            _grpStBc.SuspendLayout();
            _grpStBt.SuspendLayout();
            _grpStSh.SuspendLayout();
            _grpStBg.SuspendLayout();
            _panelPixelBlur.SuspendLayout();
            _pbFlow.SuspendLayout();
            _grpPbMode.SuspendLayout();
            _grpPbInt.SuspendLayout();
            _grpPbTarget.SuspendLayout();
            _panelPortrait.SuspendLayout();
            _ptFlow.SuspendLayout();
            _grpPtBlur.SuspendLayout();
            _grpPtFeather.SuspendLayout();
            _panelGrayscale.SuspendLayout();
            _gsFlow.SuspendLayout();
            _grpGsTb.SuspendLayout();
            _serverPanel.SuspendLayout();
            _topBar.SuspendLayout();
            _bottomContainer.SuspendLayout();
            _loadingOverlay.SuspendLayout();
            _canvas.SuspendLayout();
            _centerPanel.SuspendLayout();
            SuspendLayout();
            // 
            // _titleLabel
            // 
            _titleLabel.BackColor = Color.Transparent;
            _titleLabel.Dock = DockStyle.Top;
            _titleLabel.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
            _titleLabel.Location = new Point(12, 16);
            _titleLabel.Name = "_titleLabel";
            _titleLabel.Size = new Size(180, 0);
            _titleLabel.TabIndex = 2;
            _titleLabel.TextAlign = ContentAlignment.MiddleLeft;
            _titleLabel.Visible = false;
            // 
            // _effectsLabel
            // 
            _effectsLabel.BackColor = Color.Transparent;
            _effectsLabel.Dock = DockStyle.Top;
            _effectsLabel.Font = new Font("Segoe UI", 13F, FontStyle.Bold);
            _effectsLabel.ForeColor = Color.FromArgb(220, 220, 220);
            _effectsLabel.Location = new Point(12, 16);
            _effectsLabel.Name = "_effectsLabel";
            _effectsLabel.Padding = new Padding(2, 0, 0, 4);
            _effectsLabel.Size = new Size(180, 40);
            _effectsLabel.TabIndex = 1;
            _effectsLabel.Text = "Effects";
            _effectsLabel.TextAlign = ContentAlignment.MiddleLeft;
            // 
            // _btnColorGrading
            // 
            _btnColorGrading.BackColor = Color.FromArgb(30, 32, 36);
            _btnColorGrading.FlatAppearance.BorderSize = 0;
            _btnColorGrading.FlatStyle = FlatStyle.Flat;
            _btnColorGrading.Font = new Font("Segoe UI", 9.5F);
            _btnColorGrading.ForeColor = Color.FromArgb(220, 220, 220);
            _btnColorGrading.Icon = "🎨";
            _btnColorGrading.Location = new Point(0, 8);
            _btnColorGrading.Margin = new Padding(0, 0, 0, 8);
            _btnColorGrading.Name = "_btnColorGrading";
            _btnColorGrading.Padding = new Padding(8, 0, 0, 0);
            _btnColorGrading.Size = new Size(180, 52);
            _btnColorGrading.TabIndex = 0;
            _btnColorGrading.Text = "Color Grading";
            _btnColorGrading.TextAlign = ContentAlignment.MiddleLeft;
            _btnColorGrading.UseVisualStyleBackColor = false;
            // 
            // _btnArtisticStyle
            // 
            _btnArtisticStyle.BackColor = Color.FromArgb(30, 32, 36);
            _btnArtisticStyle.FlatAppearance.BorderSize = 0;
            _btnArtisticStyle.FlatStyle = FlatStyle.Flat;
            _btnArtisticStyle.Font = new Font("Segoe UI", 9.5F);
            _btnArtisticStyle.ForeColor = Color.FromArgb(220, 220, 220);
            _btnArtisticStyle.Icon = "✏️";
            _btnArtisticStyle.Location = new Point(0, 68);
            _btnArtisticStyle.Margin = new Padding(0, 0, 0, 8);
            _btnArtisticStyle.Name = "_btnArtisticStyle";
            _btnArtisticStyle.Padding = new Padding(8, 0, 0, 0);
            _btnArtisticStyle.Size = new Size(180, 52);
            _btnArtisticStyle.TabIndex = 1;
            _btnArtisticStyle.Text = "Artistic Style";
            _btnArtisticStyle.TextAlign = ContentAlignment.MiddleLeft;
            _btnArtisticStyle.UseVisualStyleBackColor = false;
            // 
            // _btnStickerGen
            // 
            _btnStickerGen.BackColor = Color.FromArgb(30, 32, 36);
            _btnStickerGen.FlatAppearance.BorderSize = 0;
            _btnStickerGen.FlatStyle = FlatStyle.Flat;
            _btnStickerGen.Font = new Font("Segoe UI", 9.5F);
            _btnStickerGen.ForeColor = Color.FromArgb(220, 220, 220);
            _btnStickerGen.Icon = "⭐";
            _btnStickerGen.Location = new Point(0, 128);
            _btnStickerGen.Margin = new Padding(0, 0, 0, 8);
            _btnStickerGen.Name = "_btnStickerGen";
            _btnStickerGen.Padding = new Padding(8, 0, 0, 0);
            _btnStickerGen.Size = new Size(180, 52);
            _btnStickerGen.TabIndex = 2;
            _btnStickerGen.Text = "Sticker Generation";
            _btnStickerGen.TextAlign = ContentAlignment.MiddleLeft;
            _btnStickerGen.UseVisualStyleBackColor = false;
            // 
            // _btnPixelBlur
            // 
            _btnPixelBlur.BackColor = Color.FromArgb(30, 32, 36);
            _btnPixelBlur.FlatAppearance.BorderSize = 0;
            _btnPixelBlur.FlatStyle = FlatStyle.Flat;
            _btnPixelBlur.Font = new Font("Segoe UI", 9.5F);
            _btnPixelBlur.ForeColor = Color.FromArgb(220, 220, 220);
            _btnPixelBlur.Icon = "🌀";
            _btnPixelBlur.Location = new Point(0, 188);
            _btnPixelBlur.Margin = new Padding(0, 0, 0, 8);
            _btnPixelBlur.Name = "_btnPixelBlur";
            _btnPixelBlur.Padding = new Padding(8, 0, 0, 0);
            _btnPixelBlur.Size = new Size(180, 52);
            _btnPixelBlur.TabIndex = 3;
            _btnPixelBlur.Text = "Pixelation & Blur";
            _btnPixelBlur.TextAlign = ContentAlignment.MiddleLeft;
            _btnPixelBlur.UseVisualStyleBackColor = false;
            // 
            // _btnPortrait
            // 
            _btnPortrait.BackColor = Color.FromArgb(30, 32, 36);
            _btnPortrait.FlatAppearance.BorderSize = 0;
            _btnPortrait.FlatStyle = FlatStyle.Flat;
            _btnPortrait.Font = new Font("Segoe UI", 9.5F);
            _btnPortrait.ForeColor = Color.FromArgb(220, 220, 220);
            _btnPortrait.Icon = "👤";
            _btnPortrait.Location = new Point(0, 248);
            _btnPortrait.Margin = new Padding(0, 0, 0, 8);
            _btnPortrait.Name = "_btnPortrait";
            _btnPortrait.Padding = new Padding(8, 0, 0, 0);
            _btnPortrait.Size = new Size(180, 52);
            _btnPortrait.TabIndex = 4;
            _btnPortrait.Text = "Portrait Effect";
            _btnPortrait.TextAlign = ContentAlignment.MiddleLeft;
            _btnPortrait.UseVisualStyleBackColor = false;
            // 
            // _leftFlow
            // 
            _leftFlow.BackColor = Color.Transparent;
            _leftFlow.Controls.Add(_btnColorGrading);
            _leftFlow.Controls.Add(_btnArtisticStyle);
            _leftFlow.Controls.Add(_btnStickerGen);
            _leftFlow.Controls.Add(_btnPixelBlur);
            _leftFlow.Controls.Add(_btnPortrait);
            _leftFlow.Controls.Add(_btnGrayscale);
            _leftFlow.Dock = DockStyle.Fill;
            _leftFlow.FlowDirection = FlowDirection.TopDown;
            _leftFlow.Location = new Point(12, 56);
            _leftFlow.Name = "_leftFlow";
            _leftFlow.Padding = new Padding(0, 8, 0, 0);
            _leftFlow.Size = new Size(180, 547);
            _leftFlow.TabIndex = 0;
            _leftFlow.WrapContents = false;
            // 
            // _btnGrayscale
            // 
            _btnGrayscale.BackColor = Color.FromArgb(30, 32, 36);
            _btnGrayscale.FlatAppearance.BorderSize = 0;
            _btnGrayscale.FlatStyle = FlatStyle.Flat;
            _btnGrayscale.Font = new Font("Segoe UI", 9.5F);
            _btnGrayscale.ForeColor = Color.FromArgb(220, 220, 220);
            _btnGrayscale.Icon = "◑";
            _btnGrayscale.Location = new Point(0, 308);
            _btnGrayscale.Margin = new Padding(0, 0, 0, 8);
            _btnGrayscale.Name = "_btnGrayscale";
            _btnGrayscale.Padding = new Padding(8, 0, 0, 0);
            _btnGrayscale.Size = new Size(180, 52);
            _btnGrayscale.TabIndex = 5;
            _btnGrayscale.Text = "Grayscale";
            _btnGrayscale.TextAlign = ContentAlignment.MiddleLeft;
            _btnGrayscale.UseVisualStyleBackColor = false;
            _btnGrayscale.Click += _btnGrayscale_Click;
            // 
            // _leftBottomSpacer2
            // 
            _leftBottomSpacer2.BackColor = Color.Transparent;
            _leftBottomSpacer2.Dock = DockStyle.Bottom;
            _leftBottomSpacer2.Location = new Point(12, 603);
            _leftBottomSpacer2.Name = "_leftBottomSpacer2";
            _leftBottomSpacer2.Size = new Size(180, 8);
            _leftBottomSpacer2.TabIndex = 6;
            // 
            // _btnSave
            // 
            _btnSave.BackColor = Color.FromArgb(0, 180, 160);
            _btnSave.Dock = DockStyle.Bottom;
            _btnSave.FlatAppearance.BorderSize = 0;
            _btnSave.FlatStyle = FlatStyle.Flat;
            _btnSave.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            _btnSave.ForeColor = Color.FromArgb(13, 13, 13);
            _btnSave.Icon = "💾";
            _btnSave.Location = new Point(12, 663);
            _btnSave.Name = "_btnSave";
            _btnSave.Padding = new Padding(8, 0, 0, 0);
            _btnSave.Size = new Size(180, 44);
            _btnSave.TabIndex = 5;
            _btnSave.Text = "Save Image";
            _btnSave.TextAlign = ContentAlignment.MiddleLeft;
            _btnSave.UseVisualStyleBackColor = false;
            // 
            // _btnCompare
            // 
            _btnCompare.BackColor = Color.FromArgb(30, 32, 36);
            _btnCompare.Dock = DockStyle.Bottom;
            _btnCompare.FlatAppearance.BorderSize = 0;
            _btnCompare.FlatStyle = FlatStyle.Flat;
            _btnCompare.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            _btnCompare.ForeColor = Color.FromArgb(220, 220, 220);
            _btnCompare.Icon = "🖼️";
            _btnCompare.Location = new Point(12, 619);
            _btnCompare.Name = "_btnCompare";
            _btnCompare.Padding = new Padding(8, 0, 0, 0);
            _btnCompare.Size = new Size(180, 44);
            _btnCompare.TabIndex = 4;
            _btnCompare.Text = "Show Before";
            _btnCompare.TextAlign = ContentAlignment.MiddleLeft;
            _btnCompare.UseVisualStyleBackColor = false;
            _btnCompare.Click += _btnCompare_Click;
            // 
            // _leftPanel
            // 
            _leftPanel.BackColor = Color.FromArgb(18, 18, 18);
            _leftPanel.Controls.Add(_leftFlow);
            _leftPanel.Controls.Add(_effectsLabel);
            _leftPanel.Controls.Add(_titleLabel);
            _leftPanel.Controls.Add(_leftBottomSpacer2);
            _leftPanel.Controls.Add(_leftBottomSpacer);
            _leftPanel.Controls.Add(_btnCompare);
            _leftPanel.Controls.Add(_btnSave);
            _leftPanel.Dock = DockStyle.Left;
            _leftPanel.Location = new Point(0, 60);
            _leftPanel.Name = "_leftPanel";
            _leftPanel.Padding = new Padding(12, 16, 12, 14);
            _leftPanel.Size = new Size(204, 721);
            _leftPanel.TabIndex = 1;
            // 
            // _leftBottomSpacer
            // 
            _leftBottomSpacer.BackColor = Color.Transparent;
            _leftBottomSpacer.Dock = DockStyle.Bottom;
            _leftBottomSpacer.Location = new Point(12, 611);
            _leftBottomSpacer.Name = "_leftBottomSpacer";
            _leftBottomSpacer.Size = new Size(180, 8);
            _leftBottomSpacer.TabIndex = 3;
            // 
            // _maskListTitle
            // 
            _maskListTitle.BackColor = Color.Transparent;
            _maskListTitle.Dock = DockStyle.Top;
            _maskListTitle.Font = new Font("Segoe UI", 8.5F, FontStyle.Bold);
            _maskListTitle.ForeColor = Color.FromArgb(160, 165, 190);
            _maskListTitle.Location = new Point(0, 12);
            _maskListTitle.Name = "_maskListTitle";
            _maskListTitle.Size = new Size(260, 36);
            _maskListTitle.TabIndex = 1;
            _maskListTitle.Text = "MASKS";
            _maskListTitle.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // _maskList
            // 
            _maskList.AutoScroll = true;
            _maskList.BackColor = Color.FromArgb(20, 20, 20);
            _maskList.Dock = DockStyle.Fill;
            _maskList.Location = new Point(0, 48);
            _maskList.Name = "_maskList";
            _maskList.Padding = new Padding(6);
            _maskList.Size = new Size(260, 673);
            _maskList.TabIndex = 0;
            // 
            // _rightPanel
            // 
            _rightPanel.BackColor = Color.FromArgb(18, 18, 18);
            _rightPanel.Controls.Add(_maskList);
            _rightPanel.Controls.Add(_maskListTitle);
            _rightPanel.Dock = DockStyle.Right;
            _rightPanel.Location = new Point(1800, 60);
            _rightPanel.Name = "_rightPanel";
            _rightPanel.Padding = new Padding(0, 12, 0, 0);
            _rightPanel.Size = new Size(260, 721);
            _rightPanel.TabIndex = 2;
            _rightPanel.Visible = false;
            // 
            // _effectSubPanel
            // 
            _effectSubPanel.Controls.Add(_lblNoEffect);
            _effectSubPanel.Controls.Add(_panelColorGrading);
            _effectSubPanel.Controls.Add(_panelArtistic);
            _effectSubPanel.Controls.Add(_panelSticker);
            _effectSubPanel.Controls.Add(_panelPixelBlur);
            _effectSubPanel.Controls.Add(_panelPortrait);
            _effectSubPanel.Controls.Add(_panelGrayscale);
            _effectSubPanel.Controls.Add(_btnApplyEffect);
            _effectSubPanel.Controls.Add(_appliedEffectsPanel);
            _effectSubPanel.Dock = DockStyle.Fill;
            _effectSubPanel.Location = new Point(0, 0);
            _effectSubPanel.Name = "_effectSubPanel";
            _effectSubPanel.Padding = new Padding(16, 8, 16, 8);
            _effectSubPanel.Size = new Size(2060, 170);
            _effectSubPanel.TabIndex = 0;
            _effectSubPanel.Resize += EffectSubPanelResize;
            // 
            // _lblNoEffect
            // 
            _lblNoEffect.BackColor = Color.Transparent;
            _lblNoEffect.Dock = DockStyle.Fill;
            _lblNoEffect.Font = new Font("Segoe UI", 11F);
            _lblNoEffect.ForeColor = Color.FromArgb(100, 105, 130);
            _lblNoEffect.Location = new Point(16, 8);
            _lblNoEffect.Name = "_lblNoEffect";
            _lblNoEffect.Size = new Size(2028, 122);
            _lblNoEffect.TabIndex = 0;
            _lblNoEffect.Text = "Select an effect from the left panel to get started";
            _lblNoEffect.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // _panelColorGrading
            // 
            _panelColorGrading.BackColor = Color.Transparent;
            _panelColorGrading.Controls.Add(_cgFlow);
            _panelColorGrading.Dock = DockStyle.Fill;
            _panelColorGrading.Location = new Point(16, 8);
            _panelColorGrading.Name = "_panelColorGrading";
            _panelColorGrading.Size = new Size(2028, 122);
            _panelColorGrading.TabIndex = 0;
            _panelColorGrading.Visible = false;
            // 
            // _cgFlow
            // 
            _cgFlow.BackColor = Color.Transparent;
            _cgFlow.Controls.Add(_grpCgTint);
            _cgFlow.Controls.Add(_grpCgTs);
            _cgFlow.Controls.Add(_grpCgBr);
            _cgFlow.Controls.Add(_grpCgCo);
            _cgFlow.Controls.Add(_grpCgTb);
            _cgFlow.Dock = DockStyle.Fill;
            _cgFlow.Location = new Point(0, 0);
            _cgFlow.Name = "_cgFlow";
            _cgFlow.Size = new Size(2028, 122);
            _cgFlow.TabIndex = 0;
            // 
            // _grpCgTint
            // 
            _grpCgTint.BackColor = Color.Transparent;
            _grpCgTint.Controls.Add(_lblCgTint);
            _grpCgTint.Controls.Add(_cgTintSwatch);
            _grpCgTint.Location = new Point(0, 0);
            _grpCgTint.Margin = new Padding(0, 0, 14, 0);
            _grpCgTint.Name = "_grpCgTint";
            _grpCgTint.Size = new Size(170, 90);
            _grpCgTint.TabIndex = 0;
            // 
            // _lblCgTint
            // 
            _lblCgTint.AutoSize = true;
            _lblCgTint.BackColor = Color.Transparent;
            _lblCgTint.Font = new Font("Segoe UI", 8.5F);
            _lblCgTint.ForeColor = Color.FromArgb(160, 165, 190);
            _lblCgTint.Location = new Point(0, 0);
            _lblCgTint.Name = "_lblCgTint";
            _lblCgTint.Size = new Size(60, 15);
            _lblCgTint.TabIndex = 0;
            _lblCgTint.Text = "Tint Color";
            // 
            // _cgTintSwatch
            // 
            _cgTintSwatch.Location = new Point(0, 24);
            _cgTintSwatch.Name = "_cgTintSwatch";
            _cgTintSwatch.SelectedColor = Color.FromArgb(100, 150, 255);
            _cgTintSwatch.Size = new Size(36, 28);
            _cgTintSwatch.TabIndex = 1;
            // 
            // _grpCgTs
            // 
            _grpCgTs.BackColor = Color.Transparent;
            _grpCgTs.Controls.Add(_lblCgTs);
            _grpCgTs.Controls.Add(_cgTintStrength);
            _grpCgTs.Location = new Point(184, 0);
            _grpCgTs.Margin = new Padding(0, 0, 14, 0);
            _grpCgTs.Name = "_grpCgTs";
            _grpCgTs.Size = new Size(170, 90);
            _grpCgTs.TabIndex = 1;
            // 
            // _lblCgTs
            // 
            _lblCgTs.AutoSize = true;
            _lblCgTs.BackColor = Color.Transparent;
            _lblCgTs.Font = new Font("Segoe UI", 8.5F);
            _lblCgTs.ForeColor = Color.FromArgb(160, 165, 190);
            _lblCgTs.Location = new Point(0, 0);
            _lblCgTs.Name = "_lblCgTs";
            _lblCgTs.Size = new Size(76, 15);
            _lblCgTs.TabIndex = 0;
            _lblCgTs.Text = "Tint Strength";
            // 
            // _cgTintStrength
            // 
            _cgTintStrength.BackColor = Color.FromArgb(18, 18, 18);
            _cgTintStrength.Location = new Point(0, 24);
            _cgTintStrength.Name = "_cgTintStrength";
            _cgTintStrength.Size = new Size(160, 40);
            _cgTintStrength.TabIndex = 1;
            _cgTintStrength.Value = 0;
            // 
            // _grpCgBr
            // 
            _grpCgBr.BackColor = Color.Transparent;
            _grpCgBr.Controls.Add(_lblCgBr);
            _grpCgBr.Controls.Add(_cgBrightness);
            _grpCgBr.Location = new Point(368, 0);
            _grpCgBr.Margin = new Padding(0, 0, 14, 0);
            _grpCgBr.Name = "_grpCgBr";
            _grpCgBr.Size = new Size(170, 90);
            _grpCgBr.TabIndex = 2;
            // 
            // _lblCgBr
            // 
            _lblCgBr.AutoSize = true;
            _lblCgBr.BackColor = Color.Transparent;
            _lblCgBr.Font = new Font("Segoe UI", 8.5F);
            _lblCgBr.ForeColor = Color.FromArgb(160, 165, 190);
            _lblCgBr.Location = new Point(0, 0);
            _lblCgBr.Name = "_lblCgBr";
            _lblCgBr.Size = new Size(62, 15);
            _lblCgBr.TabIndex = 0;
            _lblCgBr.Text = "Brightness";
            // 
            // _cgBrightness
            // 
            _cgBrightness.BackColor = Color.FromArgb(18, 18, 18);
            _cgBrightness.Location = new Point(0, 24);
            _cgBrightness.Maximum = 255;
            _cgBrightness.Minimum = -255;
            _cgBrightness.Name = "_cgBrightness";
            _cgBrightness.Size = new Size(160, 40);
            _cgBrightness.TabIndex = 1;
            _cgBrightness.Value = 0;
            // 
            // _grpCgCo
            // 
            _grpCgCo.BackColor = Color.Transparent;
            _grpCgCo.Controls.Add(_lblCgCo);
            _grpCgCo.Controls.Add(_cgContrast);
            _grpCgCo.Location = new Point(552, 0);
            _grpCgCo.Margin = new Padding(0, 0, 14, 0);
            _grpCgCo.Name = "_grpCgCo";
            _grpCgCo.Size = new Size(170, 90);
            _grpCgCo.TabIndex = 3;
            // 
            // _lblCgCo
            // 
            _lblCgCo.AutoSize = true;
            _lblCgCo.BackColor = Color.Transparent;
            _lblCgCo.Font = new Font("Segoe UI", 8.5F);
            _lblCgCo.ForeColor = Color.FromArgb(160, 165, 190);
            _lblCgCo.Location = new Point(0, 0);
            _lblCgCo.Name = "_lblCgCo";
            _lblCgCo.Size = new Size(78, 15);
            _lblCgCo.TabIndex = 0;
            _lblCgCo.Text = "Contrast ×0.1";
            // 
            // _cgContrast
            // 
            _cgContrast.BackColor = Color.FromArgb(18, 18, 18);
            _cgContrast.Location = new Point(0, 24);
            _cgContrast.Maximum = 30;
            _cgContrast.Minimum = 1;
            _cgContrast.Name = "_cgContrast";
            _cgContrast.Size = new Size(160, 40);
            _cgContrast.TabIndex = 1;
            _cgContrast.Value = 10;
            // 
            // _grpCgTb
            // 
            _grpCgTb.BackColor = Color.Transparent;
            _grpCgTb.Controls.Add(_lblCgTb);
            _grpCgTb.Controls.Add(_cgTargetBg);
            _grpCgTb.Location = new Point(736, 0);
            _grpCgTb.Margin = new Padding(0, 0, 14, 0);
            _grpCgTb.Name = "_grpCgTb";
            _grpCgTb.Size = new Size(150, 90);
            _grpCgTb.TabIndex = 5;
            // 
            // _lblCgTb
            // 
            _lblCgTb.AutoSize = true;
            _lblCgTb.BackColor = Color.Transparent;
            _lblCgTb.Font = new Font("Segoe UI", 8.5F);
            _lblCgTb.ForeColor = Color.FromArgb(160, 165, 190);
            _lblCgTb.Location = new Point(0, 0);
            _lblCgTb.Name = "_lblCgTb";
            _lblCgTb.Size = new Size(40, 15);
            _lblCgTb.TabIndex = 0;
            _lblCgTb.Text = "Target";
            // 
            // _cgTargetBg
            // 
            _cgTargetBg.AutoSize = true;
            _cgTargetBg.BackColor = Color.Transparent;
            _cgTargetBg.Font = new Font("Segoe UI", 9F);
            _cgTargetBg.Location = new Point(0, 22);
            _cgTargetBg.Name = "_cgTargetBg";
            _cgTargetBg.Size = new Size(90, 19);
            _cgTargetBg.TabIndex = 1;
            _cgTargetBg.Text = "Background";
            _cgTargetBg.UseVisualStyleBackColor = false;
            // 
            // _panelArtistic
            // 
            _panelArtistic.BackColor = Color.Transparent;
            _panelArtistic.Controls.Add(_artFlow);
            _panelArtistic.Dock = DockStyle.Fill;
            _panelArtistic.Location = new Point(16, 8);
            _panelArtistic.Name = "_panelArtistic";
            _panelArtistic.Size = new Size(2028, 122);
            _panelArtistic.TabIndex = 1;
            _panelArtistic.Visible = false;
            // 
            // _artFlow
            // 
            _artFlow.BackColor = Color.Transparent;
            _artFlow.Controls.Add(_grpArtInt);
            _artFlow.Dock = DockStyle.Fill;
            _artFlow.Location = new Point(0, 0);
            _artFlow.Name = "_artFlow";
            _artFlow.Size = new Size(2028, 122);
            _artFlow.TabIndex = 0;
            // 
            // _grpArtInt
            // 
            _grpArtInt.BackColor = Color.Transparent;
            _grpArtInt.Controls.Add(_lblArtInt);
            _grpArtInt.Controls.Add(_artIntensity);
            _grpArtInt.Location = new Point(0, 0);
            _grpArtInt.Margin = new Padding(0, 0, 14, 0);
            _grpArtInt.Name = "_grpArtInt";
            _grpArtInt.Size = new Size(170, 90);
            _grpArtInt.TabIndex = 0;
            // 
            // _lblArtInt
            // 
            _lblArtInt.AutoSize = true;
            _lblArtInt.BackColor = Color.Transparent;
            _lblArtInt.Font = new Font("Segoe UI", 8.5F);
            _lblArtInt.ForeColor = Color.FromArgb(160, 165, 190);
            _lblArtInt.Location = new Point(0, 0);
            _lblArtInt.Name = "_lblArtInt";
            _lblArtInt.Size = new Size(90, 15);
            _lblArtInt.TabIndex = 0;
            _lblArtInt.Text = "Sketch Intensity";
            // 
            // _artIntensity
            // 
            _artIntensity.BackColor = Color.FromArgb(18, 18, 18);
            _artIntensity.Location = new Point(0, 24);
            _artIntensity.Name = "_artIntensity";
            _artIntensity.Size = new Size(160, 40);
            _artIntensity.TabIndex = 1;
            // 
            // _panelSticker
            // 
            _panelSticker.BackColor = Color.Transparent;
            _panelSticker.Controls.Add(_stFlow);
            _panelSticker.Dock = DockStyle.Fill;
            _panelSticker.Location = new Point(16, 8);
            _panelSticker.Name = "_panelSticker";
            _panelSticker.Size = new Size(2028, 122);
            _panelSticker.TabIndex = 2;
            _panelSticker.Visible = false;
            // 
            // _stFlow
            // 
            _stFlow.BackColor = Color.Transparent;
            _stFlow.Controls.Add(_grpStSc);
            _stFlow.Controls.Add(_grpStRot);
            _stFlow.Controls.Add(_grpStBc);
            _stFlow.Controls.Add(_grpStBt);
            _stFlow.Controls.Add(_grpStSh);
            _stFlow.Controls.Add(_grpStBg);
            _stFlow.Dock = DockStyle.Fill;
            _stFlow.Location = new Point(0, 0);
            _stFlow.Name = "_stFlow";
            _stFlow.Size = new Size(2028, 122);
            _stFlow.TabIndex = 0;
            // 
            // _grpStSc
            // 
            _grpStSc.BackColor = Color.Transparent;
            _grpStSc.Controls.Add(_lblStSc);
            _grpStSc.Controls.Add(_stScale);
            _grpStSc.Location = new Point(0, 0);
            _grpStSc.Margin = new Padding(0, 0, 14, 0);
            _grpStSc.Name = "_grpStSc";
            _grpStSc.Size = new Size(170, 90);
            _grpStSc.TabIndex = 0;
            // 
            // _lblStSc
            // 
            _lblStSc.AutoSize = true;
            _lblStSc.BackColor = Color.Transparent;
            _lblStSc.Font = new Font("Segoe UI", 8.5F);
            _lblStSc.ForeColor = Color.FromArgb(160, 165, 190);
            _lblStSc.Location = new Point(0, 0);
            _lblStSc.Name = "_lblStSc";
            _lblStSc.Size = new Size(68, 15);
            _lblStSc.TabIndex = 0;
            _lblStSc.Text = "Scale (×0.1)";
            // 
            // _stScale
            // 
            _stScale.BackColor = Color.FromArgb(18, 18, 18);
            _stScale.Location = new Point(0, 24);
            _stScale.Maximum = 50;
            _stScale.Minimum = 1;
            _stScale.Name = "_stScale";
            _stScale.Size = new Size(160, 40);
            _stScale.TabIndex = 1;
            _stScale.Value = 10;
            // 
            // _grpStRot
            // 
            _grpStRot.BackColor = Color.Transparent;
            _grpStRot.Controls.Add(_lblStRot);
            _grpStRot.Controls.Add(_stRotation);
            _grpStRot.Location = new Point(184, 0);
            _grpStRot.Margin = new Padding(0, 0, 14, 0);
            _grpStRot.Name = "_grpStRot";
            _grpStRot.Size = new Size(170, 90);
            _grpStRot.TabIndex = 1;
            // 
            // _lblStRot
            // 
            _lblStRot.AutoSize = true;
            _lblStRot.BackColor = Color.Transparent;
            _lblStRot.Font = new Font("Segoe UI", 8.5F);
            _lblStRot.ForeColor = Color.FromArgb(160, 165, 190);
            _lblStRot.Location = new Point(0, 0);
            _lblStRot.Name = "_lblStRot";
            _lblStRot.Size = new Size(60, 15);
            _lblStRot.TabIndex = 0;
            _lblStRot.Text = "Rotation °";
            // 
            // _stRotation
            // 
            _stRotation.BackColor = Color.FromArgb(18, 18, 18);
            _stRotation.Location = new Point(0, 24);
            _stRotation.Maximum = 180;
            _stRotation.Minimum = -180;
            _stRotation.Name = "_stRotation";
            _stRotation.Size = new Size(160, 40);
            _stRotation.TabIndex = 1;
            _stRotation.Value = 0;
            // 
            // _grpStBc
            // 
            _grpStBc.BackColor = Color.Transparent;
            _grpStBc.Controls.Add(_lblStBc);
            _grpStBc.Controls.Add(_stBorderColor);
            _grpStBc.Location = new Point(368, 0);
            _grpStBc.Margin = new Padding(0, 0, 14, 0);
            _grpStBc.Name = "_grpStBc";
            _grpStBc.Size = new Size(140, 90);
            _grpStBc.TabIndex = 2;
            // 
            // _lblStBc
            // 
            _lblStBc.AutoSize = true;
            _lblStBc.BackColor = Color.Transparent;
            _lblStBc.Font = new Font("Segoe UI", 8.5F);
            _lblStBc.ForeColor = Color.FromArgb(160, 165, 190);
            _lblStBc.Location = new Point(0, 0);
            _lblStBc.Name = "_lblStBc";
            _lblStBc.Size = new Size(74, 15);
            _lblStBc.TabIndex = 0;
            _lblStBc.Text = "Border Color";
            // 
            // _stBorderColor
            // 
            _stBorderColor.Location = new Point(0, 22);
            _stBorderColor.Name = "_stBorderColor";
            _stBorderColor.Size = new Size(36, 28);
            _stBorderColor.TabIndex = 1;
            // 
            // _grpStBt
            // 
            _grpStBt.BackColor = Color.Transparent;
            _grpStBt.Controls.Add(_lblStBt);
            _grpStBt.Controls.Add(_stThickness);
            _grpStBt.Location = new Point(522, 0);
            _grpStBt.Margin = new Padding(0, 0, 14, 0);
            _grpStBt.Name = "_grpStBt";
            _grpStBt.Size = new Size(170, 90);
            _grpStBt.TabIndex = 3;
            // 
            // _lblStBt
            // 
            _lblStBt.AutoSize = true;
            _lblStBt.BackColor = Color.Transparent;
            _lblStBt.Font = new Font("Segoe UI", 8.5F);
            _lblStBt.ForeColor = Color.FromArgb(160, 165, 190);
            _lblStBt.Location = new Point(0, 0);
            _lblStBt.Name = "_lblStBt";
            _lblStBt.Size = new Size(97, 15);
            _lblStBt.TabIndex = 0;
            _lblStBt.Text = "Border Thickness";
            // 
            // _stThickness
            // 
            _stThickness.BackColor = Color.FromArgb(18, 18, 18);
            _stThickness.Location = new Point(0, 24);
            _stThickness.Maximum = 30;
            _stThickness.Minimum = 1;
            _stThickness.Name = "_stThickness";
            _stThickness.Size = new Size(160, 40);
            _stThickness.TabIndex = 1;
            _stThickness.Value = 15;
            // 
            // _grpStSh
            // 
            _grpStSh.BackColor = Color.Transparent;
            _grpStSh.Controls.Add(_lblStSh);
            _grpStSh.Controls.Add(_stShadowBlur);
            _grpStSh.Location = new Point(706, 0);
            _grpStSh.Margin = new Padding(0, 0, 14, 0);
            _grpStSh.Name = "_grpStSh";
            _grpStSh.Size = new Size(170, 90);
            _grpStSh.TabIndex = 4;
            // 
            // _lblStSh
            // 
            _lblStSh.AutoSize = true;
            _lblStSh.BackColor = Color.Transparent;
            _lblStSh.Font = new Font("Segoe UI", 8.5F);
            _lblStSh.ForeColor = Color.FromArgb(160, 165, 190);
            _lblStSh.Location = new Point(0, 0);
            _lblStSh.Name = "_lblStSh";
            _lblStSh.Size = new Size(73, 15);
            _lblStSh.TabIndex = 0;
            _lblStSh.Text = "Shadow Blur";
            // 
            // _stShadowBlur
            // 
            _stShadowBlur.BackColor = Color.FromArgb(18, 18, 18);
            _stShadowBlur.Location = new Point(0, 24);
            _stShadowBlur.Maximum = 51;
            _stShadowBlur.Minimum = 1;
            _stShadowBlur.Name = "_stShadowBlur";
            _stShadowBlur.Size = new Size(160, 40);
            _stShadowBlur.TabIndex = 1;
            _stShadowBlur.Value = 15;
            // 
            // _grpStBg
            // 
            _grpStBg.BackColor = Color.Transparent;
            _grpStBg.Controls.Add(_lblStBg);
            _grpStBg.Controls.Add(_rdoStickerOriginalBg);
            _grpStBg.Controls.Add(_rdoStickerColorBg);
            _grpStBg.Controls.Add(_rdoStickerImageBg);
            _grpStBg.Controls.Add(_rdoStickerTransparentBg);
            _grpStBg.Controls.Add(_stBgColorSwatch);
            _grpStBg.Controls.Add(_btnStickerUploadBg);
            _grpStBg.Location = new Point(890, 0);
            _grpStBg.Margin = new Padding(0, 0, 14, 0);
            _grpStBg.Name = "_grpStBg";
            _grpStBg.Size = new Size(360, 90);
            _grpStBg.TabIndex = 5;
            // 
            // _lblStBg
            // 
            _lblStBg.AutoSize = true;
            _lblStBg.BackColor = Color.Transparent;
            _lblStBg.Font = new Font("Segoe UI", 8.5F);
            _lblStBg.ForeColor = Color.FromArgb(160, 165, 190);
            _lblStBg.Location = new Point(0, 0);
            _lblStBg.Name = "_lblStBg";
            _lblStBg.Size = new Size(71, 15);
            _lblStBg.TabIndex = 0;
            _lblStBg.Text = "Background";
            // 
            // _rdoStickerOriginalBg
            // 
            _rdoStickerOriginalBg.AutoSize = true;
            _rdoStickerOriginalBg.BackColor = Color.Transparent;
            _rdoStickerOriginalBg.Checked = true;
            _rdoStickerOriginalBg.Font = new Font("Segoe UI", 8.5F);
            _rdoStickerOriginalBg.Location = new Point(0, 18);
            _rdoStickerOriginalBg.Name = "_rdoStickerOriginalBg";
            _rdoStickerOriginalBg.Size = new Size(67, 19);
            _rdoStickerOriginalBg.TabIndex = 1;
            _rdoStickerOriginalBg.TabStop = true;
            _rdoStickerOriginalBg.Text = "Original";
            _rdoStickerOriginalBg.UseVisualStyleBackColor = false;
            // 
            // _rdoStickerColorBg
            // 
            _rdoStickerColorBg.AutoSize = true;
            _rdoStickerColorBg.BackColor = Color.Transparent;
            _rdoStickerColorBg.Font = new Font("Segoe UI", 8.5F);
            _rdoStickerColorBg.Location = new Point(70, 18);
            _rdoStickerColorBg.Name = "_rdoStickerColorBg";
            _rdoStickerColorBg.Size = new Size(83, 19);
            _rdoStickerColorBg.TabIndex = 2;
            _rdoStickerColorBg.Text = "Solid Color";
            _rdoStickerColorBg.UseVisualStyleBackColor = false;
            // 
            // _rdoStickerImageBg
            // 
            _rdoStickerImageBg.AutoSize = true;
            _rdoStickerImageBg.BackColor = Color.Transparent;
            _rdoStickerImageBg.Font = new Font("Segoe UI", 8.5F);
            _rdoStickerImageBg.Location = new Point(148, 18);
            _rdoStickerImageBg.Name = "_rdoStickerImageBg";
            _rdoStickerImageBg.Size = new Size(99, 19);
            _rdoStickerImageBg.TabIndex = 3;
            _rdoStickerImageBg.Text = "Upload Image";
            _rdoStickerImageBg.UseVisualStyleBackColor = false;
            // 
            // _rdoStickerTransparentBg
            // 
            _rdoStickerTransparentBg.AutoSize = true;
            _rdoStickerTransparentBg.BackColor = Color.Transparent;
            _rdoStickerTransparentBg.Font = new Font("Segoe UI", 8.5F);
            _rdoStickerTransparentBg.Location = new Point(250, 18);
            _rdoStickerTransparentBg.Name = "_rdoStickerTransparentBg";
            _rdoStickerTransparentBg.Size = new Size(87, 19);
            _rdoStickerTransparentBg.TabIndex = 6;
            _rdoStickerTransparentBg.Text = "Transparent";
            _rdoStickerTransparentBg.UseVisualStyleBackColor = false;
            // 
            // _stBgColorSwatch
            // 
            _stBgColorSwatch.Location = new Point(70, 42);
            _stBgColorSwatch.Name = "_stBgColorSwatch";
            _stBgColorSwatch.SelectedColor = Color.FromArgb(30, 30, 50);
            _stBgColorSwatch.Size = new Size(28, 22);
            _stBgColorSwatch.TabIndex = 4;
            _stBgColorSwatch.Visible = false;
            // 
            // _btnStickerUploadBg
            // 
            _btnStickerUploadBg.FlatStyle = FlatStyle.Flat;
            _btnStickerUploadBg.Font = new Font("Segoe UI", 8F);
            _btnStickerUploadBg.Location = new Point(148, 42);
            _btnStickerUploadBg.Name = "_btnStickerUploadBg";
            _btnStickerUploadBg.Size = new Size(80, 24);
            _btnStickerUploadBg.TabIndex = 5;
            _btnStickerUploadBg.Text = "Browse…";
            _btnStickerUploadBg.Visible = false;
            // 
            // _panelPixelBlur
            // 
            _panelPixelBlur.BackColor = Color.Transparent;
            _panelPixelBlur.Controls.Add(_pbFlow);
            _panelPixelBlur.Dock = DockStyle.Fill;
            _panelPixelBlur.Location = new Point(16, 8);
            _panelPixelBlur.Name = "_panelPixelBlur";
            _panelPixelBlur.Size = new Size(2028, 122);
            _panelPixelBlur.TabIndex = 3;
            _panelPixelBlur.Visible = false;
            // 
            // _pbFlow
            // 
            _pbFlow.BackColor = Color.Transparent;
            _pbFlow.Controls.Add(_grpPbMode);
            _pbFlow.Controls.Add(_grpPbInt);
            _pbFlow.Controls.Add(_grpPbTarget);
            _pbFlow.Dock = DockStyle.Fill;
            _pbFlow.Location = new Point(0, 0);
            _pbFlow.Name = "_pbFlow";
            _pbFlow.Size = new Size(2028, 122);
            _pbFlow.TabIndex = 0;
            _pbFlow.WrapContents = false;
            // 
            // _grpPbMode
            // 
            _grpPbMode.BackColor = Color.Transparent;
            _grpPbMode.Controls.Add(_lblPbMode);
            _grpPbMode.Controls.Add(_btnPixelMode);
            _grpPbMode.Controls.Add(_btnBlurMode);
            _grpPbMode.Location = new Point(0, 0);
            _grpPbMode.Margin = new Padding(0, 0, 14, 0);
            _grpPbMode.Name = "_grpPbMode";
            _grpPbMode.Size = new Size(170, 90);
            _grpPbMode.TabIndex = 0;
            // 
            // _lblPbMode
            // 
            _lblPbMode.AutoSize = true;
            _lblPbMode.BackColor = Color.Transparent;
            _lblPbMode.Font = new Font("Segoe UI", 8.5F);
            _lblPbMode.ForeColor = Color.FromArgb(160, 165, 190);
            _lblPbMode.Location = new Point(0, 0);
            _lblPbMode.Name = "_lblPbMode";
            _lblPbMode.Size = new Size(38, 15);
            _lblPbMode.TabIndex = 0;
            _lblPbMode.Text = "Mode";
            // 
            // _btnPixelMode
            // 
            _btnPixelMode.FlatStyle = FlatStyle.Flat;
            _btnPixelMode.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            _btnPixelMode.Location = new Point(0, 24);
            _btnPixelMode.Margin = new Padding(0, 0, 4, 0);
            _btnPixelMode.Name = "_btnPixelMode";
            _btnPixelMode.Size = new Size(70, 28);
            _btnPixelMode.TabIndex = 1;
            _btnPixelMode.Text = "Pixelate";
            // 
            // _btnBlurMode
            // 
            _btnBlurMode.FlatStyle = FlatStyle.Flat;
            _btnBlurMode.Font = new Font("Segoe UI", 8F);
            _btnBlurMode.Location = new Point(74, 20);
            _btnBlurMode.Name = "_btnBlurMode";
            _btnBlurMode.Size = new Size(52, 28);
            _btnBlurMode.TabIndex = 2;
            _btnBlurMode.Text = "Blur";
            // 
            // _grpPbInt
            // 
            _grpPbInt.BackColor = Color.Transparent;
            _grpPbInt.Controls.Add(_lblPbInt);
            _grpPbInt.Controls.Add(_pbIntensity);
            _grpPbInt.Location = new Point(184, 0);
            _grpPbInt.Margin = new Padding(0, 0, 14, 0);
            _grpPbInt.Name = "_grpPbInt";
            _grpPbInt.Size = new Size(170, 90);
            _grpPbInt.TabIndex = 1;
            // 
            // _lblPbInt
            // 
            _lblPbInt.AutoSize = true;
            _lblPbInt.BackColor = Color.Transparent;
            _lblPbInt.Font = new Font("Segoe UI", 8.5F);
            _lblPbInt.ForeColor = Color.FromArgb(160, 165, 190);
            _lblPbInt.Location = new Point(0, 0);
            _lblPbInt.Name = "_lblPbInt";
            _lblPbInt.Size = new Size(52, 15);
            _lblPbInt.TabIndex = 0;
            _lblPbInt.Text = "Intensity";
            // 
            // _pbIntensity
            // 
            _pbIntensity.BackColor = Color.FromArgb(18, 18, 18);
            _pbIntensity.Location = new Point(0, 24);
            _pbIntensity.Minimum = 1;
            _pbIntensity.Name = "_pbIntensity";
            _pbIntensity.Size = new Size(160, 40);
            _pbIntensity.TabIndex = 1;
            _pbIntensity.Value = 40;
            // 
            // _grpPbTarget
            // 
            _grpPbTarget.BackColor = Color.Transparent;
            _grpPbTarget.Controls.Add(_lblPbTarget);
            _grpPbTarget.Controls.Add(_btnPbForeground);
            _grpPbTarget.Controls.Add(_btnPbBackground);
            _grpPbTarget.Location = new Point(368, 0);
            _grpPbTarget.Margin = new Padding(0, 0, 14, 0);
            _grpPbTarget.Name = "_grpPbTarget";
            _grpPbTarget.Size = new Size(200, 90);
            _grpPbTarget.TabIndex = 2;
            // 
            // _lblPbTarget
            // 
            _lblPbTarget.AutoSize = true;
            _lblPbTarget.BackColor = Color.Transparent;
            _lblPbTarget.Font = new Font("Segoe UI", 8.5F);
            _lblPbTarget.ForeColor = Color.FromArgb(160, 165, 190);
            _lblPbTarget.Location = new Point(0, 0);
            _lblPbTarget.Name = "_lblPbTarget";
            _lblPbTarget.Size = new Size(40, 15);
            _lblPbTarget.TabIndex = 0;
            _lblPbTarget.Text = "Target";
            // 
            // _btnPbForeground
            // 
            _btnPbForeground.FlatStyle = FlatStyle.Flat;
            _btnPbForeground.Font = new Font("Segoe UI", 8F, FontStyle.Bold);
            _btnPbForeground.Location = new Point(0, 24);
            _btnPbForeground.Margin = new Padding(0, 0, 4, 0);
            _btnPbForeground.Name = "_btnPbForeground";
            _btnPbForeground.Size = new Size(80, 28);
            _btnPbForeground.TabIndex = 1;
            _btnPbForeground.Text = "Foreground";
            // 
            // _btnPbBackground
            // 
            _btnPbBackground.FlatStyle = FlatStyle.Flat;
            _btnPbBackground.Font = new Font("Segoe UI", 8F);
            _btnPbBackground.Location = new Point(84, 20);
            _btnPbBackground.Name = "_btnPbBackground";
            _btnPbBackground.Size = new Size(80, 28);
            _btnPbBackground.TabIndex = 2;
            _btnPbBackground.Text = "Background";
            // 
            // _panelPortrait
            // 
            _panelPortrait.BackColor = Color.Transparent;
            _panelPortrait.Controls.Add(_ptFlow);
            _panelPortrait.Dock = DockStyle.Fill;
            _panelPortrait.Location = new Point(16, 8);
            _panelPortrait.Name = "_panelPortrait";
            _panelPortrait.Size = new Size(2028, 122);
            _panelPortrait.TabIndex = 4;
            _panelPortrait.Visible = false;
            // 
            // _ptFlow
            // 
            _ptFlow.BackColor = Color.Transparent;
            _ptFlow.Controls.Add(_grpPtBlur);
            _ptFlow.Controls.Add(_grpPtFeather);
            _ptFlow.Dock = DockStyle.Fill;
            _ptFlow.Location = new Point(0, 0);
            _ptFlow.Name = "_ptFlow";
            _ptFlow.Size = new Size(2028, 122);
            _ptFlow.TabIndex = 0;
            _ptFlow.WrapContents = false;
            // 
            // _grpPtBlur
            // 
            _grpPtBlur.BackColor = Color.Transparent;
            _grpPtBlur.Controls.Add(_lblPtBlur);
            _grpPtBlur.Controls.Add(_ptBlurStrength);
            _grpPtBlur.Location = new Point(0, 0);
            _grpPtBlur.Margin = new Padding(0, 0, 14, 0);
            _grpPtBlur.Name = "_grpPtBlur";
            _grpPtBlur.Size = new Size(170, 90);
            _grpPtBlur.TabIndex = 0;
            // 
            // _lblPtBlur
            // 
            _lblPtBlur.AutoSize = true;
            _lblPtBlur.BackColor = Color.Transparent;
            _lblPtBlur.Font = new Font("Segoe UI", 8.5F);
            _lblPtBlur.ForeColor = Color.FromArgb(160, 165, 190);
            _lblPtBlur.Location = new Point(0, 0);
            _lblPtBlur.Name = "_lblPtBlur";
            _lblPtBlur.Size = new Size(76, 15);
            _lblPtBlur.TabIndex = 0;
            _lblPtBlur.Text = "Blur Strength";
            // 
            // _ptBlurStrength
            // 
            _ptBlurStrength.BackColor = Color.FromArgb(18, 18, 18);
            _ptBlurStrength.Location = new Point(0, 24);
            _ptBlurStrength.Maximum = 101;
            _ptBlurStrength.Minimum = 3;
            _ptBlurStrength.Name = "_ptBlurStrength";
            _ptBlurStrength.Size = new Size(160, 40);
            _ptBlurStrength.TabIndex = 1;
            _ptBlurStrength.Value = 51;
            // 
            // _grpPtFeather
            // 
            _grpPtFeather.BackColor = Color.Transparent;
            _grpPtFeather.Controls.Add(_lblPtFeather);
            _grpPtFeather.Controls.Add(_ptFeatherAmount);
            _grpPtFeather.Location = new Point(184, 0);
            _grpPtFeather.Margin = new Padding(0, 0, 14, 0);
            _grpPtFeather.Name = "_grpPtFeather";
            _grpPtFeather.Size = new Size(170, 90);
            _grpPtFeather.TabIndex = 1;
            // 
            // _lblPtFeather
            // 
            _lblPtFeather.AutoSize = true;
            _lblPtFeather.BackColor = Color.Transparent;
            _lblPtFeather.Font = new Font("Segoe UI", 8.5F);
            _lblPtFeather.ForeColor = Color.FromArgb(160, 165, 190);
            _lblPtFeather.Location = new Point(0, 0);
            _lblPtFeather.Name = "_lblPtFeather";
            _lblPtFeather.Size = new Size(75, 15);
            _lblPtFeather.TabIndex = 0;
            _lblPtFeather.Text = "Edge Feather";
            // 
            // _ptFeatherAmount
            // 
            _ptFeatherAmount.BackColor = Color.FromArgb(18, 18, 18);
            _ptFeatherAmount.Location = new Point(0, 24);
            _ptFeatherAmount.Maximum = 51;
            _ptFeatherAmount.Name = "_ptFeatherAmount";
            _ptFeatherAmount.Size = new Size(160, 40);
            _ptFeatherAmount.TabIndex = 1;
            _ptFeatherAmount.Value = 21;
            // 
            // _panelGrayscale
            // 
            _panelGrayscale.BackColor = Color.Transparent;
            _panelGrayscale.Controls.Add(_gsFlow);
            _panelGrayscale.Dock = DockStyle.Fill;
            _panelGrayscale.Location = new Point(16, 8);
            _panelGrayscale.Name = "_panelGrayscale";
            _panelGrayscale.Size = new Size(2028, 122);
            _panelGrayscale.TabIndex = 5;
            _panelGrayscale.Visible = false;
            // 
            // _gsFlow
            // 
            _gsFlow.BackColor = Color.Transparent;
            _gsFlow.Controls.Add(_grpGsTb);
            _gsFlow.Dock = DockStyle.Fill;
            _gsFlow.Location = new Point(0, 0);
            _gsFlow.Name = "_gsFlow";
            _gsFlow.Size = new Size(2028, 122);
            _gsFlow.TabIndex = 0;
            // 
            // _grpGsTb
            // 
            _grpGsTb.BackColor = Color.Transparent;
            _grpGsTb.Controls.Add(_lblGsTb);
            _grpGsTb.Controls.Add(_gsTargetBg);
            _grpGsTb.Location = new Point(0, 0);
            _grpGsTb.Margin = new Padding(0, 0, 14, 0);
            _grpGsTb.Name = "_grpGsTb";
            _grpGsTb.Size = new Size(150, 90);
            _grpGsTb.TabIndex = 0;
            // 
            // _lblGsTb
            // 
            _lblGsTb.AutoSize = true;
            _lblGsTb.BackColor = Color.Transparent;
            _lblGsTb.Font = new Font("Segoe UI", 8.5F);
            _lblGsTb.ForeColor = Color.FromArgb(160, 165, 190);
            _lblGsTb.Location = new Point(0, 0);
            _lblGsTb.Name = "_lblGsTb";
            _lblGsTb.Size = new Size(40, 15);
            _lblGsTb.TabIndex = 0;
            _lblGsTb.Text = "Target";
            // 
            // _gsTargetBg
            // 
            _gsTargetBg.AutoSize = true;
            _gsTargetBg.BackColor = Color.Transparent;
            _gsTargetBg.Font = new Font("Segoe UI", 9F);
            _gsTargetBg.Location = new Point(0, 22);
            _gsTargetBg.Name = "_gsTargetBg";
            _gsTargetBg.Size = new Size(90, 19);
            _gsTargetBg.TabIndex = 1;
            _gsTargetBg.Text = "Background";
            _gsTargetBg.UseVisualStyleBackColor = false;
            // 
            // _btnApplyEffect
            // 
            _btnApplyEffect.Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
            _btnApplyEffect.BackColor = Color.FromArgb(14, 48, 52);
            _btnApplyEffect.FlatAppearance.BorderSize = 2;
            _btnApplyEffect.FlatStyle = FlatStyle.Flat;
            _btnApplyEffect.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            _btnApplyEffect.Location = new Point(1860, 70);
            _btnApplyEffect.Name = "_btnApplyEffect";
            _btnApplyEffect.Size = new Size(120, 44);
            _btnApplyEffect.TabIndex = 6;
            _btnApplyEffect.Text = "Apply";
            _btnApplyEffect.UseVisualStyleBackColor = false;
            _btnApplyEffect.Visible = false;
            // 
            // _appliedEffectsPanel
            // 
            _appliedEffectsPanel.BackColor = Color.Transparent;
            _appliedEffectsPanel.Dock = DockStyle.Bottom;
            _appliedEffectsPanel.Location = new Point(16, 130);
            _appliedEffectsPanel.Name = "_appliedEffectsPanel";
            _appliedEffectsPanel.Padding = new Padding(0, 2, 0, 2);
            _appliedEffectsPanel.Size = new Size(2028, 32);
            _appliedEffectsPanel.TabIndex = 10;
            _appliedEffectsPanel.Visible = false;
            _appliedEffectsPanel.WrapContents = false;
            // 
            // _lblSelMode
            // 
            _lblSelMode.Anchor = AnchorStyles.Left;
            _lblSelMode.BackColor = Color.Transparent;
            _lblSelMode.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            _lblSelMode.ForeColor = Color.FromArgb(160, 165, 190);
            _lblSelMode.Location = new Point(306, 12);
            _lblSelMode.Name = "_lblSelMode";
            _lblSelMode.Size = new Size(130, 42);
            _lblSelMode.TabIndex = 0;
            _lblSelMode.Text = "Selection Mode";
            _lblSelMode.TextAlign = ContentAlignment.MiddleRight;
            _lblSelMode.Click += _lblSelMode_Click_1;
            // 
            // _btnBBox
            // 
            _btnBBox.Anchor = AnchorStyles.Left;
            _btnBBox.BackColor = Color.Transparent;
            _btnBBox.FlatAppearance.BorderSize = 0;
            _btnBBox.FlatStyle = FlatStyle.Flat;
            _btnBBox.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            _btnBBox.Location = new Point(452, 12);
            _btnBBox.Name = "_btnBBox";
            _btnBBox.Size = new Size(155, 42);
            _btnBBox.TabIndex = 1;
            _btnBBox.Text = "Bounding Box";
            _btnBBox.UseVisualStyleBackColor = false;
            // 
            // _btnPrompt
            // 
            _btnPrompt.Anchor = AnchorStyles.Left;
            _btnPrompt.BackColor = Color.Transparent;
            _btnPrompt.FlatAppearance.BorderSize = 0;
            _btnPrompt.FlatStyle = FlatStyle.Flat;
            _btnPrompt.Font = new Font("Segoe UI", 10F);
            _btnPrompt.Location = new Point(628, 9);
            _btnPrompt.Name = "_btnPrompt";
            _btnPrompt.Size = new Size(110, 42);
            _btnPrompt.TabIndex = 2;
            _btnPrompt.Text = "Prompt";
            _btnPrompt.UseVisualStyleBackColor = false;
            // 
            // _promptBox
            // 
            _promptBox.Anchor = AnchorStyles.Left;
            _promptBox.BackColor = Color.FromArgb(30, 32, 36);
            _promptBox.BorderColor = Color.FromArgb(0, 229, 255);
            _promptBox.Font = new Font("Segoe UI", 10.5F);
            _promptBox.ForeColor = Color.FromArgb(220, 220, 220);
            _promptBox.Location = new Point(752, 11);
            _promptBox.Name = "_promptBox";
            _promptBox.PlaceholderText = "Describe the object to segment…";
            _promptBox.Size = new Size(662, 40);
            _promptBox.TabIndex = 3;
            _promptBox.Visible = false;
            // 
            // _btnSegment
            // 
            _btnSegment.Anchor = AnchorStyles.Left;
            _btnSegment.BackColor = Color.FromArgb(0, 150, 255);
            _btnSegment.FlatStyle = FlatStyle.Flat;
            _btnSegment.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            _btnSegment.ForeColor = Color.White;
            _btnSegment.Icon = "🔍";
            _btnSegment.Location = new Point(1446, 9);
            _btnSegment.Name = "_btnSegment";
            _btnSegment.Size = new Size(135, 42);
            _btnSegment.TabIndex = 4;
            _btnSegment.Text = "Segment";
            _btnSegment.UseVisualStyleBackColor = false;
            // 
            // _serverPanel
            // 
            _serverPanel.BackColor = Color.Transparent;
            _serverPanel.Controls.Add(_lblServer);
            _serverPanel.Controls.Add(_txtServerUrl);
            _serverPanel.Controls.Add(_lblServerStatus);
            _serverPanel.Location = new Point(0, 0);
            _serverPanel.Name = "_serverPanel";
            _serverPanel.Size = new Size(0, 0);
            _serverPanel.TabIndex = 6;
            _serverPanel.Visible = false;
            // 
            // _lblServer
            // 
            _lblServer.Location = new Point(0, 0);
            _lblServer.Name = "_lblServer";
            _lblServer.Size = new Size(100, 23);
            _lblServer.TabIndex = 0;
            // 
            // _txtServerUrl
            // 
            _txtServerUrl.Location = new Point(0, 0);
            _txtServerUrl.Name = "_txtServerUrl";
            _txtServerUrl.Size = new Size(100, 23);
            _txtServerUrl.TabIndex = 1;
            // 
            // _lblServerStatus
            // 
            _lblServerStatus.Location = new Point(0, 0);
            _lblServerStatus.Name = "_lblServerStatus";
            _lblServerStatus.Size = new Size(100, 23);
            _lblServerStatus.TabIndex = 2;
            // 
            // _topBar
            // 
            _topBar.BackColor = Color.FromArgb(22, 22, 22);
            _topBar.Controls.Add(label1);
            _topBar.Controls.Add(_lblSelMode);
            _topBar.Controls.Add(_btnBBox);
            _topBar.Controls.Add(_btnPrompt);
            _topBar.Controls.Add(_promptBox);
            _topBar.Controls.Add(_btnSegment);
            _topBar.Controls.Add(_btnStartServer);
            _topBar.Controls.Add(_serverPanel);
            _topBar.Dock = DockStyle.Top;
            _topBar.Location = new Point(0, 0);
            _topBar.Name = "_topBar";
            _topBar.Padding = new Padding(0, 0, 0, 6);
            _topBar.Size = new Size(2060, 60);
            _topBar.TabIndex = 4;
            _topBar.Resize += TopBarResize;
            // 
            // label1
            // 
            label1.Anchor = AnchorStyles.Left;
            label1.BackColor = Color.Transparent;
            label1.Font = new Font("Segoe UI", 14.25F, FontStyle.Bold, GraphicsUnit.Point, 0);
            label1.ForeColor = Color.FromArgb(160, 165, 190);
            label1.Location = new Point(12, 9);
            label1.Name = "label1";
            label1.Size = new Size(130, 42);
            label1.TabIndex = 7;
            label1.Text = "Vision Edit";
            label1.TextAlign = ContentAlignment.MiddleRight;
            label1.Click += label1_Click;
            // 
            // _btnStartServer
            // 
            _btnStartServer.Anchor = AnchorStyles.Right;
            _btnStartServer.BackColor = Color.FromArgb(0, 160, 80);
            _btnStartServer.FlatAppearance.BorderSize = 0;
            _btnStartServer.FlatStyle = FlatStyle.Flat;
            _btnStartServer.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            _btnStartServer.ForeColor = Color.White;
            _btnStartServer.Location = new Point(1860, 9);
            _btnStartServer.Name = "_btnStartServer";
            _btnStartServer.Size = new Size(175, 42);
            _btnStartServer.TabIndex = 5;
            _btnStartServer.Text = "Start Server";
            _btnStartServer.UseVisualStyleBackColor = false;
            // 
            // _bottomBar
            // 
            _bottomBar.BackColor = Color.FromArgb(22, 22, 22);
            _bottomBar.Dock = DockStyle.Bottom;
            _bottomBar.Location = new Point(0, 0);
            _bottomBar.Name = "_bottomBar";
            _bottomBar.Size = new Size(1440, 0);
            _bottomBar.TabIndex = 1;
            _bottomBar.Visible = false;
            // 
            // _bottomContainer
            // 
            _bottomContainer.BackColor = Color.FromArgb(18, 18, 18);
            _bottomContainer.Controls.Add(_effectSubPanel);
            _bottomContainer.Dock = DockStyle.Bottom;
            _bottomContainer.Location = new Point(0, 781);
            _bottomContainer.Name = "_bottomContainer";
            _bottomContainer.Size = new Size(2060, 170);
            _bottomContainer.TabIndex = 3;
            // 
            // _btnResetAll
            // 
            _btnResetAll.BackColor = Color.FromArgb(80, 30, 30);
            _btnResetAll.FlatAppearance.BorderSize = 0;
            _btnResetAll.FlatStyle = FlatStyle.Flat;
            _btnResetAll.Font = new Font("Segoe UI", 7.5F, FontStyle.Bold);
            _btnResetAll.ForeColor = Color.FromArgb(255, 120, 120);
            _btnResetAll.Location = new Point(0, 0);
            _btnResetAll.Margin = new Padding(4, 2, 4, 2);
            _btnResetAll.Name = "_btnResetAll";
            _btnResetAll.Size = new Size(72, 24);
            _btnResetAll.TabIndex = 0;
            _btnResetAll.Text = "Reset All";
            _btnResetAll.UseVisualStyleBackColor = false;
            // 
            // _loadingLabel
            // 
            _loadingLabel.BackColor = Color.Transparent;
            _loadingLabel.Dock = DockStyle.Fill;
            _loadingLabel.Font = new Font("Segoe UI", 11F, FontStyle.Bold);
            _loadingLabel.ForeColor = Color.FromArgb(220, 220, 220);
            _loadingLabel.Location = new Point(0, 0);
            _loadingLabel.Name = "_loadingLabel";
            _loadingLabel.Size = new Size(1548, 673);
            _loadingLabel.TabIndex = 0;
            _loadingLabel.Text = "Segmenting... (server may take 6–7 min to start)";
            _loadingLabel.TextAlign = ContentAlignment.MiddleCenter;
            // 
            // _loadingOverlay
            // 
            _loadingOverlay.BackColor = Color.FromArgb(200, 18, 19, 26);
            _loadingOverlay.Controls.Add(_loadingLabel);
            _loadingOverlay.Dock = DockStyle.Fill;
            _loadingOverlay.Location = new Point(0, 0);
            _loadingOverlay.Name = "_loadingOverlay";
            _loadingOverlay.Size = new Size(1548, 673);
            _loadingOverlay.TabIndex = 2;
            _loadingOverlay.Visible = false;
            // 
            // _canvas
            // 
            _canvas.AllowDrop = true;
            _canvas.BackColor = Color.FromArgb(13, 13, 13);
            _canvas.Controls.Add(_loadingOverlay);
            _canvas.Controls.Add(_btnChangeImage);
            _canvas.Dock = DockStyle.Fill;
            _canvas.Location = new Point(24, 24);
            _canvas.Name = "_canvas";
            _canvas.Size = new Size(1548, 673);
            _canvas.TabIndex = 0;
            // 
            // _btnChangeImage
            // 
            _btnChangeImage.Anchor = AnchorStyles.Top | AnchorStyles.Right;
            _btnChangeImage.BackColor = Color.FromArgb(50, 50, 80);
            _btnChangeImage.FlatStyle = FlatStyle.Flat;
            _btnChangeImage.Font = new Font("Segoe UI", 8.5F);
            _btnChangeImage.Location = new Point(2208, 6);
            _btnChangeImage.Name = "_btnChangeImage";
            _btnChangeImage.Size = new Size(120, 30);
            _btnChangeImage.TabIndex = 1;
            _btnChangeImage.Text = "Change Image";
            _btnChangeImage.UseVisualStyleBackColor = false;
            // 
            // _centerPanel
            // 
            _centerPanel.BackColor = Color.FromArgb(13, 13, 13);
            _centerPanel.Controls.Add(_canvas);
            _centerPanel.Dock = DockStyle.Fill;
            _centerPanel.Location = new Point(204, 60);
            _centerPanel.Name = "_centerPanel";
            _centerPanel.Padding = new Padding(24);
            _centerPanel.Size = new Size(1596, 721);
            _centerPanel.TabIndex = 0;
            _centerPanel.Paint += PaintCenterBorder;
            // 
            // MainForm
            // 
            AutoScaleDimensions = new SizeF(7F, 15F);
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(2060, 951);
            Controls.Add(_centerPanel);
            Controls.Add(_leftPanel);
            Controls.Add(_rightPanel);
            Controls.Add(_bottomContainer);
            Controls.Add(_topBar);
            MinimumSize = new Size(1096, 672);
            Name = "MainForm";
            Text = "VisionEdit CV";
            _leftFlow.ResumeLayout(false);
            _leftPanel.ResumeLayout(false);
            _rightPanel.ResumeLayout(false);
            _effectSubPanel.ResumeLayout(false);
            _panelColorGrading.ResumeLayout(false);
            _cgFlow.ResumeLayout(false);
            _grpCgTint.ResumeLayout(false);
            _grpCgTint.PerformLayout();
            _grpCgTs.ResumeLayout(false);
            _grpCgTs.PerformLayout();
            _grpCgBr.ResumeLayout(false);
            _grpCgBr.PerformLayout();
            _grpCgCo.ResumeLayout(false);
            _grpCgCo.PerformLayout();
            _grpCgTb.ResumeLayout(false);
            _grpCgTb.PerformLayout();
            _panelArtistic.ResumeLayout(false);
            _artFlow.ResumeLayout(false);
            _grpArtInt.ResumeLayout(false);
            _grpArtInt.PerformLayout();
            _panelSticker.ResumeLayout(false);
            _stFlow.ResumeLayout(false);
            _grpStSc.ResumeLayout(false);
            _grpStSc.PerformLayout();
            _grpStRot.ResumeLayout(false);
            _grpStRot.PerformLayout();
            _grpStBc.ResumeLayout(false);
            _grpStBc.PerformLayout();
            _grpStBt.ResumeLayout(false);
            _grpStBt.PerformLayout();
            _grpStSh.ResumeLayout(false);
            _grpStSh.PerformLayout();
            _grpStBg.ResumeLayout(false);
            _grpStBg.PerformLayout();
            _panelPixelBlur.ResumeLayout(false);
            _pbFlow.ResumeLayout(false);
            _grpPbMode.ResumeLayout(false);
            _grpPbMode.PerformLayout();
            _grpPbInt.ResumeLayout(false);
            _grpPbInt.PerformLayout();
            _grpPbTarget.ResumeLayout(false);
            _grpPbTarget.PerformLayout();
            _panelPortrait.ResumeLayout(false);
            _ptFlow.ResumeLayout(false);
            _grpPtBlur.ResumeLayout(false);
            _grpPtBlur.PerformLayout();
            _grpPtFeather.ResumeLayout(false);
            _grpPtFeather.PerformLayout();
            _panelGrayscale.ResumeLayout(false);
            _gsFlow.ResumeLayout(false);
            _grpGsTb.ResumeLayout(false);
            _grpGsTb.PerformLayout();
            _serverPanel.ResumeLayout(false);
            _serverPanel.PerformLayout();
            _topBar.ResumeLayout(false);
            _bottomContainer.ResumeLayout(false);
            _loadingOverlay.ResumeLayout(false);
            _canvas.ResumeLayout(false);
            _centerPanel.ResumeLayout(false);
            ResumeLayout(false);
        }

        private Panel _leftBottomSpacer;
        private DarkButton _btnStartServer;
        private Label label1;
    }
}
