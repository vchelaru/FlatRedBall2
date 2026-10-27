using System.Runtime.InteropServices;
using PremultiplyBench;

BenchGame.PlatformName = "DesktopGL";
BenchGame.ExtraInfo = () =>
{
    string cpu = Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString", "?") as string ?? "?";
    return $"CPU: {cpu.Trim()}\nGL_RENDERER: {Gl(0x1F01)}\nGL_VERSION: {Gl(0x1F02)}\nGL_VENDOR: {Gl(0x1F00)}";
};
if (args.Length > 0) BenchGame.WriteResult = (name, text) => { Directory.CreateDirectory(args[0]); File.WriteAllText(Path.Combine(args[0], name), text); };
using var game = new BenchGame();
game.Run();

static string Gl(uint name) => Marshal.PtrToStringAnsi(NativeGl.glGetString(name)) ?? "?";

static class NativeGl
{
    [DllImport("opengl32.dll")] public static extern IntPtr glGetString(uint name);
}
