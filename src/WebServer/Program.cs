  
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Hosting;

namespace FeedReader.WebServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args)
                .ConfigureWebHostDefaults(builder =>
                {
                    builder.UseStartup<Startup>();
                });
        }
    }

    public class Startup
    {
        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseStaticFiles();

            app.UseBlazorFrameworkFiles();

            app.UseRouting();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapFallbackToFile("/index.html");
            });
        }
    }
}