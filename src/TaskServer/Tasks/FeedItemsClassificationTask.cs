using FeedReader.ServerCore.Services;
using Microsoft.Extensions.Logging;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace FeedReader.TaskServer.Tasks
{
    public class FeedItemsClassificationTask : TaskBase
    {
        FeedService FeedService { get; set; }

        public FeedItemsClassificationTask(FeedService feedService, ILogger<RefreshFeedTask> logger)
            : base("FeedItemsClassificationTask", TimeSpan.FromMinutes(1), logger)
        {
            FeedService = feedService;
        }

        protected override Task DoTaskOnceAsync(CancellationToken cancellationToken)
        {
            return FeedService.ClassifyFeedItemsAsync(cancellationToken);
        }
    }
}
