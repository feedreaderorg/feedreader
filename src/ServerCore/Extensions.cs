using FeedReader.ServerCore.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;

namespace FeedReader.ServerCore
{
    public static class Extensions
    {
        static Dictionary<string, string> _timeZones = new Dictionary<string, string>() {
            {"ACDT", "+1030"},
            {"ACST", "+0930"},
            {"ADT", "-0300"},
            {"AEDT", "+1100"},
            {"AEST", "+1000"},
            {"AHDT", "-0900"},
            {"AHST", "-1000"},
            {"AST", "-0400"},
            {"AT", "-0200"},
            {"AWDT", "+0900"},
            {"AWST", "+0800"},
            {"BAT", "+0300"},
            {"BDST", "+0200"},
            {"BET", "-1100"},
            {"BST", "-0300"},
            {"BT", "+0300"},
            {"BZT2", "-0300"},
            {"CADT", "+1030"},
            {"CAST", "+0930"},
            {"CAT", "-1000"},
            {"CCT", "+0800"},
            {"CDT", "-0500"},
            {"CED", "+0200"},
            {"CET", "+0100"},
            {"CEST", "+0200"},
            {"CST", "-0600"},
            {"EAST", "+1000"},
            {"EDT", "-0400"},
            {"EED", "+0300"},
            {"EET", "+0200"},
            {"EEST", "+0300"},
            {"EST", "-0500"},
            {"FST", "+0200"},
            {"FWT", "+0100"},
            {"GMT", "GMT"},
            {"GST", "+1000"},
            {"HDT", "-0900"},
            {"HST", "-1000"},
            {"IDLE", "+1200"},
            {"IDLW", "-1200"},
            {"IST", "+0530"},
            {"IT", "+0330"},
            {"JST", "+0900"},
            {"JT", "+0700"},
            {"MDT", "-0600"},
            {"MED", "+0200"},
            {"MET", "+0100"},
            {"MEST", "+0200"},
            {"MEWT", "+0100"},
            {"MST", "-0700"},
            {"MT", "+0800"},
            {"NDT", "-0230"},
            {"NFT", "-0330"},
            {"NT", "-1100"},
            {"NST", "+0630"},
            {"NZ", "+1100"},
            {"NZST", "+1200"},
            {"NZDT", "+1300"},
            {"NZT", "+1200"},
            {"PDT", "-0700"},
            {"PST", "-0800"},
            {"ROK", "+0900"},
            {"SAD", "+1000"},
            {"SAST", "+0900"},
            {"SAT", "+0900"},
            {"SDT", "+1000"},
            {"SST", "+0200"},
            {"SWT", "+0100"},
            {"USZ3", "+0400"},
            {"USZ4", "+0500"},
            {"USZ5", "+0600"},
            {"USZ6", "+0700"},
            {"UT", "-0000"},
            {"UTC", "-0000"},
            {"UZ10", "+1100"},
            {"WAT", "-0100"},
            {"WET", "-0000"},
            {"WST", "+0800"},
            {"YDT", "-0800"},
            {"YST", "-0900"},
            {"ZP4", "+0400"},
            {"ZP5", "+0500"},
            {"ZP6", "+0600"}
        };

        public static IServiceCollection AddFeedReaderServerCoreServices(this IServiceCollection services, IConfiguration configuration)
        {
            services.AddLogging();
            services.AddTransient<HttpClient>();
            services.AddDbContextFactory<DbContext>(options => options.UseNpgsql(configuration.GetConnectionString("DbConnectionString")));
            services.AddSingleton<AuthService>();
            services.AddSingleton<FeedService>();
            services.AddSingleton<UserService>();
            services.AddSingleton<FileService>();
            return services;
        }

        public static DateTime ToUtcDateTime(this string str)
        {
            DateTime d;
            if (DateTime.TryParse(str, out d))
            {
                return d.ToUniversalTime();
            }

            foreach (var item in _timeZones)
            {
                if (str.IndexOf(item.Key) > 0)
                {
                    return DateTime.Parse(str.Replace(item.Key, item.Value)).ToUniversalTime();
                }
            }
            throw new FormatException();
        }

        public static Guid Md5HashToGuid(this string str)
        {
            return new Guid(MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(str)));
        }

        public static void UpdateEntity<TEntity>(this DbContext context, Expression<Func<TEntity>> updateExpression, string keyFieldName = "Id")
        {
            var memberInitExpression = updateExpression.Body as MemberInitExpression;
            if (memberInitExpression == null)
            {
                throw new ArgumentException("Update expression must be a member initialization");
            }

            var entity = updateExpression.Compile().Invoke();
            context.Attach(entity);

            var updatedPropNames = memberInitExpression.Bindings.Select(b => b.Member.Name);
            foreach (var propName in updatedPropNames)
            {
                if (propName == keyFieldName)
                {
                    continue;
                }
                context.Entry(entity).Property(propName).IsModified = true;
            }
        }

        public static Uri GetHttpsVersion(this Uri uri)
        {
            if (uri.Scheme == Uri.UriSchemeHttp && uri.IsDefaultPort)
            {
                var ub = new UriBuilder(uri);
                ub.Scheme = Uri.UriSchemeHttps;
                ub.Port = 443;
                return ub.Uri;
            }
            else
            {
                return uri;
            }
        }

        public static T TryGetValue<T>(this IDictionary<string, T> dict, string key) where T : class
        {
            T value;
            return (dict != null && key != null && dict.TryGetValue(key, out value)) ? value : null;
        }
    }
}
