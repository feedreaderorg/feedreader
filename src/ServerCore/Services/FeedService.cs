using FeedReader.ServerCore.Models;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Xml;

namespace FeedReader.ServerCore.Services
{
    public class FeedService
    {
        IDbContextFactory<DbContext> DbFactory { get; set; }

        HttpClient HttpClient { get; set; }

        public FeedService(IDbContextFactory<DbContext> dbFactory, HttpClient httpClient)
        {
            DbFactory = dbFactory;
            HttpClient = httpClient;
        }

        public async Task<List<FeedInfo>> DiscoverFeedsAsync(string query)
        {
            var feeds = new List<FeedInfo>();

            // Normalize query string.
            query = query.Trim().TrimEnd('/');

            // Treat it as uri.
            Uri uri;
            if (Uri.TryCreate(query, UriKind.Absolute, out uri))
            {
                // Try to get from https if this is http.
                var httpsUri = GetHttpsUri(uri);
                if (httpsUri != null)
                {
                    await TryToDiscoverFeedsFromUriAsync(httpsUri, feeds);
                    if (feeds.Count > 0)
                    {
                        return feeds;
                    }
                }

                if (httpsUri != uri)
                {
                    // Ok, try uri directly.
                    await TryToDiscoverFeedsFromUriAsync(uri, feeds);
                }
            }

            return feeds;
        }

        async Task TryToDiscoverFeedsFromUriAsync(Uri uri, List<FeedInfo> feeds)
        {
            var idFromUri = new Guid(MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(uri.ToString().ToLower())));

            // Try to find from idFromUri.
            using (var db = DbFactory.CreateDbContext())
            {
                var feedInDb = await db.FeedInfos.FirstOrDefaultAsync(f => f.IdFromUri == idFromUri);
                if (feedInDb != null)
                {
                    feeds.Add(feedInDb);
                    return;
                }
            }

            // Get the content from the uri.
            var content = await HttpClient.GetStringAsync(uri);
            var feed = await TryToParseFeedFromContentAsync(content);
            if (feed != null)
            {
                feed.Id = Guid.NewGuid();
                feed.IdFromUri = idFromUri;
                feed.RegistrationTime = DateTime.UtcNow;
                feed.Uri = uri.ToString();

                // Save to db.
                using (var db = DbFactory.CreateDbContext())
                {
                    if (await db.FeedInfos.FirstOrDefaultAsync(f => f.IdFromUri == idFromUri) == null)
                    {
                        bool findUnusedSubscriptionName = false;
                        for (var i = 0; i < 10; ++i)
                        {
                            feed.SubscriptionName = GenerateFeedSubscriptionName(feed, feed.SubscriptionName);
                            if (await db.FeedInfos.FirstOrDefaultAsync(f => f.SubscriptionName == feed.SubscriptionName) == null)
                            {
                                findUnusedSubscriptionName = true;
                                break;
                            }
                        }
                        if (findUnusedSubscriptionName)
                        {
                            db.FeedInfos.Add(feed);
                            await db.SaveChangesAsync();
                        }
                        else
                        {
                            throw new Exception("Failed to generate unused subscription name");
                        }
                    }
                }

                // Return
                feeds.Add(feed);
                return;
            }

            // Try to discover feed from html content directly.
            await TryToDiscoverFeedsFromHtmlAsync(content, feeds);
        }

        string GenerateFeedSubscriptionName(FeedInfo feed, string lastProposedName)
        {
            var proposedName = lastProposedName;
            if (!string.IsNullOrEmpty(proposedName))
            {
                var lastHyphen = proposedName.LastIndexOf("-0");
                if (lastHyphen > -1)
                {
                    proposedName.Substring(0, lastHyphen);
                }
                return $"{proposedName}-{new Random().Next(1000)}";
            }
            else
            {
                proposedName = "";
                var uri = new Uri(feed.Uri);
                var domainsPart = uri.Host.ToLower().Split('.');
                for (var i = 0; i < domainsPart.Length - 1; ++i)
                {
                    var str = new string(domainsPart[i].Where(c => char.IsLetterOrDigit(c)).ToArray());
                    if (str == "www")
                    {
                        continue;
                    }
                    else
                    {
                        if (proposedName == "")
                        {
                            proposedName = str;
                        }
                        else
                        {
                            proposedName = $"{proposedName}-{str}";
                        }
                    }
                }
                if (proposedName == "")
                {
                    proposedName = new Random().Next(1000).ToString();
                }
                return proposedName;
            }
        }

