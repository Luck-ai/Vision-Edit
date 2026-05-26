using Android.App;
using Android.Content.PM;
using Android.OS;
using Avalonia;
using Avalonia.Android;
using VisionEditCV.Desktop;

namespace VisionEditCV.Android;

[Activity(
    Label = "VisionEditCV",
    Theme = "@style/MyTheme.NoActionBar",
    Icon = "@android:drawable/sym_def_app_icon",
    Launcher = false,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode)]
public class MainActivity : AvaloniaMainActivity<App>
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Initialize Emgu.CV native library on Android
        Emgu.CV.CvInvokeAndroid.Init(this);

        base.OnCreate(savedInstanceState);
    }

    protected override AppBuilder CustomizeAppBuilder(AppBuilder builder)
    {
        return base.CustomizeAppBuilder(builder)
            .WithInterFont();
    }
}
