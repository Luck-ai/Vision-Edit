using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using VisionEditCV.Shared.ViewModels;

namespace VisionEditCV.Shared.Views;

public partial class MobileView : UserControl
{
    public MobileView()
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
        if (this.FindControl<Grid>("RootGrid") is { } root)
        {
            root.Margin = new Thickness(safeArea.Left, safeArea.Top, safeArea.Right, safeArea.Bottom);
        }
    }

    private void OnSheetScrimTapped(object? sender, TappedEventArgs e)
    {
        if (DataContext is MainWindowViewModel vm) vm.MobileSheetTab = "";
    }
}
