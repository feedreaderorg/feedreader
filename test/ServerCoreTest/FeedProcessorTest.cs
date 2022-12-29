using FeedReader.ServerCore.Processors;
using FeedReader.ServerCore.Services;
using Moq;
using Xunit;

namespace FeedReader.ServerCoreTest
{
    public class FeedProcessorTest
    {
        [Fact]
        public void GetIconFromWebSiteHtmlShortcutTag()
        {
            var mockWebContentClient = new Mock<IWebContentProvider>();
            mockWebContentClient.Setup(c => c.GetAsync("https://blog.eltrovemo.com/")).ReturnsAsync(TestUtils.LoadTestData("blog.eltrovemo.com.2022.01.23.html"));

            var feedProcessor = new FeedProcessor(mockWebContentClient.Object);
            var data = TestUtils.LoadTestData("blog.eltrovemo.com.2022.01.23.xml");
            var feed = feedProcessor.TryToParseFeedFromContent(data, parseItems: false).Result;
            Assert.NotNull(feed);
            Assert.Equal("https://blog.eltrovemo.com/favicon.ico", feed.IconUri);
        }

        [Fact]
        public void GetIconFromWebSitePredefinedPath()
        {
			var mockWebContentClient = new Mock<IWebContentProvider>();
            mockWebContentClient.Setup(c => c.GetAsync("https://coolshell.cn/")).ReturnsAsync(TestUtils.LoadTestData("coolshell.cn.2022.02.02.html"));
            mockWebContentClient.Setup(c => c.GetHeaderAsync("https://coolshell.cn/favicon.ico")).ReturnsAsync("HTTP/1.1 200 OK");
			
            var feedProcessor = new FeedProcessor(mockWebContentClient.Object);
            var data = TestUtils.LoadTestData("coolshell.cn.2022.01.31.xml");
            var feed = feedProcessor.TryToParseFeedFromContent(data, parseItems: false).Result;
            Assert.NotNull(feed);
            Assert.Equal("https://coolshell.cn/favicon.ico", feed.IconUri);
        }

        [Fact]
        public void GetIconUriWithRelativePathAndQueryParameterFromWebSite()
        {
			// For issue: https://github.com/xieyubo/FeedReader/issues/16
			var mockWebContentClient = new Mock<IWebContentProvider>();
			mockWebContentClient.Setup(c => c.GetAsync("https://www.insider.com/")).ReturnsAsync(TestUtils.LoadTestData("www.insider.com.2022.12.27.html"));

            var feedProcessor = new FeedProcessor(mockWebContentClient.Object);
            var data = TestUtils.LoadTestData("www.insider.com.2022.12.27.xml");
            var feed = feedProcessor.TryToParseFeedFromContent(data, parseItems: false).Result;
            Assert.NotNull(feed);
            Assert.Equal("https://www.insider.com/public/assets/INSIDER/US/favicons/apple-touch-icon.png?v=2021-08", feed.IconUri);
        }
    }

    
}
