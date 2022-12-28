using FeedReader.ServerCore.Models;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Xml;

namespace FeedReader.ServerCore.Processors
{
    public abstract class FeedParser
    {
        static Regex HtmlTagRegex { get; } = new Regex("<.*?>");

        static Regex WhiteSpaceRegex { get; } = new Regex("\\s+");

        static Regex ImgRegex { get; } = new Regex("<img\\s.*?\\bsrc\\s*=\\s*[\"'](.*?)[\"'].*?>");

        static Regex VideoRegex { get; } = new Regex("<video\\s.*?\\bposter\\s*=\\s*[\"'](.*?)[\"'].*?>");

        public static FeedParser TryCreateFeedParser(string content)
        {
            try
            {
                var xml = new XmlDocument();
                xml.LoadXml(content);
                if (xml.DocumentElement?.Name == "feed")
                {
                    return new AtomFeedParser(xml);
                }
                else
                {
                    return new RssFeedParser(xml);
                }
            }
            catch
            {
            }

            try
            {
                return new JsonFeedParser(content);
            }
            catch
            {
            }

            try
            {
                // remove invalid character and try again.
                content = new string(content.Select(c => XmlConvert.IsXmlChar(c) ? c : '.').ToArray());
                var xml = new XmlDocument();
                xml.LoadXml(content);
                if (xml.DocumentElement?.Name == "feed")
                {
                    return new AtomFeedParser(xml);
                }
                else
                {
                    return new RssFeedParser(xml);
                }
            }
            catch
            {
            }

            return null;
        }

        public abstract FeedInfo TryParseFeed(bool parseItems);

        protected string TryGetImageUrl(string content)
        {
            if (!string.IsNullOrWhiteSpace(content))
            {
                var match = ImgRegex.Match(content);
                if (match.Success)
                {
                    var uri = match.Groups[1].Value;
                    if (Uri.IsWellFormedUriString(uri, UriKind.Absolute))
                    {
                        return uri;
                    }
                }

                match = VideoRegex.Match(content);
                if (match.Success)
                {
                    var uri = match.Groups[1].Value;
                    if (Uri.IsWellFormedUriString(uri, UriKind.Absolute))
                    {
                        return uri;
                    }
                }
            }
            return null;
        }

        protected string GetSummary(string content)
        {
            if (content != null)
            {
                content = HtmlTagRegex.Replace(content, string.Empty);
                content = WhiteSpaceRegex.Replace(content, " ").Trim();
                content = WebUtility.HtmlDecode(content);
                var str = new StringInfo(content);
                content = str.SubstringByTextElements(0, Math.Min(str.LengthInTextElements,  500));
            }
            else
            {
                content = string.Empty;
            }
            return content;
        }
    }

    public abstract class XmlFeedParser : FeedParser
    {
        public XmlDocument FeedXml { get; }
        public XmlNamespaceManager FeedXmlNS { get; }

        public XmlFeedParser(XmlDocument xml)
        {
            FeedXml = xml;
            FeedXmlNS = new XmlNamespaceManager(xml.NameTable);
            FeedXmlNS.AddNamespace("media", "http://search.yahoo.com/mrss/");
        }

        protected string TryGetImageUrl(XmlNode xml)
        {
            // Find media content, standard: media rss, https://www.rssboard.org/media-rss
            string imgUrl = null;
            var mediaContents = xml.SelectNodes("media:content", FeedXmlNS) ?? xml.SelectNodes("media:group/media:content", FeedXmlNS);
            if (mediaContents != null)
            {
                foreach (XmlNode mediaContent in mediaContents)
                {
                    var attributes = mediaContent.Attributes;
                    var medium = attributes["medium"]?.InnerText;
                    if (medium == "image")
                    {
                        // is default?
                        if (attributes["isDefault"]?.InnerText.Trim().ToLower() == "true")
                        {
                            imgUrl = attributes["url"].InnerText;
                            break;
                        }
                        else if (string.IsNullOrWhiteSpace(imgUrl))
                        {
                            imgUrl = attributes["url"].InnerText;
                        }
                    }
                    else
                    {
                        var type = attributes["type"]?.InnerText;
                        if (type?.StartsWith("image/") == true)
                        {
                            imgUrl = attributes["url"].InnerText;
                        }
                    }
                }
            }
            if (!string.IsNullOrWhiteSpace(imgUrl))
            {
                return imgUrl;
            }

            // have media:thumbnail?
            var thumbnail = xml.SelectSingleNode("media:thumbnail", FeedXmlNS);
            if (thumbnail != null)
            {
                imgUrl = thumbnail.Attributes["url"].InnerText;
            }
            if (!string.IsNullOrWhiteSpace(imgUrl))
            {
                return imgUrl;
            }

            // have media:group/media:thumbnail?
            thumbnail = xml.SelectSingleNode("media:group/media:thumbnail", FeedXmlNS);
            if (thumbnail != null)
            {
                imgUrl = thumbnail.Attributes["url"].InnerText;
            }
            if (!string.IsNullOrWhiteSpace(imgUrl))
            {
                return imgUrl;
            }

            // Not found.
            return null;
        }
    }

