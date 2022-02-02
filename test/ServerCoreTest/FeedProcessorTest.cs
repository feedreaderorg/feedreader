using FeedReader.ServerCore.Processors;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Xunit;

namespace FeedReader.ServerCoreTest
{
    public class FeedProcessorTest
    {
        [Fact]
        public void GetIconFromWebSiteHtmlShortcutTag()
        {
            var httpClient = MockHttpClientFactory.CreateMockHttpClient(r =>
            {
                Assert.Equal("https://blog.eltrovemo.com/", r.RequestUri.ToString());
                return TestUtils.LoadTestData("blog.eltrovemo.com.2022.01.23.html");
            });
            var feedProcessor = new FeedProcessor(httpClient);
            var data = TestUtils.LoadTestData("blog.eltrovemo.com.2022.01.23.xml");
            var feed = feedProcessor.TryToParseFeedFromContent(data, parseItems: false).Result;
            Assert.NotNull(feed);
            Assert.Equal("https://blog.eltrovemo.com/favicon.ico", feed.IconUri);
        }

        [Fact]
        public void GetIconFromWebSitePredefinedPath()
        {
            var httpClient = MockHttpClientFactory.CreateMockHttpClient(r =>
            {
                var path = r.RequestUri.ToString();
                if (path == "https://coolshell.cn/")
                {
                    return TestUtils.LoadTestData("coolshell.cn.2022.02.02.html");
                }
                else if (path == "https://coolshell.cn/favicon.ico")
                {
                    return "";
                }
                else
                {
                    Assert.True(false);
                    return "";
                }
            });

            var feedProcessor = new FeedProcessor(httpClient);
            var data = TestUtils.LoadTestData("coolshell.cn.2022.01.31.xml");
            var feed = feedProcessor.TryToParseFeedFromContent(data, parseItems: false).Result;
            Assert.NotNull(feed);
            Assert.Equal("https://coolshell.cn:443/favicon.ico", feed.IconUri);
        }
    }

    
}
