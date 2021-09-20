using FeedReader.ServerCore.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeedReader.TaskServer.Tasks
{
    public class FeedItemStatisticsRefreshingTask : TaskBase
    {
        FeedService FeedService { get; set; }

        public FeedItemStatisticsRefreshingTask(FeedService feedService, ILogger<FeedItemStatisticsRefreshingTask> logger)
            : base("FeedItemStatisticsRefreshingTask", TimeSpan.FromHours(1), logger)
        {
            FeedService = feedService;
        }

        protected override Task DoTaskOnceAsync(CancellationToken cancellationToken)
        {
            return FeedService.RefreshFeedItemStatisticsAsync(cancellationToken);
        }
    }
}
