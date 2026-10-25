using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;
using Microsoft.Xna.Framework;

namespace AposShapesAndroidBench;

[Activity(
    Label = "AposBench",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleInstance,
    // Without this KNI throws "NaturalOrientation detection failed" in AndroidGameActivity.OnCreate.
    ScreenOrientation = ScreenOrientation.FullSensor,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.ScreenSize)]
public class MainActivity : AndroidGameActivity
{
    protected override void OnCreate(Bundle? savedInstanceState)
    {
        // Mode is passed as `am start ... --es mode <batch|split|gl>`.
        var mode = Intent?.GetStringExtra("mode") ?? "batch";
        Bench.Log($"OnCreate mode={mode} apos={Bench.AposVersion}, {SystemClock.ElapsedRealtime() - Android.OS.Process.StartElapsedRealtime} ms after process start");
        base.OnCreate(savedInstanceState);
        var game = new BenchGame(mode);
        SetContentView((View)game.Services.GetService(typeof(View))!);
        game.Run();
    }
}
