using FeedReader.ServerCore;
using FeedReader.TaskServer.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FeedReader.TaskServer
{
    public class Program
    {
        public static void Main(string[] args)
        {
            CreateHostBuilder(args).Build().Run();
        }

        public static IHostBuilder CreateHostBuilder(string[] args)
        {
            return Host.CreateDefaultBuilder(args).ConfigureServices((hostContext, services) =>
            {
                services.AddFeedReaderServerCoreServices(hostContext.Configuration);
                services.AddHostedService<RefreshFeedTask>();
                services.AddHostedService<FeedItemsClassificationTask>();
            });
        }
    }
}