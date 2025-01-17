using System.Threading.Tasks;
using FeedReader.WebClient.Models;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace FeedReader.WebClient
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);

            builder.RootComponents.Add<App>("#app");

            var host = builder.Build();
            App.CurrentUser = new User(host.Services.GetRequiredService<IJSRuntime>());
            App.DefaultSiteIcon = host.Services.GetRequiredService<NavigationManager>().ToAbsoluteUri("/img/default_site_icon.png").ToString();
            await App.CurrentUser.Init(builder.Configuration["api_server_address"]);
            await host.RunAsync();
        }
    }
}