    public class JsonFeedParser : FeedParser
    {
        public JsonFeedParser(string jsonString)
        {
            throw new NotImplementedException();
        }

        public override FeedInfo TryParseFeed(bool parseItems)
        {
            throw new NotImplementedException();
        }
    }

    public class AtomFeedParser : XmlFeedParser
    {
        public AtomFeedParser(XmlDocument xml)
            : base(xml)
        {
            throw new NotImplementedException();
        }

        public override FeedInfo TryParseFeed(bool parseItems)
        {
            throw new NotImplementedException();
        }
    }

    public class RssFeedParser : XmlFeedParser
    {
        public RssFeedParser(XmlDocument xml)
            : base(xml)
        {
            FeedXmlNS.AddNamespace("content", "http://purl.org/rss/1.0/modules/content/");
        }

        public override FeedInfo TryParseFeed(bool parseItems)
        {
            // Parse channel. As spec, every feed has only one channel.
            var channelNode = FeedXml.SelectSingleNode("/rss/channel");

            // Parse feed info.
            var feed = new FeedInfo()
            {
                Name = channelNode["title"]?.InnerText?.Trim(),
                WebsiteLink = channelNode["link"]?.InnerText?.Trim(),
                Description = channelNode["description"]?.InnerText?.Trim(),
                IconUri = channelNode.SelectSingleNode("/rss/channel/image")?["url"]?.InnerText?.Trim()
            };

            // Parse feed items.
            if (parseItems)
            {
                feed.FeedItems = new List<FeedItem>();
                var itemNodes = FeedXml.SelectNodes("/rss/channel/item");
                if (itemNodes != null)
                {
                    foreach (XmlNode itemNode in itemNodes)
                    {
                        feed.FeedItems.Add(ParseFeedItem(itemNode));
                    }
                }
                feed.TotalPosts = feed.FeedItems.Count;
            }
            return feed;
        }

        FeedItem ParseFeedItem(XmlNode xml)
        {
            var feedItem = new FeedItem()
            {
                Title = xml["title"]?.InnerText?.Trim(),
                Link = xml["link"]?.InnerText?.Trim(),
                PublishTime = xml["pubDate"]?.InnerText?.ToUtcDateTime() ?? default(DateTime),
            };

            // Get feed id.
            string id = xml["guid"]?.InnerText?.Trim();
            if (string.IsNullOrEmpty(id))
            {
                id = feedItem.Link;
            }
            feedItem.Id = id?.Md5HashToGuid() ?? Guid.Empty;

            // Get content
            feedItem.Content = xml["description"]?.InnerText?.Trim();
            if (string.IsNullOrEmpty(feedItem.Content))
            {
                feedItem.Content = xml.SelectSingleNode("content:encoded", FeedXmlNS)?.InnerText?.Trim();
            }

            // Try to find topic picture.
            feedItem.PictureUri = TryGetImageUrl(xml);

            // In some feeds, description doesn't contain picture, try to find content if it has.
            // Standard, rdf 1.0: http://purl.org/rss/1.0/modules/content/
            if (string.IsNullOrWhiteSpace(feedItem.PictureUri))
            {
                feedItem.PictureUri = TryGetImageUrl(xml.SelectSingleNode("content:encoded", FeedXmlNS)?.InnerText);
            }

            // have image node?
            if (string.IsNullOrEmpty(feedItem.PictureUri))
            {
                feedItem.PictureUri = xml.SelectSingleNode("image")?.InnerText?.Trim();
            }

            // have enclosure node?
            if (string.IsNullOrWhiteSpace(feedItem.PictureUri))
            {
                var enclosureNode = xml.SelectSingleNode("enclosure");
                if (enclosureNode != null)
                {
                    string imgType = enclosureNode.Attributes["type"]?.InnerText?.Trim();
                    if (imgType != null && imgType.StartsWith("image/"))
                    {
                        feedItem.PictureUri = enclosureNode.Attributes["url"]?.InnerText?.Trim();
                    }
                }
            }

            // Can we find the picture in the content?
            if (string.IsNullOrEmpty(feedItem.PictureUri))
            {
                feedItem.PictureUri = TryGetImageUrl(feedItem.Content);
            }

            // Get summary.
            feedItem.Summary = GetSummary(feedItem.Content);

            // Done.
            return feedItem;
        }
    }
}
