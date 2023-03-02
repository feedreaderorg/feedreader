using FeedReader.ServerCore.FeedParsers;
using System;
using System.Linq;
using Xunit;

namespace FeedReader.ServerCoreTest
{
    public class FeedParserTest
    {
        [Fact]
        public void FeedItemParseTest()
        {
            var feedParser = FeedParser.TryCreateFeedParser(TestUtils.LoadTestData("rss.item.test.data.xml"));
            var feed = feedParser.TryParseFeed(parseItems: true);
            Assert.NotNull(feed);

            // Test all html tags are removed and html enitities are decoded.
            Assert.Equal("More convenience. C++20 Concurrency — Part 2: jthreads by Gajendra Gulgulia From the article: In this part of the issue, I’ll discuss about the new std::jthread that helps us avoid the boilerplate code for joining the conventional std::thread in the first section. In the end, I’ll also mention about the std::swap algorithm’s specialization introduced in C++20 to swap the underlying thread handles associated with std::jthread ...", feed.FeedItems[0].Summary);
        }

        [Fact]
        public void InvalidXmlCharacterCanBeIgnored()
		{
            var feedParser = FeedParser.TryCreateFeedParser(TestUtils.LoadTestData("coolshell.cn.2022.01.31.xml"));
            var feed = feedParser.TryParseFeed(parseItems: true);
            Assert.NotNull(feed);
            Assert.Equal("享受编程和技术所带来的快乐 - Coding Your Ambition", feed.Description);
            Assert.Equal(15, feed.FeedItems.Count);
            Assert.Equal("网络数字身份认证术", feed.FeedItems.First().Title);
            Assert.Equal("程序员如何把控自己的职业", feed.FeedItems.Last().Title);
		}

        [Fact]
        public void ParseItemWithoutPubDate()
        {
            var feedParser = FeedParser.TryCreateFeedParser(TestUtils.LoadTestData("ffmpeg.com.2022.02.03.xml"));
            var feed = feedParser.TryParseFeed(parseItems: true);
            Assert.NotNull(feed);
            Assert.Equal(default(DateTime), feed.LatestItemPublishTime);
            Assert.Equal(47, feed.FeedItems.Count);
            foreach (var item in feed.FeedItems)
            {
                Assert.Equal(default(DateTime), item.PublishTime);
            }
        }

        [Fact]
        public void ExtractTopicImageFromMediaContentTag()
        {
            var feedParser = FeedParser.TryCreateFeedParser(TestUtils.LoadTestData("moxie.foxnews.com.2022.12.28.xml"));
            var feed = feedParser.TryParseFeed(parseItems: true);
            Assert.NotNull(feed);
            Assert.Equal("https://a57.foxnews.com/static.foxnews.com/foxnews.com/content/uploads/2022/12/931/523/Whale.jpg?ve=1&tl=1", feed.FeedItems[0].PictureUri);
            Assert.Equal("https://www.foxnews.com/us/vanishing-north-atlantic-right-whale-remain-protected-endangered-species-act", feed.FeedItems[0].Link);
        }

        [Fact]
        public void ParseRssV1_0()
        {
            var feedParser = FeedParser.TryCreateFeedParser(TestUtils.LoadTestData("store.steampowered.com.2023.03.02.xml"));
            var feed = feedParser.TryParseFeed(parseItems: true);
            Assert.NotNull(feed);
            Assert.Equal("Steam RSS News Feed", feed.Name);
            Assert.Equal("http://www.steampowered.com/", feed.WebsiteLink);
            Assert.Equal("All Steam news, all the time!", feed.Description);
            Assert.Equal(20, feed.FeedItems.Count);
            Assert.Equal("Team Fortress 2 Update Released", feed.FeedItems[0].Title);
            Assert.Equal("https://store.steampowered.com/news/190467/", feed.FeedItems[0].Link);
            Assert.Equal(new DateTime(2023, 3, 2, 0, 34, 0, DateTimeKind.Utc), feed.FeedItems[0].PublishTime);
            Assert.Equal("An update to Team Fortress 2 has been released. The update will be applied automatically when you restart Team Fortress 2. The major changes include:<br/><br><ul style=\"padding-bottom: 0px; margin-bottom: 0px;\" ><li>Added missing Summer tag for Workshop maps<br></ul>", feed.FeedItems[0].Content);
            Assert.Equal("An update to Team Fortress 2 has been released. The update will be applied automatically when you restart Team Fortress 2. The major changes include:Added missing Summer tag for Workshop maps", feed.FeedItems[0].Summary);
        }
    }
}
