
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Configuration;
using FeedReader.WebServer.Services;
using FeedReader.ServerCore;

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
            Mappers.StaticServer = configuration["StaticServer"];
        }

        public void ConfigureServices(IServiceCollection services)
        {
            services.AddFeedReaderServerCoreServices(Configuration);

            services.AddSingleton<StaticFileService>();

            services.AddControllers();

            services.AddApiVersioning();

            services.AddResponseCaching();

            var grpc = services.AddGrpc();
            grpc.AddServiceOptions<WebServerApi>(configure => configure.Interceptors.Add<WebServerApiInterceptor>());

            services.AddCors(o => o.AddPolicy("AllowAll", builder =>
            {
                builder.AllowAnyOrigin()
                       .AllowAnyMethod()
                       .AllowAnyHeader()
                       .WithExposedHeaders("Grpc-Status", "Grpc-Message", "Grpc-Encoding", "Grpc-Accept-Encoding");
            }));
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
                else if (IsStartsWithSegments(context, "/imgproxy"))
                {
                    return app.ApplicationServices.GetService<StaticFileService>().ProcessImageProxyRequestAsync(context);
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

            app.UseRouting();

            app.UseGrpcWeb();

            app.UseCors("AllowAll");

            app.UseResponseCaching();

            app.UseEndpoints(endpoints =>
            {
                endpoints.MapControllers();
                endpoints.MapGrpcService<AnonymousService>().EnableGrpcWeb().RequireCors("AllowAll");
                endpoints.MapGrpcService<WebServerApi>().EnableGrpcWeb().RequireCors("AllowAll");
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