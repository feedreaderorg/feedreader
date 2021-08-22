using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
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

        [Inject]
        public IJSRuntime JSRuntime { get; set; }
        #endregion

        AuthServerApiClient AuthServerApi { get; set; }
        WebServerApiClient WebServerApi { get; set; }

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
            OnStateChanged?.Invoke(this, null);
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

        void Reset()
        {
            Token = "";
            Role = UserRole.Guest;
            AvatarUri = "img/default-avatar.png";
            SubscribedFeeds.Clear();
            RefreshWebServerApi();
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
                WebServerApi = new WebServerApiClient(grpcChannel);
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
                    f.TotalSubscribers = feed.TotalSubscribers;
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

        #region
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
        #endregion
    }
}