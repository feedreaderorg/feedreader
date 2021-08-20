
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;
using FeedReader.WebServer.Services;
using System.Net.Http;

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
        IConfiguration Configuration { get; set; }

        public Startup(IConfiguration configuration)
        {
            Configuration = configuration;
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddDbContextFactory<DbContext>(options => options.UseNpgsql(Configuration.GetConnectionString("DbConnectionString")));

            services.AddSingleton<HttpClient>();
            services.AddSingleton<StaticFileService>();
            services.AddSingleton<AuthService>();
            services.AddSingleton<FeedService>();
            services.AddSingleton<UserService>();

            var grpc = services.AddGrpc();
            grpc.AddServiceOptions<WebServerApi>(configure => configure.Interceptors.Add<WebServerApiInterceptor>());
        }

        public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
        {
            app.Use((context, next) =>
            {
                if (IsStartsWithSegments(context, "/upload"))
                {
                    return app.ApplicationServices.GetService<StaticFileService>().ProcessUploadAsync(context);
                }
                else if (IsStartsWithSegments(context, "/file"))
                {
                    return app.ApplicationServices.GetService<StaticFileService>().ProcessGetFileAsync(context);
                }
                else
                {
                    return next();
                }
            });

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
                endpoints.MapGrpcService<WebServerApi>().EnableGrpcWeb();
                endpoints.MapFallbackToFile("/index.html");
            });
        }

        bool IsStartsWithSegments(HttpContext context, string segments)
        {
            var path = context.Request.Path;
            if (path.StartsWithSegments(segments))
            {
                path = path.Value.Substring(segments.Length);
                if (path == "")
                {
                    path = "/";
                }
                context.Request.Path = new PathString(path);
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}