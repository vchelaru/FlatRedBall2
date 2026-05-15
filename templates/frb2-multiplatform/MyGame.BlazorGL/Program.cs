using System;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xna.Framework;

namespace MyGame.BlazorGL
{
    internal class Program
    {
        private static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);
            builder.RootComponents.Add<App>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");
            builder.Services.AddScoped(sp => new HttpClient()
            {
                BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
            });
            // Index.razor.cs resolves Func<Game> from DI on the first tick to construct Game1.
            builder.Services.AddSingleton<Func<Game>>(_ => () => new MyGame.Game1());
            await builder.Build().RunAsync();
        }
    }
}
