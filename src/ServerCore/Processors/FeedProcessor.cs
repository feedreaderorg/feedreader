using FeedReader.ServerCore.Models;
using HtmlAgilityPack;
using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace FeedReader.ServerCore.Processors
{
    public class FeedProcessor
    {
        private HttpClient HttpClient { get; set; }

        public FeedProcessor(HttpClient httpClient)
        {
            HttpClient = httpClient;
        }

        public async Task<FeedInfo> TryToParseFeedFromContent(string content, bool parseItems, CancellationToken cancellationToken = default(CancellationToken))
        {
            var feed = FeedParser.TryCreateFeedParser(content)?.TryParseFeed(parseItems);
            if (feed != null)
            {
                if (string.IsNullOrEmpty(feed.IconUri))
                {
                    Uri websiteUri;
                    if (Uri.TryCreate(feed.WebsiteLink, UriKind.Absolute, out websiteUri))
                    {
                        var httpsUri = websiteUri.GetHttpsVersion();
                        feed.IconUri = await TryToGetIconUriFromWebsiteUriAsync(httpsUri, cancellationToken);
                        if (string.IsNullOrEmpty(feed.IconUri) && httpsUri != websiteUri)
                        {
                            feed.IconUri = await TryToGetIconUriFromWebsiteUriAsync(websiteUri, cancellationToken);
                        }
                    }
                }
            }
            return feed;
        }

        private async Task<string> TryToGetIconUriFromWebsiteUriAsync(Uri uri, CancellationToken cancellationToken)
        {
            try
            {
                var html = new HtmlDocument();
                html.LoadHtml(await HttpClient.GetStringAsync(uri));
                var el = html.DocumentNode.SelectSingleNode("/html/head/link[@rel='apple-touch-icon' and @href]") ??
                         html.DocumentNode.SelectSingleNode("/html/head/link[@rel='shortcut icon' and @href]") ??
                         html.DocumentNode.SelectSingleNode("/html/head/link[@rel='icon' and @href]");
                var iconPath = el?.Attributes["href"]?.Value;
                if (el != null)
                {
                    if (Uri.IsWellFormedUriString(iconPath, UriKind.Absolute))
                    {
                        return iconPath;
                    }
                    else if (Uri.IsWellFormedUriString(iconPath, UriKind.Relative))
                    {
                        return new Uri(uri, iconPath).ToString();
                    }
                }
                else
                {
                    // Try to get icon from /favicon.ico
                    var ub = new UriBuilder(uri);
                    ub.Path = "/favicon.ico";
                    try
                    {
                        var path = ub.ToString();
                        var message = await HttpClient.GetAsync(path);
                        if (message.StatusCode == System.Net.HttpStatusCode.OK)
                        {
                            return path;
                        }
                    }
                    catch
                    {
                    }
                }
            }
            catch
            {
            }
            return null;
        }
    }
}
