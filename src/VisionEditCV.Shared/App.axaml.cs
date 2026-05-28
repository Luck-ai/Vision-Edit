using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using VisionEditCV.Shared.ViewModels;
using VisionEditCV.Shared.Views;

namespace VisionEditCV.Shared;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        var shell = new AppShellViewModel();

        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.MainWindow = new Avalonia.Controls.Window
            {
                Title = "VisionEditCV",
                Width = 1440,
                Height = 900,
                Content = new MainShellView { DataContext = shell }
            };
        }
        else if (ApplicationLifetime is ISingleViewApplicationLifetime singleView)
        {
            singleView.MainView = new MobileShellView { DataContext = shell };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
