using System.Runtime.InteropServices;
using Android.App;
using Android.Content.PM;
using Android.OS;
using Android.Views;

namespace PremultiplyBench;

[Activity(Label = "PremultiplyBench", MainLauncher = true, LaunchMode = LaunchMode.SingleInstance,
    ScreenOrientation = ScreenOrientation.FullSensor,
    ConfigurationChanges = ConfigChanges.Orientation | ConfigChanges.Keyboard | ConfigChanges.KeyboardHidden | ConfigChanges.ScreenSize | ConfigChanges.ScreenLayout | ConfigChanges.UiMode | ConfigChanges.SmallestScreenSize)]
public class BenchActivity : Microsoft.Xna.Framework.AndroidGameActivity
{
    protected override void OnCreate(Bundle? bundle)
    {
        base.OnCreate(bundle);
        // adb-pullable without root: /sdcard/Android/data/com.flatredball.premultiplybench/files/
        string dir = GetExternalFilesDir(null)!.AbsolutePath;
        foreach (var f in Directory.GetFiles(dir, "results-*")) File.Delete(f);
        BenchGame.PlatformName = "Android";
        BenchGame.ExitWhenDone = false;
        BenchGame.WriteResult = (name, text) => File.WriteAllText(Path.Combine(dir, name), text);
        BenchGame.ExtraInfo = () =>
            $"Device: {Build.Manufacturer} {Build.Model} (Android {Build.VERSION.Release}, API {(int)Build.VERSION.SdkInt})\n" +
            $"SoC/hardware: {(OperatingSystem.IsAndroidVersionAtLeast(31) ? Build.SocManufacturer + " " + Build.SocModel : "")} {Build.Hardware}\n" +
            $"GL_RENDERER: {Gl(0x1F01)}\nGL_VERSION: {Gl(0x1F02)}\nGL_VENDOR: {Gl(0x1F00)}";
        var game = new BenchGame();
        SetContentView((View)game.Services.GetService(typeof(View))!);
        game.Run();
    }

    static string Gl(uint name) => Marshal.PtrToStringAnsi(glGetString(name)) ?? "?";

    [DllImport("libGLESv2.so")] static extern IntPtr glGetString(uint name);
}
