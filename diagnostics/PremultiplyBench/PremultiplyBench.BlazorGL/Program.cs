using System.Runtime.InteropServices.JavaScript;
using FlatRedBall2.BlazorGL;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Xna.Framework;
using PremultiplyBench;

#if WASM_AOT
BenchGame.PlatformName = "BlazorGL-AOT";
#else
BenchGame.PlatformName = "BlazorGL-Interp";
#endif
BenchGame.ExitWhenDone = false;
BenchGame.ExtraInfo = () =>
{
    try { return "Browser: " + JSHost.GlobalThis.GetPropertyAsJSObject("navigator")!.GetPropertyAsString("userAgent"); }
    catch (Exception e) { return "Browser: n/a " + e.GetType().Name; }
};
// No filesystem: emit every line to the console, which tools/measure-web.ps1 captures.
BenchGame.WriteResult = (name, text) =>
{
    foreach (var line in text.Split('\n')) Console.WriteLine($"[pmb] {name}: {line}");
    if (name.EndsWith(".done")) Console.WriteLine("[pmb-done]");
};

var builder = WebAssemblyHostBuilder.CreateDefault(args);
builder.RootComponents.Add<App>("#app");
builder.Services.AddSingleton<Func<Game>>(_ => () => new BenchGame());
await builder.Build().RunAsync();
