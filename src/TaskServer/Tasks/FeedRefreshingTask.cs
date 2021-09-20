using FeedReader.ServerCore.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeedReader.TaskServer.Tasks
{
    public class FeedRefreshingTask : TaskBase
    {
        FeedService FeedService { get; set; }
        
        public FeedRefreshingTask(FeedService feedService, ILogger<FeedRefreshingTask> logger)
            : base("FeedRefreshingTask", TimeSpan.FromMinutes(5), logger)
        {
            FeedService = feedService;
        }

        protected override Task DoTaskOnceAsync(CancellationToken cancellationToken)
        {
            return FeedService.RefreshFeedsAsync(cancellationToken);
        }
    }
}
