using System.Threading.Tasks;
using Grpc.Core;
using FeedReader.Share.Protocols;
using FeedReader.ServerCore.Services;
using System.Linq;
using System;

namespace FeedReader.WebServer
{
    public class AnonymousService : Share.Protocols.AnonymousService.AnonymousServiceBase
    {
        private Validator Validator { get; } = new Validator();
        private AuthService AuthService { get; set; }
        private FeedService FeedService { get; set; }

        public AnonymousService(AuthService authService, FeedService feedService)
        {
            AuthService = authService;
            FeedService = feedService;
        }

        public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
        {
            var (user, nonce) = await AuthService.LoginAsync(request.Token);
            return new LoginResponse()
            {
                Token = user.Token,
                UserId = user.Id.ToString(),
                Nonce = nonce,
            };
        }

        public override async Task<GetRecommedFeedItemsResponse> GetRecommedFeedItems(GetRecommedFeedItemsRequest request, ServerCallContext context)
        {
            (request.StartIndex, request.Count) = Validator.ValidateStartIndexAndCount(request.StartIndex, request.Count);

            var feedItems = await FeedService.GetFeedItemsByIdAsync(feedId: null, request.StartIndex, request.Count);
            var response = new GetRecommedFeedItemsResponse();
            response.FeedItems.AddRange(feedItems.Select(f => f.ToProtocolFeedItem()));
            return response;
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
            (request.StartIndex, request.Count) = Validator.ValidateStartIndexAndCount(request.StartIndex, request.Count);

            var feedItems = await FeedService.GetFeedItemsByIdAsync(Guid.Parse(request.FeedId), request.StartIndex, request.Count);
            var response = new GetFeedItemsResponse();
            response.FeedItems.AddRange(feedItems.Select(f => f.ToProtocolFeedItem()));
            return response;
        }
    }
}