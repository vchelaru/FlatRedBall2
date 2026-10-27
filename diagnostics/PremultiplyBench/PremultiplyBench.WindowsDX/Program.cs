using PremultiplyBench;

BenchGame.PlatformName = "WindowsDX11";
BenchGame.ExtraInfo = () =>
{
    string cpu = Microsoft.Win32.Registry.GetValue(@"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0", "ProcessorNameString", "?") as string ?? "?";
    return $"CPU: {cpu.Trim()}";
};
if (args.Length > 0) BenchGame.WriteResult = (name, text) => { Directory.CreateDirectory(args[0]); File.WriteAllText(Path.Combine(args[0], name), text); };
using var game = new BenchGame();
game.Run();
