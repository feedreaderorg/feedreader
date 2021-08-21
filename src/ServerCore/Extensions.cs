using FeedReader.ServerCore.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System.Net.Http;

namespace FeedReader.ServerCore
{
    public static class Extensions
    {
        public static IServiceCollection AddFeedReaderServerCoreServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddLogging();
            services.AddTransient<HttpClient>();
            services.AddDbContextFactory<DbContext>(options => options.UseNpgsql(configuration.GetConnectionString("DbConnectionString")));
            services.AddSingleton<AuthService>();
            services.AddSingleton<FeedService>();
            services.AddSingleton<UserService>();
            services.AddSingleton<FileService>();
            return services;
        }
    }
}
