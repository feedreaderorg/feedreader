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
using static FeedReader.Share.Protocols.AuthServerApi;
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

        AuthServerApiClient AuthServerApi { get; set; }

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
            AuthServerApi = new AuthServerApiClient(grpcChannel);

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

        public async Task FavoriteFeedItemAsync(FeedItem feedItem)
        {
            await WebServerApi.FavoriteFeedItemAsync(new Share.Protocols.FavoriteFeedItemRequest()
            {
                FeedItemId = feedItem.Id,
                Favorite = true
            });
            ++feedItem.TotalFavorites;
        }

        public async Task LoginAsync(string token)
        {
            var response = await AuthServerApi.LoginAsync(new Share.Protocols.LoginRequest()
            {
                Token = token,
            });

            // Save the new token.
            Token = response.Token;
            await Save("feedreader-access-token", Token);

            // Update user.
            Id = response.UserId;
            Role = UserRole.Normal;
            RefreshWebServerApi();
            OnStateChanged?.Invoke(this, null);
            _ = RefreshUserProfile();
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
                        feed = (await WebServerApi.GetFeedInfoAsync(new Share.Protocols.GetFeedInfoRequest()
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

        public async Task<IEnumerable<FeedItem>> GetFavorites(int startIndex, int count)
        {
            while (Favorites.Count < startIndex + count)
            {
                var response = await App.CurrentUser.WebServerApi.GetFavoritesAsync(new Share.Protocols.GetFavoritesRequest()
                {
                    StartIndex = startIndex,
                    Count = 50
                });
                var newItems = response.FeedItems.Select(i => i.ToModelFeedItem());
                Favorites.AddRange(newItems);
                Favorites = Favorites.DistinctBy(f => f.Id).OrderByDescending(f => f.PublishTime).ToList();
                if (newItems.Count() < 50)
                {
                    break;
                }
            }

            if (startIndex >= Favorites.Count)
            {
                return new List<FeedItem>();
            }
            else
            {
                count = Math.Min(Favorites.Count - startIndex, count);
                return Favorites.GetRange(startIndex, count);
            }
        }

        public async Task RefreshUserProfile()
        {
            UpdateSelf((await WebServerApi.GetUserProfileAsync(new Share.Protocols.GetUserProfileRequest()
            {
                UserId = string.Empty
            })).User);
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

        void UpdateUser(Share.Protocols.User user)
        {
            if (user.Id == Id)
            {
                UpdateSelf(user);
            }
        }

        void UpdateSelf(Share.Protocols.User user)
        {
            SubscribedFeeds = user.SubscribedFeeds.Select(f =>
            {
                var r = f.ToModelFeed();
                r.IsSubscribed = true;
                return r;
            }).ToList();
            OnStateChanged?.Invoke(this, null);
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