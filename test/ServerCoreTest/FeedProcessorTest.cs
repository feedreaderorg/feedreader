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
        public void GetIconFromWebSite()
        {
            var httpClient = MockHttpClientFactory.CreateMockHttpClient(r =>
            {
                Assert.Equal("https://blog.eltrovemo.com/", r.RequestUri.ToString());
                return LoadTestData("blog.eltrovemo.com.2022.01.23.html");
            });
            var feedProcessor = new FeedProcessor(httpClient);
            var data = LoadTestData("blog.eltrovemo.com.2022.01.23.xml");
            var feed = feedProcessor.TryToParseFeedFromContent(data, parseItems: false).Result;
            Assert.NotNull(feed);
            Assert.Equal("https://blog.eltrovemo.com/favicon.ico", feed.IconUri);
        }

        private string LoadTestData(string testFileName, [CallerFilePath] string callerFilePath = null)
        {
            var path = Path.GetDirectoryName(callerFilePath);
            path = Path.Combine(path, "TestData", testFileName);
            return File.ReadAllText(path, Encoding.UTF8);
        }
    }

    
}
