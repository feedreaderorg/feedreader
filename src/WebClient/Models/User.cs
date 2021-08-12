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
        Unregistered,
        Disabled,
        Normal
    };

    public class User
    {
        #region Properties
        string Token { get; set; }

        public string Username { get; set; }

        public string DisplayName { get; set; }

        public UserRole Role { get; set; }

        public string AvatarUri { get; set; }
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
            UpdateFrom(response.User);
        }

        void UpdateFrom(Share.Protocols.User u)
        {
            Username = u.Username;
            DisplayName = u.DisplayName;
            Role = ProtocolUserRoleToUserRole(u.Role);
            AvatarUri = u.AvatarUri;
        }

        static UserRole ProtocolUserRoleToUserRole(Share.Protocols.UserRole role)
        {
            switch (role)
            {
                default:
                case Share.Protocols.UserRole.Guest:
                    return UserRole.Guest;

                case Share.Protocols.UserRole.Unregistered:
                    return UserRole.Unregistered;

                case Share.Protocols.UserRole.Disabled:
                    return UserRole.Disabled;

                case Share.Protocols.UserRole.Normal:
                    return UserRole.Normal;
            }
        }
    }
}