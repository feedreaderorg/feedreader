using FeedReader.ServerCore.FeedParsers;
using Xunit;

namespace FeedReader.ServerCoreTest
{
    public class AtomFeedParserTest
    {
        [Fact]
        public void FeedItemParseTest()
        {
            var feedParser = FeedParser.TryCreateFeedParser(TestUtils.LoadTestData("YouTube.Channel.ChineseChessMasterClass.2023.01.03.xml"));
            var feed = feedParser.TryParseFeed(parseItems: true);

            Assert.NotNull(feed);
            Assert.Equal("象棋MasterClass", feed.Name);
            Assert.Equal("https://www.youtube.com/channel/UChvX1XzDIPLrs4mNWMRj0vw", feed.WebsiteLink);

            Assert.Equal(15, feed.FeedItems.Count);

            var feedItem = feed.FeedItems[0];
            Assert.Equal("狂炮打士！双人赛决战火花四溅！|| 洪智/党国蕾 vs 赵鑫鑫/时凤兰 ||", feedItem.Title);
            Assert.Equal("https://www.youtube.com/watch?v=fcqDx_JPteM", feedItem.Link);
            Assert.Equal("https://i3.ytimg.com/vi/fcqDx_JPteM/hqdefault.jpg", feedItem.PictureUri);
        }
    }
}
