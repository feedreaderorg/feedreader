using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using Microsoft.JSInterop;
using static FeedReader.Share.Protocols.AnonymousService;
using static FeedReader.Share.Protocols.WebServerApi;

namespace FeedReader.WebClient.Models
{
	public enum UserRole
    {
        Guest,
        Normal
    };

    public class User
    {
        string ServerAddress { get; set; }

        public List<FeedItem> Favorites { get; set; } = new List<FeedItem>();

        public string Id { get; set; }

        public string Token { get; set; }

        public UserRole Role { get; set; }

        public event EventHandler OnStateChanged;

        public List<Feed> SubscribedFeeds { get; set; } = new List<Feed>();

        public WebServerApiClient WebServerApi { get; set; }

        public AnonymousServiceClient AnonymousService { get; set; }

        private IJSRuntime JS { get; set; }

        public User(IJSRuntime js)
        {
            JS = js;
            Reset();
        }

        public async Task Init(string serverAddress)
        {
            ServerAddress = serverAddress;
            var httpHandler = new HttpClientHandler();
            var grpcHandler = new GrpcWebHandler(GrpcWebMode.GrpcWebText, httpHandler);
            var grpcChannel = GrpcChannel.ForAddress(serverAddress, new GrpcChannelOptions { HttpHandler = grpcHandler });
            AnonymousService = new AnonymousServiceClient(grpcChannel);

            // Try to load token
            Token = await Load("feedreader-access-token");
            if (!string.IsNullOrEmpty(Token))
            {
                try
                {
                    await LoginAsync(Token);
                }
                catch
                {
                    Reset();
                }
            }
        }

        public async Task AddOrRemoveFavorite(FeedItem feedItem, bool addToFavorite)
        {
            await WebServerApi.FavoriteFeedItemAsync(new Share.Protocols.FavoriteFeedItemRequest()
            {
                FeedItemId = feedItem.Id,
                Favorite = addToFavorite
            });

            var oldItem = Favorites.Find(f => f.Id == feedItem.Id);
            if (addToFavorite && oldItem == null)
            {
                Favorites.Add(feedItem);
                Favorites = Favorites.OrderByDescending(f => f.PublishTime).ToList();
            }
            else if (!addToFavorite && oldItem != null)
            {
                Favorites.Remove(oldItem);
            }
        }

        // Return Nonce in the token.
        public async Task<string> LoginAsync(string token)
        {
            var response = await AnonymousService.LoginAsync(new Share.Protocols.LoginRequest()
            {
                Token = token,
            });

            // Save the new token.
            Token = response.Token;
            await Save("feedreader-access-token", Token);
            RefreshWebServerApi();

            // Update user.
            Id = response.UserId;
            Role = UserRole.Normal;
            await RefreshSubscriptions();
            await RefreshFavorites();
            RefreshInBackground();
            OnStateChanged?.Invoke(this, null);

            return response.Nonce;
        }

        public async Task LogoutAsync()
        {
            // TODO: logout from server?
            await Task.Delay(2000);
            await ClearStorage();
            Reset();
            await Task.CompletedTask;
        }

        public async Task<List<Feed>> SearchFeedAsync(string query, CancellationToken cancelToken)
        {
            var response = await WebServerApi.DiscoverFeedsAsync(new Share.Protocols.DiscoverFeedsRequest()
            {
                Query = query ?? string.Empty
            }, headers: null, deadline: null, cancelToken);
            return response.Feeds.Select(s => s.ToModelFeed()).ToList();
        }

        public async Task SubscribeFeedAsync(Feed feed)
        {
            if (SubscribedFeeds.Find(f => f.Id == feed.Id) != null)
            {
                return;
            }

            await WebServerApi.SubscribeFeedAsync(new Share.Protocols.SubscribeFeedRequest()
            {
                FeedId = feed.Id.ToString(),
                Subscribe = true,
            });
            SubscribedFeeds.Add(feed);
            OnStateChanged?.Invoke(this, null);
        }

        public async Task UnsubscribeFeedAsync(Feed feed)
        {
            feed = SubscribedFeeds.Find(f => f.Id == feed.Id);
            if (feed == null)
            {
                return;
            }

            await WebServerApi.SubscribeFeedAsync(new Share.Protocols.SubscribeFeedRequest()
            {
                FeedId = feed.Id.ToString(),
                Subscribe = false,
            });
            SubscribedFeeds.Remove(feed);
            OnStateChanged?.Invoke(this, null);
        }

