using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using System.Diagnostics;
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
        var sinceOnCreate = Stopwatch.StartNew();
        Timing.Log($"OnCreate, {SystemClock.ElapsedRealtime() - Android.OS.Process.StartElapsedRealtime} ms after process start");
        base.OnCreate(savedInstanceState);
        var game = new Game1(sinceOnCreate);
        SetContentView((View)game.Services.GetService(typeof(View))!);
        game.Run();
    }
}
