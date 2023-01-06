using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;
using FeedReader.ServerCore.Models;
using FeedReader.Share;
using JWT;
using JWT.Algorithms;
using JWT.Builder;
using JWT.Exceptions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;

namespace FeedReader.ServerCore.Services
{
    public class AuthService
    {
        IDbContextFactory<DbContext> DbFactory { get; set;  }
        string FeedReaderJwtSecret { get; set; }
        MicrosoftKeys MicrosoftKeyss { get; set; }

        private Dictionary<string, X509Certificate2> GoogleCerts { get; set; }

        public AuthService(IDbContextFactory<DbContext> dbFactory, IConfiguration configuration)
        {
            DbFactory = dbFactory;
            FeedReaderJwtSecret = configuration["FeedReaderJwtSecret"];

            // Get Microsoft & google Keys
            using (var http = new HttpClient())
            {
                MicrosoftKeyss = JsonConvert.DeserializeObject<MicrosoftKeys>(http.GetStringAsync(MICROSOFT_PUBLIC_KEYS_URL).Result);

                var content = http.GetStringAsync(GOOGLE_PUBLIC_KEYS_URL).Result;
                var keys = JsonConvert.DeserializeObject<IDictionary<string, string>>(content);
                var certs = new Dictionary<string, X509Certificate2>();
                foreach (var key in keys)
				{
                    certs[key.Key] = new X509Certificate2(Encoding.UTF8.GetBytes(key.Value));
				}
                GoogleCerts = certs;
            }
        }

        public async Task<(User, string)> LoginAsync(string jwtToken)
        {
            // Parse token.
            var token = ParseToken(jwtToken);

            // Get the uid.
            Guid uid;
            if (token.OAuthIssuer == OAuthIssuers.FeedReader)
            {
                uid = new Guid(token.OAuthId);
            }
            else
            {
                using (var db = DbFactory.CreateDbContext())
                {
                    var tmpUid = (await db.UserOAuthIds.FirstOrDefaultAsync(u => u.OAuthIssuer == token.OAuthIssuer && u.OAuthId == token.OAuthId))?.UserId;
                    if (tmpUid != null)
                    {
                        uid = tmpUid.Value;
                    }
                    else
                    {
                        // Register the user.
                        uid = Guid.NewGuid();
                        db.Add(new User()
                        {
                            Id = uid,
                            RegistrationTime = DateTime.UtcNow
                        });
                        db.UserOAuthIds.Add(new UserOAuthIds()
                        {
                            OAuthIssuer = token.OAuthIssuer,
                            OAuthId = token.OAuthId,
                            UserId = uid
                        });
                        var forceSubscribedFeeds = await db.FeedInfos.Where(f => f.ForceSubscribed).Select(f => f.Id).ToArrayAsync();
                        foreach (var feedId in forceSubscribedFeeds)
                        {
                            db.FeedSubscriptions.Add(new FeedSubscription()
                            {
                                UserId = uid,
                                FeedId = feedId,
                                Subscribed = true,
                            });
                        }
                        await db.SaveChangesAsync();
                    }
                }
            }

            // Retrive user from db.
            User user;
            using (var db = DbFactory.CreateDbContext())
            {
                user = await db.Users.FindAsync(uid);
            }
            if (user == null)
            {
                throw new KeyNotFoundException("User doesn't found.");
            }

            // Generate feedreader token.
            var now = DateTimeOffset.UtcNow;
            user.Token = new JwtBuilder()
                .WithAlgorithm(new HMACSHA256Algorithm())
                .WithSecret(FeedReaderJwtSecret)
                .AddClaim("iss", FEEDREADER_ISS)
                .AddClaim("aud", FEEDREADER_AUD)
                .AddClaim("uid", user.Id)
                .AddClaim("sid", Guid.NewGuid().ToString())
                .AddClaim("iat", now.ToUnixTimeSeconds())
                .AddClaim("exp", now.AddDays(7).ToUnixTimeSeconds())
                .Encode();
            return (user, token.Nonce);
        }

        public Guid ValidateFeedReaderUserToken(string feedReaderToken)
        {
            var token = ParseToken(feedReaderToken);
            if (token.OAuthIssuer != OAuthIssuers.FeedReader)
            {
                throw new UnauthorizedAccessException("Token is invalid");
            }
            return Guid.Parse(token.OAuthId);
        }

        Token ParseToken(string jwtToken)
        {
            try
            {
                var claims = new JwtBuilder().DoNotVerifySignature().Decode<IDictionary<string, string>>(jwtToken);
                var iss = GetValue(claims, "iss");
                if (string.IsNullOrEmpty(iss))
                {
                    throw new UnauthorizedAccessException("No iss claim in token");
                }

                switch (iss)
                {
                    case FEEDREADER_ISS:
                        return ParseFeedReaderToken(jwtToken);

                    case MICROSOFT_ISS:
                        return ParseMicrosoftToken(jwtToken);

                    case GOOGLE_ISS:
                        return ParseGoogleToken(jwtToken);

                    default:
                        throw new UnauthorizedAccessException("Not supported token issuer");
                }
            }
            catch (InvalidTokenPartsException)
            {
                throw new UnauthorizedAccessException("Token is invalid");
            }
            catch (TokenExpiredException)
            {
                throw new UnauthorizedAccessException("Token is expired");
            }
            catch (SignatureVerificationException)
            {
                throw new UnauthorizedAccessException("Token signature is invalid");
            }
        }

