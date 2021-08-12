namespace FeedReader.WebServer
{
    public static class UserMapper
    {
        public static Share.Protocols.UserRole ToProtocolUserRole(string role)
        {
            switch (role)
            {
                default:
                case Models.UserRoles.Guest:
                    return Share.Protocols.UserRole.Guest;

                case Models.UserRoles.Unregistered:
                    return Share.Protocols.UserRole.Unregistered;

                case Models.UserRoles.Normal:
                    return Share.Protocols.UserRole.Normal;
            }
        }

        public static Share.Protocols.User ToProtocolUser(this Models.User u)
        {
            return new Share.Protocols.User()
            {
                Username = u.Username ?? string.Empty,
                Role = ToProtocolUserRole(u.Role),
                DisplayName = u.DisplayName ?? string.Empty,
                AvatarUri = u.AvatarUri ?? string.Empty,
                RegistrationTime = Google.Protobuf.WellKnownTypes.Timestamp.FromDateTime(u.RegistrationTime)
            };
        }
    }
}