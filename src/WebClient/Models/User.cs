using System;
using System.Net.Http;
using System.Threading.Tasks;
using Grpc.Net.Client;
using Grpc.Net.Client.Web;
using static FeedReader.Share.Protocols.AuthServerApi;

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
        string Token { get; set; }

        public UserRole Role { get; set; }
        #endregion

        AuthServerApiClient AuthServerapi { get; set; }

        public User()
        {
        }

        public User(string serverAddress)
        {
            var httpHandler = new HttpClientHandler();
            var grpcHandler = new GrpcWebHandler(GrpcWebMode.GrpcWebText, httpHandler);
            var grpcChannel = GrpcChannel.ForAddress(serverAddress, new GrpcChannelOptions { HttpHandler = grpcHandler });
            AuthServerapi = new AuthServerApiClient(grpcChannel);
        }

        public async Task LoginAsync(string token)
        {
            var response = await AuthServerapi.LoginAsync(new Share.Protocols.LoginRequest()
            {
                Token = token,
            });
            Token = response.Token;
            Role = UserRole.Normal;
        }
    }
}