        Token ParseFeedReaderToken(string token)
        {
            // Validate the signature.
            var claims = new JwtBuilder().WithAlgorithm(new HMACSHA256Algorithm()).WithSecret(FeedReaderJwtSecret).MustVerifySignature().Decode<IDictionary<string, string>>(token);

            // Validate the audience.
            var aud = GetValue(claims, "aud");
            if (aud != FEEDREADER_AUD)
            {
                throw new UnauthorizedAccessException("Invalid aud claim in token");
            }

            // Validate the uid is existed.
            var uid = GetValue(claims, "uid");
            if (string.IsNullOrEmpty(uid))
            {
                throw new UnauthorizedAccessException("No uid claim in token");
            }

            // Validate the session id.
            var sid = GetValue(claims, "sid");
            if (string.IsNullOrEmpty(sid))
            {
                throw new UnauthorizedAccessException("No sid claim in token");
            }

            // All validateions pass
            return new Token
            {
                OriginalToken = token,
                OAuthId = uid,
                OAuthIssuer = OAuthIssuers.FeedReader,
                Nonce = string.Empty,
            };
        }

        Token ParseMicrosoftToken(string token)
        {
            // Decode kid
            var header = new JwtBuilder().DecodeHeader<IDictionary<string, string>>(token);
            var kid = GetValue(header, "kid");
            if (string.IsNullOrEmpty(kid))
            {
                throw new UnauthorizedAccessException("No kid claim in token");
            }

            // Get Microsoft publick key
            var key = MicrosoftKeyss.Keys.Where(k => k.Kid == kid).FirstOrDefault();
            if (key == null)
            {
                throw new UnauthorizedAccessException("Invalid kid claim in token");
            }

            // Get cert
            var cert = new X509Certificate2(new JwtBase64UrlEncoder().Decode(key.X5c.First()));

            // Validate the signature.
            var claims = new JwtBuilder().WithAlgorithm(new RS256Algorithm(cert)).MustVerifySignature().Decode<IDictionary<string, string>>(token);

            // Validate the audience.
            var aud = GetValue(claims, "aud");
            if (aud != MICROSOFT_AUD)
            {
                throw new UnauthorizedAccessException("Invalid aud claim in token");
            }

            // Validate the sub which is the user identity id in Microsoft account system for this application.
            var sub = GetValue(claims, "sub");
            if (string.IsNullOrEmpty(sub))
            {
                throw new UnauthorizedAccessException("No sub claim in token");
            }

            // All validations pass
            return new Token
            {
                OriginalToken = token,
                OAuthId = sub,
                OAuthIssuer = OAuthIssuers.Microsoft,
                Nonce = GetValue(claims, "nonce") ?? string.Empty,
            };
        }

        Token ParseGoogleToken(string token)
		{
            // Decode kid
            var header = new JwtBuilder().DecodeHeader<IDictionary<string, string>>(token);
            var kid = GetValue(header, "kid");
            var cert = GoogleCerts.TryGetValue(kid);
            if (cert == null)
			{
                throw new FeedReaderException(HttpStatusCode.Unauthorized);
			}

            // Validate algorithm type.
            var alg = header.TryGetValue("alg");
            if (alg != "RS256")
            {
                throw new FeedReaderException(HttpStatusCode.Unauthorized);
            }

            // Validate the signature.
            var claims = new JwtBuilder().WithAlgorithm(new RS256Algorithm(cert)).MustVerifySignature().Decode<IDictionary<string, string>>(token);

            // Validate the audience.
            var aud = claims.TryGetValue("aud");
            if (aud != GOOGLE_AUD)
            {
                throw new FeedReaderException(HttpStatusCode.Unauthorized);
            }

            // Validate the sub which is the user identity id in Google account system.
            var sub = GetValue(claims, "sub");
            if (string.IsNullOrEmpty(sub))
            {
                throw new FeedReaderException(HttpStatusCode.Unauthorized);
            }

            // All validations pass
            return new Token
            {
                OriginalToken = token,
                OAuthId = sub,
                OAuthIssuer = OAuthIssuers.Google,
                Nonce = GetValue(claims, "nonce") ?? string.Empty,
            };
        }

        string GetValue(IDictionary<string, string> dict, string key)
        {
            string value;
            return dict.TryGetValue(key, out value) ? value : null;
        }

        const string FEEDREADER_ISS = "https://auth.feedreader.org/v1.0";
        const string FEEDREADER_AUD = "118f64a6-5e60-4835-bf7c-83f677e91ad0";

        const string MICROSOFT_PUBLIC_KEYS_URL = "https://login.microsoftonline.com/common/discovery/v2.0/keys";
        const string MICROSOFT_ISS = "https://login.microsoftonline.com/9188040d-6c67-4c5b-b112-36a304b66dad/v2.0";
        const string MICROSOFT_AUD = "ecdce664-80f7-4d97-a047-ae75035b957c";

        const string GOOGLE_PUBLIC_KEYS_URL = "https://www.googleapis.com/oauth2/v1/certs";
        const string GOOGLE_ISS = "https://accounts.google.com";
        const string GOOGLE_AUD = "830207024957-oh9b7oth864jtkb32glia884o0neq1vl.apps.googleusercontent.com";

        class MicrosoftKey
        {
            public string Kty { get; set; }
            public string Use { get; set; }
            public string Kid { get; set; }
            public IEnumerable<string> X5c { get; set; }
        }

        class MicrosoftKeys
        {
            public IEnumerable<MicrosoftKey> Keys { get; set; }
        }

        class Token
        {
            public string OriginalToken { get; set; }

            public string OAuthId { get; set; }

            public string OAuthIssuer { get; set; }

            /// <summary>
            /// Session id. Only available for feedreader token.
            /// </summary>
            public string SessionId { get; set; }

            public string Nonce { get; set; }
        }
    }
}