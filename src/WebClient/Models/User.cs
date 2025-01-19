using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
using Google.Protobuf.WellKnownTypes;
using Grpc.Core;
using Grpc.Core.Interceptors;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using Microsoft.AspNetCore.WebUtilities;
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

        public IEnumerable<Feed> SubscribedFeeds => _allFeeds.Where(f => f.Subscribed == true).ToArray();

        public WebServerApiClient WebServerApi { get; set; }

        public AnonymousServiceClient AnonymousService { get; set; }

        private IJSRuntime JS { get; set; }
        private List<Feed> _allFeeds = new List<Feed>();
        private HttpClient _api;
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
            _api = new HttpClient() { BaseAddress = new Uri(serverAddress) };

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

        public async Task<List<Feed>> SearchFeedAsync(string query, int count = 50, CancellationToken cancelToken = default)
        {
            var feeds = await _api.GetFromJsonAsync<IEnumerable<Share.Protocols.FeedInfo>>(QueryHelpers.AddQueryString("api/v1.0/feed/discover", 
                new Dictionary<string, string> { { "query", query }, { "start", "0" }, { "count", $"{count}" } }
            ));
            return feeds.Select(s => s.ToModelFeed()).ToList();
        }

        public async Task<List<FeedItem>> GetRecommedFeedItemsAsync(int start = 0, int count = 50, CancellationToken cancelToken = default)
        {

            Console.WriteLine(QueryHelpers.AddQueryString("v1.0/feed/recommends",
                new Dictionary<string, string> { { "start", $"{start}" }, { "count", $"{count}" } }
            ));
            var feedItems = await _api.GetFromJsonAsync<IEnumerable<Share.Protocols.FeedItem>>(QueryHelpers.AddQueryString("api/v1.0/feed/recommends",
                new Dictionary<string, string> { { "start", $"{start}" }, { "count", $"{count}" } }
            ));
            return feedItems.Select(s => s.ToModelFeedItem()).ToList();
        }

        public async Task SubscribeFeedAsync(Feed feed)
        {
            var item = _allFeeds.Find(f => f.Id == feed.Id);
            if (item?.Subscribed == true)
            {
                return;
            }

            await WebServerApi.SubscribeFeedAsync(new Share.Protocols.SubscribeFeedRequest()
            {
                FeedId = feed.Id.ToString(),
                Subscribe = true,
            });

			if (item == null)
			{
				_allFeeds.Add(item = feed);
			}
			item.Subscribed = true;
			OnStateChanged?.Invoke(this, null);
        }

        public async Task UnsubscribeFeedAsync(Feed feed)
        {
            feed = _allFeeds.Find(f => f.Id == feed.Id);
            if (feed == null || feed.Subscribed == false)
            {
                return;
            }

            await WebServerApi.SubscribeFeedAsync(new Share.Protocols.SubscribeFeedRequest()
            {
                FeedId = feed.Id.ToString(),
                Subscribe = false,
            });
            if (feed.LastReadedTime == DateTime.MinValue)
            {
                _allFeeds.Remove(feed);
            }
            else
            {
                feed.Subscribed = false;
            }
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
                var feed = _allFeeds.FirstOrDefault(f => f.SubscriptionName == feedSubscriptionName);
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

        public async Task MarkAsReaded(Feed feed)
        {
            await WebServerApi.UpdateFeedSubscriptionAsync(new Share.Protocols.UpdateFeedSubscriptionRequest()
            {
                FeedId = feed.Id.ToString(),
                LastReadedTime  = feed.LastReadedTime.ToTimestamp(),
            });

            var item = _allFeeds.FirstOrDefault(f => f.Id == feed.Id);
            if (item == null)
            {
                _allFeeds.Add(feed);
            }
            else if (item != feed)
            {
                item.LastReadedTime = feed.LastReadedTime;
            }
            OnStateChanged?.Invoke(this, null);
        }

        private async Task RefreshSubscriptions()
        {
            var response = await WebServerApi.GetUserSubscriptionsAsync(new Share.Protocols.GetUserSubscriptionsRequest());
            _allFeeds = response.Feeds.Select(f => f.ToModelFeed()).ToList();
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
            _allFeeds.Clear();
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