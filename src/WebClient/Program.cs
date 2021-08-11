using System.Threading.Tasks;
using FeedReader.WebClient.Models;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;

namespace FeedReader.WebClient
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);

            builder.RootComponents.Add<App>("#app");

            App.CurrentUser = new User();

            await builder.Build().RunAsync();
        }
    }
}