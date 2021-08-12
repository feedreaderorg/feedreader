  
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using FeedReader.WebServer.Services;

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
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSingleton<AuthService>();
            services.AddGrpc();
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.UseStaticFiles(new StaticFileOptions()
            {
                OnPrepareResponse = ctx =>
                {
                    if (ctx.File.Name.EndsWith(".css")) {
                        ctx.Context.Response.Headers["Cache-Control"] = "no-cache";
                    }
                }
            });

            app.UseBlazorFrameworkFiles();

            app.UseRouting();

            app.UseGrpcWeb();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapGrpcService<AuthServerApi>().EnableGrpcWeb();

                endpoints.MapFallbackToFile("/index.html");
            });
        }
    }
}