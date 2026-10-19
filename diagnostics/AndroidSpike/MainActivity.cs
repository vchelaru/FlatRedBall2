using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Microsoft.Xna.Framework;

namespace AndroidSpike;

[Activity(
    Label = "AndroidSpike",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleInstance,
    // Without this KNI throws "NaturalOrientation detection failed" in AndroidGameActivity.OnCreate.
    ScreenOrientation = ScreenOrientation.FullSensor,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.ScreenSize)]
public class MainActivity : AndroidGameActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        base.OnCreate(savedInstanceState);
        var game = new Game1();
        SetContentView((View)game.Services.GetService(typeof(View))!);
        game.Run();
    }
}
