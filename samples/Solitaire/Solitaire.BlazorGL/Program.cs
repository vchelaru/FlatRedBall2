using System;
using System.Net.Http;
using System.Threading.Tasks;
using FlatRedBall2.BlazorGL;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Xna.Framework;

namespace Solitaire.BlazorGL
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
            builder.Services.AddSingleton<Func<Game>>(_ => () => new Solitaire.Game1());
            await builder.Build().RunAsync();
        }
    }
}
