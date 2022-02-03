using System.Threading.Tasks;
using Grpc.Core;
using FeedReader.Share.Protocols;
using FeedReader.ServerCore.Services;
using System.Linq;

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
            var user = await AuthService.LoginAsync(request.Token);
            return new LoginResponse()
            {
                Token = user.Token,
                UserId = user.Id.ToString(),
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
    }
}