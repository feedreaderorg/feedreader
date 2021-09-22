﻿using FeedReader.ServerCore;
using FeedReader.ServerCore.Models;
using FeedReader.ServerCore.Services;
using FeedReader.Share.Protocols;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Core.Interceptors;
using System;
using System.Collections.Generic;
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
                ValidateToken(context);
                return await base.UnaryServerHandler(request, context, continuation);
            }
            catch (UnauthorizedAccessException ex)
            {
                throw new RpcException(new Status(StatusCode.Unauthenticated, ex.Message));
            }
        }

        public override async Task ServerStreamingServerHandler<TRequest, TResponse>(TRequest request, IServerStreamWriter<TResponse> responseStream, ServerCallContext context, ServerStreamingServerMethod<TRequest, TResponse> continuation)
        {
            try
            {
                ValidateToken(context);
                await base.ServerStreamingServerHandler(request, responseStream, context, continuation);
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

        public override async Task SubscribeEvents(Empty request, IServerStreamWriter<Event> responseStream, ServerCallContext context)
        {
            var userId = GetUserId(context);
            var sessionId = context.GetHashCode();
            UserService.SubscribeUserEvent(userId, sessionId, async user =>
            {
                try
                {
                    await responseStream.WriteAsync(new Event()
                    {
                        User = user.ToProtocolUser()
                    });
                }
                catch
                {
                }
            });

            await Task.Delay(60 * 1000);
            UserService.UnsubscribeUserEvent(userId, sessionId);
        }

        public override async Task<GetFavoritesResponse> GetFavorites(GetFavoritesRequest request, ServerCallContext context)
        {
            var favorites = await UserService.GetFavoritesAsync(GetUserId(context), request.Page);
            var response = new GetFavoritesResponse();
            response.FeedItems.AddRange(favorites.Select(f => f.ToProtocolFeedItem()));
            return response;
        }

        public override async Task<GetFeedInfoResponse> GetFeedInfo(GetFeedInfoRequest request, ServerCallContext context)
        {
            if (string.IsNullOrEmpty(request.FeedId))
            {
                throw new ArgumentException("FeedId can't be null", "FeedId");
            }

            var feedInfo = await FeedService.GetFeedInfoAsync(Guid.Parse(request.FeedId));
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
            List<ServerCore.Models.FeedItem> feedItems;
            switch (request.QueryCase)
            {
                case GetFeedItemsRequest.QueryOneofCase.FeedId:
                    feedItems = await FeedService.GetFeedItemsByIdAsync(Guid.Parse(request.FeedId), request.Page);
                    break;

                case GetFeedItemsRequest.QueryOneofCase.Category:
                    feedItems = await FeedService.GetFeedItemsByCategoryAsync(request.Category, request.Page);
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
    }
}