        public async Task<Feed> GetFeed(string feedSubscriptionName)
        {
            if (string.IsNullOrWhiteSpace(feedSubscriptionName))
            {
                return null;
            }
            else
            {
                var feed = SubscribedFeeds.FirstOrDefault(f => f.SubscriptionName == feedSubscriptionName);
                if (feed == null)
                {
                    try
                    {
                        feed = (await AnonymousService.GetFeedInfoAsync(new Share.Protocols.GetFeedInfoRequest()
                        {
                            SubscriptionName = feedSubscriptionName
                        })).Feed.ToModelFeed();
                    }
                    catch
                    {
                    }
                }
                return feed;
            }
        }

        private async Task RefreshSubscriptions()
        {
            var response = await WebServerApi.GetUserSubscriptionsAsync(new Share.Protocols.GetUserSubscriptionsRequest());
            SubscribedFeeds = response.Feeds.Select(f => f.ToModelFeed()).ToList();
        }

        private async Task RefreshFavorites()
        {
            var response = await WebServerApi.GetUserFavoritesAsync(new Share.Protocols.GetUserFavoritesRequest());
            Favorites = response.FeedItems.Select(f => f.ToModelFeedItem()).ToList();
        }

        void Reset()
        {
            Token = "";
            Role = UserRole.Guest;
            SubscribedFeeds.Clear();
            RefreshWebServerApi();
            OnStateChanged?.Invoke(this, null);
        }

        void RefreshWebServerApi()
        {
            if (string.IsNullOrEmpty(Token) || string.IsNullOrEmpty(ServerAddress))
            {
                WebServerApi = null;
            }
            else
            {
                var httpHandler = new CustomizedHttpClientHandler(Token);
                var grpcHandler = new GrpcWebHandler(GrpcWebMode.GrpcWebText, httpHandler);
                var grpcChannel = GrpcChannel.ForAddress(ServerAddress, new GrpcChannelOptions { HttpHandler = grpcHandler });
                WebServerApi = new WebServerApiClient(grpcChannel.Intercept(new GrpcInterceptor(OnUnauthenticatedException)));
            }
        }

        void OnUnauthenticatedException(string message)
        {
            // TODO: log exception message.
            Reset();
        }

        private async Task<string> Load(string key)
        {
            try
            {
                // Try to get the saved access token from the local cache.
                return await JS.InvokeAsync<string>("localStorage.getItem", key);
            }
            catch
            {
                await Save(key, null);
                return null;
            }
        }

        private async Task Save(string key, string value)
        {
            try
            {
                if (value == null)
                {
                    await JS.InvokeVoidAsync("localStorage.removeItem", key);
                }
                else
                {
                    await JS.InvokeVoidAsync("localStorage.setItem", key, value);
                }
            }
            catch
            {
            }
        }

        private async Task ClearStorage()
        {
            try
            {
                await JS.InvokeVoidAsync("localStorage.clear");
            }
            catch
            {
            }
        }

        private async void RefreshInBackground()
		{
            while (true)
			{
                if (SubscribedFeeds != null)
                {
                    try
                    {
                        foreach (var feed in SubscribedFeeds)
                        {
                            _ = feed.RefreshAsync();
                        }
                    }
                    catch
                    {
                    }
                }

                // Wait for 10 mins to refresh.
                await Task.Delay(600 * 1000);
			}
		}

        #region CustomizedHttpClientHandler & GrpcInterceptor
        class CustomizedHttpClientHandler : HttpClientHandler
        {
            string Token { get; set; }

            public CustomizedHttpClientHandler(string token)
            {
                Token = token;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                request.Headers.Add("authentication", Token);
                return base.SendAsync(request, cancellationToken);
            }
        }

        class GrpcInterceptor : Interceptor
        {
            Action<string> OnUnauthenticatedException { get; set; }

            public GrpcInterceptor(Action<string> onUnauthenticatedException)
            {
                OnUnauthenticatedException = onUnauthenticatedException;
            }

            public override AsyncUnaryCall<TResponse> AsyncUnaryCall<TRequest, TResponse>(TRequest request, ClientInterceptorContext<TRequest, TResponse> context, AsyncUnaryCallContinuation<TRequest, TResponse> continuation)
            {
                var call = continuation(request, context);
                return new AsyncUnaryCall<TResponse>(ProcessResponse(call.ResponseAsync, OnUnauthenticatedException), call.ResponseHeadersAsync, call.GetStatus, call.GetTrailers, call.Dispose);
            }

            public static async Task<TResponse> ProcessResponse<TResponse>(Task<TResponse> task, Action<string> onUnauthenticatedException)
            {
                try
                {
                    return await task;
                }
                catch (RpcException ex)
                {
                    if (ex.StatusCode == StatusCode.Unauthenticated)
                    {
                        onUnauthenticatedException(ex.Message);
                        return default(TResponse);
                    }
                    else
                    {
                        throw;
                    }
                }
            }
        }
        #endregion
    }
}