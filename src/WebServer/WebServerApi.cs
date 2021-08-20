using FeedReader.Share.Protocols;
using FeedReader.WebServer.Services;
using Grpc.Core;
using Grpc.Core.Interceptors;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace FeedReader.WebServer
{
    public class WebServerApiInterceptor : Interceptor
    {
        AuthService AuthService { get; set; }

        public WebServerApiInterceptor(AuthService authService)
        {
            AuthService = authService;
        }

        public override async Task<TResponse> UnaryServerHandler<TRequest, TResponse>(TRequest request, ServerCallContext context, UnaryServerMethod<TRequest, TResponse> continuation)
        {
            try
            {
                var token = context.RequestHeaders.Get("authentication")?.Value;
                if (string.IsNullOrEmpty(token))
                {
                    throw new UnauthorizedAccessException("token is missing");
                }

                var userId = AuthService.ValidateFeedReaderUserToken(token);
                context.UserState.Add("userId", userId);
                return await continuation(request, context);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new RpcException(new Status(StatusCode.Unauthenticated, ex.Message));
            }
        }
    }

    public class WebServerApi : Share.Protocols.WebServerApi.WebServerApiBase
    {
        FeedService FeedService { get; set; }
        UserService UserService { get; set; }

        public WebServerApi(FeedService feedService, UserService userService)
        {
            FeedService = feedService;
            UserService = userService;
        }

        public override async Task<DiscoverFeedsResponse> DiscoverFeeds(DiscoverFeedsRequest request, ServerCallContext context)
        {
            var feeds = await FeedService.DiscoverFeedsAsync(request.Query);
            var response = new DiscoverFeedsResponse();
            response.Feeds.AddRange(feeds.Select(f => f.ToProtocolFeedInfo()).ToList());
            return response;
        }

        public override async Task<SubscribeFeedResponse> SubscribeFeed(SubscribeFeedRequest request, ServerCallContext context)
        {
            if (request.Subscribe)
            {
                await UserService.SubscribeFeed((Guid)context.UserState["userId"], Guid.Parse(request.FeedId));
            }
            else
            {
                await UserService.UnsubscribeFeed((Guid)context.UserState["userId"], Guid.Parse(request.FeedId));
            }
            return new SubscribeFeedResponse();
        }
    }
}