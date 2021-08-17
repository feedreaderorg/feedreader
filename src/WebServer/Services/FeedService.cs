using FeedReader.WebServer.Models;
using HtmlAgilityPack;
using Microsoft.AspNetCore.Components;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;
using System.Xml;

namespace FeedReader.WebServer.Services
{
    public class FeedService
    {
        HttpClient HttpClient { get; set; }

        public FeedService(HttpClient httpClient)
        {
            HttpClient = httpClient;
        }

        public async Task<List<FeedInfo>> DiscoverFeedsAsync(string query)
        {
            var feeds = new List<FeedInfo>();

            Uri uri;
            if (Uri.TryCreate(query, UriKind.Absolute, out uri))
            {
                await DiscoverFeedsFromUriAsync(uri, feeds);
            }

            return feeds;
        }

        async Task DiscoverFeedsFromUriAsync(Uri uri, List<FeedInfo> feeds)
        {
            var content = await HttpClient.GetStringAsync(uri);
            var feed = await TryToParseFeedFromContentAsync(content);
            if (feed != null)
            {
                feeds.Add(feed);
            }
            else
            {
                await DiscoverFeedsFromHtmlAsync(content, feeds);
            }
        }

        async Task<FeedInfo> TryToParseFeedFromContentAsync(string content)
        {
            var feed = TryToParseJsonFeed(content) ?? TryToParseXmlFeed(content);
            if (feed != null)
            {
                if (string.IsNullOrEmpty(feed.IconUri))
                {
                    feed.IconUri = await TryToGetIconUriFromWebsiteLinkAsync(feed.WebsiteLink);
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

        Task DiscoverFeedsFromHtmlAsync(string content, List<FeedInfo> feeds)
        {
            throw new NotImplementedException();
        }

        async Task<string> TryToGetIconUriFromWebsiteLinkAsync(string websiteLink)
        {
            try
            {
                if (string.IsNullOrEmpty(websiteLink))
                {
                    return null;
                }

                var web = new HtmlWeb();
                var html = await web.LoadFromWebAsync(websiteLink);
                var el = html.DocumentNode.SelectSingleNode("/html/head/link[@rel='apple-touch-icon' and @href]") ??
                         html.DocumentNode.SelectSingleNode("/html/head/link[@rel='shortcut icon' and @href]") ??
                         html.DocumentNode.SelectSingleNode("/html/head/link[@rel='icon' and @href]");
                if (el != null)
                {
                    var uriBuilder = new UriBuilder(websiteLink);
                    uriBuilder.Path = el.Attributes["href"].Value;
                    return uriBuilder.ToString();
                }
            }
            catch
            {
            }
            return null;
        }
    }
}