using FeedReader.ServerCore.Services;
using FeedReader.Share;
using FeedReader.Share.Protocols;
using Grpc.Core;
using Grpc.Core.Interceptors;
using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Net;
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
                ValidateToken(context);
                return await base.UnaryServerHandler(request, context, continuation);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new RpcException(new Status(StatusCode.Unauthenticated, ex.Message));
            }
        }

        void ValidateToken(ServerCallContext context)
        {
            var token = context.RequestHeaders.Get("authentication")?.Value;
            if (string.IsNullOrEmpty(token))
            {
                throw new UnauthorizedAccessException("token is missing");
            }

            var userId = AuthService.ValidateFeedReaderUserToken(token);
            context.UserState.Add("userId", userId);
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

        public override async Task<GetUserProfileResponse> GetUserProfile(GetUserProfileRequest request, ServerCallContext context)
        {
            var userId = ValidateUserSelfOnly(context, request.UserId);
            var user = await UserService.GetUserProfile(userId);
            if (user == null)
            {
                throw new FeedReaderException(HttpStatusCode.NotFound);
            }

            var response = new GetUserProfileResponse()
            {
                User = user.ToProtocolUser()
            };
            return response;
        }

        public override async Task<GetUserSubscriptionsResponse> GetUserSubscriptions(GetUserSubscriptionsRequest request, ServerCallContext context)
        {
            var userId = ValidateUserSelfOnly(context, request.UserId);
            var subscriptions = await UserService.GetUserSubscriptions(userId);
            var response = new GetUserSubscriptionsResponse();
            response.Feeds.AddRange(subscriptions.Select(f => f.ToProtocolFeedInfo()));
            return response;
        }

        public override async Task<GetUserFavoritesResponse> GetUserFavorites(GetUserFavoritesRequest request, ServerCallContext context)
        {
            var userId = ValidateUserSelfOnly(context, request.UserId);
            var favorites = await UserService.GetFavorites(userId);
            var response = new GetUserFavoritesResponse();
            response.FeedItems.AddRange(favorites.Select(f => f.ToProtocolFeedItem()));
            return response;
        }

        public override async Task<DiscoverFeedsResponse> DiscoverFeeds(DiscoverFeedsRequest request, ServerCallContext context)
        {
            var feeds = await FeedService.DiscoverFeedsAsync(request.Query);
            var response = new DiscoverFeedsResponse();
            response.Feeds.AddRange(feeds.Select(f => f.ToProtocolFeedInfo()).ToList());
            return response;
        }

        public override async Task<FavoriteFeedItemResponse> FavoriteFeedItem(FavoriteFeedItemRequest request, ServerCallContext context)
        {
            if (request.Favorite)
            {
                await UserService.FavoriteFeedItemAsync(GetUserId(context), Guid.Parse(request.FeedItemId));
            }
            else
            {
                await UserService.UnFavoriteFeedItemAsync(GetUserId(context), Guid.Parse(request.FeedItemId));
            }
            return new FavoriteFeedItemResponse();
        }

        public override async Task<SubscribeFeedResponse> SubscribeFeed(SubscribeFeedRequest request, ServerCallContext context)
        {
            if (request.Subscribe)
            {
                await UserService.SubscribeFeed(GetUserId(context), Guid.Parse(request.FeedId));
            }
            else
            {
                await UserService.UnsubscribeFeed(GetUserId(context), Guid.Parse(request.FeedId));
            }
            return new SubscribeFeedResponse();
        }

        public override async Task<UpdateFeedSubscriptionResponse> UpdateFeedSubscription(UpdateFeedSubscriptionRequest request, ServerCallContext context)
        {
            var userId = GetUserId(context);
            var feedId = Guid.Parse(request.FeedId);
            var lastedReadedTime = request.LastReadedTime.ToDateTime();
            await UserService.UpdateFeedSubscription(userId, feedId, lastedReadedTime);
            return new UpdateFeedSubscriptionResponse();
        }

        public override async Task<GetFeedInfoResponse> GetFeedInfo(GetFeedInfoRequest request, ServerCallContext context)
        {
            ServerCore.Models.FeedInfo feedInfo = null;
            switch (request.KeyCase)
            {
                case GetFeedInfoRequest.KeyOneofCase.FeedId:
                    if (string.IsNullOrEmpty(request.FeedId))
                    {
                        throw new ArgumentException("FeedId can't be empty", "FeedId");
                    }
                    feedInfo = await FeedService.GetFeedInfoById(Guid.Parse(request.FeedId));
                    break;

                case GetFeedInfoRequest.KeyOneofCase.SubscriptionName:
                    if (string.IsNullOrEmpty(request.SubscriptionName))
                    {
                        throw new ArgumentException("SubscriptionName can't be empty", "FeedId");
                    }
                    feedInfo = await FeedService.GetFeedInfoBySubscriptionName(request.SubscriptionName);
                    break;

                default:
                    throw new ArgumentException();
            }

            if (feedInfo == null)
            {
                return new GetFeedInfoResponse();
            }
            else
            {
                return new GetFeedInfoResponse()
                {
                    Feed = feedInfo.ToProtocolFeedInfo()
                };
            }
        }

        public override async Task<GetFeedItemsResponse> GetFeedItems(GetFeedItemsRequest request, ServerCallContext context)
        {
            if (request.StartIndex < 0)
            {
                throw new ArgumentException("'StartIndex' can't be less than 0.");
            }
            if (request.Count < 1 || request.Count > 50)
            {
                throw new ArgumentException("'Count' must be between 1 to 50.");
            }

            List<ServerCore.Models.FeedItem> feedItems;
            switch (request.QueryCase)
            {
                case GetFeedItemsRequest.QueryOneofCase.FeedId:
                    feedItems = await FeedService.GetFeedItemsByIdAsync(Guid.Parse(request.FeedId), request.StartIndex, request.Count);
                    break;

                case GetFeedItemsRequest.QueryOneofCase.Category:
                    feedItems = await FeedService.GetFeedItemsByCategoryAsync(request.Category, request.StartIndex, request.Count);
                    break;

                default:
                    throw new ArgumentException("Unsupported query", "Query");
            }
            var response = new GetFeedItemsResponse();
            response.FeedItems.AddRange(feedItems.Select(f => f.ToProtocolFeedItem()));
            return response;
        }

        Guid GetUserId(ServerCallContext context)
        {
            return (Guid)context.UserState["userId"];
        }

        /// <summary>
        /// Make sure the request is made by userself.
        /// </summary>
        /// <param name="context"></param>
        /// <param name="resourceUserId"></param>
        /// <returns>The id of userself.</returns>
        private Guid ValidateUserSelfOnly(ServerCallContext context, string resourceUserId)
        {
            var userselfId = GetUserId(context);
            if (!string.IsNullOrEmpty(resourceUserId) && Guid.Parse(resourceUserId) != userselfId)
            {
                throw new FeedReaderException(HttpStatusCode.Forbidden);
            }
            return userselfId;
        }
    }
}