using System;
using System.Threading.Tasks;
using FeedReader.WebClient.Models;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;
using Newtonsoft.Json;

namespace FeedReader.WebClient
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);

            builder.RootComponents.Add<App>("#app");

            var host = builder.Build();
            var js = host.Services.GetRequiredService<IJSRuntime>();
            try
            {
                // Try to get current user from local cache, if failed,create a new user.
                var json = await js.InvokeAsync<string> ("localStorage.getItem", "current-user");
                App.CurrentUser = JsonConvert.DeserializeObject<User>(json);
            }
            catch (Exception)
            {
                App.CurrentUser = new User();
            }

            // Hook up current user state changed.
            App.CurrentUser.SetServerAddress(builder.Configuration["api_server_address"]);
            App.CurrentUser.OnStateChanged += async (s, e) => await js.InvokeAsync<string>("localStorage.setItem", "current-user", JsonConvert.SerializeObject(App.CurrentUser));
            await builder.Build().RunAsync();
        }
    }
}