        async Task<FeedInfo> TryToParseFeedFromContentAsync(string content)
        {
            var feed = TryToParseJsonFeed(content) ?? TryToParseXmlFeed(content);
            if (feed != null)
            {
                if (string.IsNullOrEmpty(feed.IconUri))
                {
                    Uri websiteUri;
                    if (Uri.TryCreate(feed.WebsiteLink, UriKind.Absolute, out websiteUri))
                    {
                        var httpsUri = GetHttpsUri(websiteUri);
                        if (httpsUri != null)
                        {
                            feed.IconUri = await TryToGetIconUriFromWebsiteUriAsync(httpsUri);
                        }

                        if (string.IsNullOrEmpty(feed.IconUri) && httpsUri != websiteUri)
                        {
                            feed.IconUri = await TryToGetIconUriFromWebsiteUriAsync(websiteUri);
                        }
                    }
                }
            }
            return feed;
        }

        FeedInfo TryToParseJsonFeed(string content)
        {
            // TODO
            return null;
        }

        FeedInfo TryToParseXmlFeed(string content)
        {
            try
            {
                var xml = new XmlDocument();
                xml.LoadXml(content);
                if (xml.DocumentElement?.Name == "feed")
                {
                    return ParseAtomFeed(xml);
                }
                else
                {
                    return ParseRssFeed(xml);
                }
            }
            catch
            {
                return null;
            }
        }

        FeedInfo ParseRssFeed(XmlDocument xml)
        {
            var feed = new FeedInfo();

            // Parse channel. As spec, every feed has only one channel.
            var channelNode = xml.SelectSingleNode("/rss/channel");
            feed.Name = channelNode["title"].InnerText;
            feed.WebsiteLink = channelNode["link"].InnerText;
            feed.Description = channelNode["description"].InnerText;
            feed.IconUri = channelNode.SelectSingleNode("/rss/channel/image")?["url"]?.InnerText;
            return feed;
        }

        FeedInfo ParseAtomFeed(XmlDocument xml)
        {
            throw new NotImplementedException();
        }

        Task TryToDiscoverFeedsFromHtmlAsync(string content, List<FeedInfo> feeds)
        {
            return Task.CompletedTask;
        }

        async Task<string> TryToGetIconUriFromWebsiteUriAsync(Uri uri)
        {
            try
            {
                var link = uri.ToString();
                var web = new HtmlWeb();
                var html = await web.LoadFromWebAsync(link);
                var el = html.DocumentNode.SelectSingleNode("/html/head/link[@rel='apple-touch-icon' and @href]") ??
                         html.DocumentNode.SelectSingleNode("/html/head/link[@rel='shortcut icon' and @href]") ??
                         html.DocumentNode.SelectSingleNode("/html/head/link[@rel='icon' and @href]");
                if (el != null)
                {
                    var uriBuilder = new UriBuilder(link);
                    uriBuilder.Path = el.Attributes["href"].Value;
                    return uriBuilder.ToString();
                }
            }
            catch
            {
            }
            return null;
        }

        Uri GetHttpsUri(Uri uri)
        {
            if (uri.Scheme == Uri.UriSchemeHttps)
            {
                return uri;
            }
            else if (uri.Scheme == Uri.UriSchemeHttp && uri.IsDefaultPort)
            {
                var ub = new UriBuilder(uri);
                ub.Scheme = Uri.UriSchemeHttps;
                ub.Port = 443;
                return ub.Uri;
            }
            else
            {
                return null;
            }
        }
    }
}