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
using Microsoft.AspNetCore.Components;
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
        #region Properties
        string ServerAddress { get; set; }

        public string Id { get; set; }

        public string Token { get; set; }

        public UserRole Role { get; set; }

        public string AvatarUri { get; set; }

        public event EventHandler OnStateChanged;

        public List<Feed> SubscribedFeeds { get; set; } = new List<Feed>();

        public WebServerApiClient WebServerApi { get; set; }

        [Inject]
        public IJSRuntime JSRuntime { get; set; }
        #endregion

        AuthServerApiClient AuthServerApi { get; set; }
        
        public User()
        {
            Reset();
        }

        public void SetServerAddress(string serverAddress)
        {
            ServerAddress = serverAddress;
            var httpHandler = new HttpClientHandler();
            var grpcHandler = new GrpcWebHandler(GrpcWebMode.GrpcWebText, httpHandler);
            var grpcChannel = GrpcChannel.ForAddress(serverAddress, new GrpcChannelOptions { HttpHandler = grpcHandler });
            AuthServerApi = new AuthServerApiClient(grpcChannel);
            RefreshWebServerApi();
        }

        public async Task LoginAsync(string token)
        {
            var response = await AuthServerApi.LoginAsync(new Share.Protocols.LoginRequest()
            {
                Token = token,
            });

            // Update user.
            Id = response.UserId;
            Token = response.Token;
            Role = UserRole.Normal;
            RefreshWebServerApi();
            OnStateChanged?.Invoke(this, null);
        }

        public async Task LogoutAsync()
        {
            // TODO: logout from server?
            await Task.Delay(2000);
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

        public Feed GetFeed(string feedSubscriptionName)
        {
            if (string.IsNullOrWhiteSpace(feedSubscriptionName))
            {
                return null;
            }
            else
            {
                // TODO: return feed which is not subscribed by this user.
                return SubscribedFeeds.FirstOrDefault(f => f.SubscriptionName == feedSubscriptionName);
            }
        }

        void Reset()
        {
            Token = "";
            Role = UserRole.Guest;
            AvatarUri = "img/default-avatar.png";
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
                SubscribeEvents();
            }
        }

        async void SubscribeEvents()
        {
            var events = WebServerApi.SubscribeEvents(new Google.Protobuf.WellKnownTypes.Empty());
            while (await events.ResponseStream.MoveNext(default(CancellationToken)))
            {
                switch (events.ResponseStream.Current.EventCase)
                {
                    case Share.Protocols.Event.EventOneofCase.User:
                        UpdateUser(events.ResponseStream.Current.User);
                        break;
                }
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
            foreach (var feed in user.SubscribedFeeds)
            {
                var f = SubscribedFeeds.FirstOrDefault(f => f.Id == feed.Id);
                if (f != null)
                {
                    // Update existed feed.
                    f.Description = feed.Description;
                    f.IconUri = feed.IconUri;
                    f.Name = feed.Name;
                    f.SubscriptionName = feed.SubscriptionName;
                    f.TotalFavorites = feed.TotalFavorites;
                    f.TotalPosts = feed.TotalPosts;
                    f.TotalSubscribers = feed.TotalSubscribers;
                    f.SiteLink = feed.SiteLink;
                }
                else
                {
                    // Add new feed.
                    SubscribedFeeds.Add(feed.ToModelFeed());
                }
            }

            // Remove unexisted feed
            SubscribedFeeds.RemoveAll(f => user.SubscribedFeeds.FirstOrDefault(x => x.Id == f.Id) == null);

            // Notify user state has been changed.
            OnStateChanged?.Invoke(this, null);
        }

        void OnUnauthenticatedException(string message)
        {
            // TODO: log exception message.
            Reset();
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

            public override AsyncServerStreamingCall<TResponse> AsyncServerStreamingCall<TRequest, TResponse>(TRequest request, ClientInterceptorContext<TRequest, TResponse> context, AsyncServerStreamingCallContinuation<TRequest, TResponse> continuation)
            {
                var call = continuation(request, context);
                return new AsyncServerStreamingCall<TResponse>(new ResponseStream<TResponse>(call.ResponseStream, OnUnauthenticatedException), call.ResponseHeadersAsync, call.GetStatus, call.GetTrailers, call.Dispose);
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

        class ResponseStream<TResponse> : IAsyncStreamReader<TResponse>
        {
            IAsyncStreamReader<TResponse> Stream { get; set; }
            Action<string> OnUnauthenticatedException { get; set; }

            public TResponse Current => Stream.Current;

            public ResponseStream(IAsyncStreamReader<TResponse> stream, Action<string> onUnauthenticatedException)
            {
                Stream = stream;
                OnUnauthenticatedException = onUnauthenticatedException;
            }

            public Task<bool> MoveNext(CancellationToken cancellationToken)
            {
                return GrpcInterceptor.ProcessResponse(Stream.MoveNext(cancellationToken), OnUnauthenticatedException);
            }
        }
        #endregion
    }
}