using System;
using System.Net;

namespace FeedReader.Share
{
    public class FeedReaderException : Exception
    {
        public HttpStatusCode StatusCode { get; }

        public FeedReaderException(HttpStatusCode statusCode)
        {
            StatusCode = statusCode;
        }
    }
}
