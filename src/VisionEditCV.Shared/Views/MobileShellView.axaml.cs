using Avalonia;
using Avalonia.Controls;

namespace VisionEditCV.Shared.Views;

public partial class MobileShellView : UserControl
{
    public MobileShellView()
    {
        InitializeComponent();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);

        var insets = TopLevel.GetTopLevel(this)?.InsetsManager;
        if (insets is null) return;

        ApplySafeAreaPadding(insets.SafeAreaPadding);
        insets.SafeAreaChanged += (_, args) => ApplySafeAreaPadding(args.SafeAreaPadding);
    }

    private void ApplySafeAreaPadding(Thickness safeArea)
    {
        Margin = new Thickness(safeArea.Left, safeArea.Top, safeArea.Right, safeArea.Bottom);
    }
}
