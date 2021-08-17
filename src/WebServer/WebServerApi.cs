using FeedReader.Share.Protocols;
using FeedReader.WebServer.Services;
using Grpc.Core;
using Microsoft.AspNetCore.Components;
using System.Linq;
using System.Threading.Tasks;

namespace FeedReader.WebServer
{
    public class WebServerApi : Share.Protocols.WebServerApi.WebServerApiBase
    {
        FeedService FeedService { get; set; }

        public WebServerApi(FeedService feedService)
        {
            FeedService = feedService;
        }

        public override async Task<DiscoverFeedsResponse> DiscoverFeeds(DiscoverFeedsRequest request, ServerCallContext context)
        {
            var feeds = await FeedService.DiscoverFeedsAsync(request.Query);
            var response = new DiscoverFeedsResponse();
            response.Feeds.AddRange(feeds.Select(f => f.ToProtocolFeedInfo()).ToList());
            return response;
        }
    }
}