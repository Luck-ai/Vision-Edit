using Android.App;
using Android.Content;
using Android.OS;

namespace VisionEditCV.Android;

[Activity(
    Label = "VisionEditCV",
    Theme = "@style/MyTheme.Splash",
    Icon = "@android:drawable/sym_def_app_icon",
    MainLauncher = true,
    NoHistory = true)]
public class SplashActivity : Activity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
    }

    protected override void OnResume()
    {
        base.OnResume();
        var intent = new Intent(this, typeof(MainActivity));
        StartActivity(intent);
    }
}
