using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace FeedReader.ServerCoreTest
{
    public delegate string SendHttpRequest(HttpRequestMessage request);

    public class MockHttpMessageHandler : HttpMessageHandler
    {
        public SendHttpRequest SendHttpRequest { get; set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(Send(request, cancellationToken));
        }

        protected override HttpResponseMessage Send(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return HttpResponseMessageFromString(SendHttpRequest(request));
        }

        private HttpResponseMessage HttpResponseMessageFromString(string content)
        {
            return new HttpResponseMessage()
            {
                StatusCode = HttpStatusCode.OK,
                Content = new StringContent(content),
            };
        }
    }

    public static class MockHttpClientFactory
    {
        public static HttpClient CreateMockHttpClient(SendHttpRequest s)
        {
            return new HttpClient(new MockHttpMessageHandler() { SendHttpRequest = s });
        }
    }
}
