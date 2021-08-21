using FeedReader.ServerCore.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeedReader.TaskServer.Tasks
{
    public class RefreshFeedTask : TaskBase
    {
        FeedService FeedService { get; set; }
        
        public RefreshFeedTask(FeedService feedService, ILogger<RefreshFeedTask> logger)
            : base("RefreshFeedTask", TimeSpan.FromMinutes(5), logger)
        {
            FeedService = feedService;
        }

        protected override Task DoTaskOnceAsync(CancellationToken cancellationToken)
        {
            return FeedService.RefreshFeedsAsync(cancellationToken);
        }
    }
}
