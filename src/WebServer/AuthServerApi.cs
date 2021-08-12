using System.Threading.Tasks;
using Grpc.Core;
using FeedReader.Share.Protocols;
using FeedReader.WebServer.Services;

namespace FeedReader.WebServer
{
    public class AuthServerApi : Share.Protocols.AuthServerApi.AuthServerApiBase
    {
        AuthService AuthService { get; set; }

        public AuthServerApi(AuthService authService)
        {
            AuthService = authService;
        }

        public override async Task<LoginResponse> Login(LoginRequest request, ServerCallContext context)
        {
            var user = await AuthService.LoginAsync(request.Token);
            return new LoginResponse()
            {
                Token = user.Token,
                User = user.ToProtocolUser()
            };
        }
    }
}