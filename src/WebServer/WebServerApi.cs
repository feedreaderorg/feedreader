﻿using FeedReader.ServerCore.Services;
using FeedReader.Share;
using FeedReader.Share.Protocols;
using Grpc.Core;
using Grpc.Core.Interceptors;
using System;
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
        private Validator Validator { get; } = new Validator();
        private FeedService FeedService { get; set; }
        private UserService UserService { get; set; }

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