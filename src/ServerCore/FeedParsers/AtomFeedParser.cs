using FeedReader.ServerCore.Models;
using System.Collections.Generic;
using System.Xml;

namespace FeedReader.ServerCore.FeedParsers
{
    public class AtomFeedParser : XmlFeedParser
    {
        public AtomFeedParser(XmlDocument xml)
            : base(xml)
        {
            FeedXmlNS.AddNamespace("ns", "http://www.w3.org/2005/Atom");
        }

        public override FeedInfo TryParseFeed(bool parseItems)
        {
            // Parse feed. Spec: https://www.rfc-editor.org/rfc/rfc4287
            var feedNode = FeedXml.SelectSingleNode("/ns:feed", FeedXmlNS);

            // Parse feed info.
            var feed = new FeedInfo()
            {
                Name = feedNode["title"].InnerText.Trim(),
                WebsiteLink = GetLinkRef(feedNode),
                Description = feedNode["subtitle"]?.InnerText?.Trim(),
                IconUri = feedNode["icon"]?.InnerText?.Trim()
            };

            // If there is not website link, use the first author link.
            if (string.IsNullOrEmpty(feed.WebsiteLink))
            {
                feed.WebsiteLink = feedNode.SelectSingleNode("/ns:feed/ns:author/ns:uri", FeedXmlNS)?.InnerText?.Trim();
            }

            // Parse feed items.
            if (parseItems)
            {
                feed.FeedItems = new List<FeedItem>();
                var itemNodes = FeedXml.SelectNodes("/ns:feed/ns:entry", FeedXmlNS);
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

        private FeedItem ParseFeedItem(XmlNode xml)
        {
            var feedItem = new FeedItem()
            {
                Title = xml["title"].InnerText.Trim(),
                Link = GetLinkRef(xml),
                Id = xml["id"].InnerText.Trim().Md5HashToGuid(),
                PublishTime = xml["updated"].InnerText.ToUtcDateTime()
            };

            // Get content and summary.
            feedItem.Summary = GetSummary(xml["summary"]?.InnerText?.Trim());
            feedItem.Content = xml["content"]?.InnerText?.Trim();
            if (string.IsNullOrEmpty(feedItem.Content))
            {
                feedItem.Content = feedItem.Summary;
            }

            // Normalize summary.
            if (string.IsNullOrEmpty(feedItem.Summary))
            {
                feedItem.Summary = GetSummary(feedItem.Content);
            }

            // Try to find topic picture.
            string imgUrl = TryGetImageUrl(xml);
            if (string.IsNullOrWhiteSpace(imgUrl))
            {
                imgUrl = TryGetImageUrl(feedItem.Content);
            }

            // Save topic image.
            feedItem.PictureUri = imgUrl;

            // All done.
            return feedItem;
        }

        private string GetLinkRef(XmlNode node, string expectedRel = null)
        {
            expectedRel = expectedRel ?? "alternate";
            var links = node.SelectNodes("ns:link", FeedXmlNS);
            if (links != null)
            {
                foreach (XmlNode link in links)
                {
                    var attributes = link.Attributes;
                    var rel = attributes["rel"]?.InnerText;
                    if (rel == expectedRel || rel == null && expectedRel == "alternate")
                    {
                        return attributes["href"].InnerText.Trim();
                    }
                }
            }
            return null;
        }
    }
}
