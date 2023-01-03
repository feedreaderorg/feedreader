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
    }
}
