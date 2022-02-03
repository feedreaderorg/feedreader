using FeedReader.Share;
using System.Net;

namespace FeedReader.WebServer
{
    public class Validator
    {
        /// <summary>
        /// Validate & set the default value (if necessary) for startIndex and count.
        /// </summary>
        /// <param name="startIndex"></param>
        /// <param name="count"></param>
        /// <returns>Updated startIndex and count.</returns>
        /// <exception cref="FeedReaderException"></exception>
        public (int, int) ValidateStartIndexAndCount(int startIndex, int count)
        {
            if (count == 0)
            {
                count = 50;
            }

            if (startIndex < 0 || count < 1 || count > 50)
            {
                throw new FeedReaderException(HttpStatusCode.BadRequest);
            }

            return (startIndex, count);
        }
    }
}
