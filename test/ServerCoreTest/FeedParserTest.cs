using FeedReader.ServerCore.Processors;
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
    }
}
