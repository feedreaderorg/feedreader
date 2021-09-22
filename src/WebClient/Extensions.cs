using System;

namespace FeedReader.WebClient
{
    public static class Extensions
    {
        public static int DescCompareTo(this DateTime d1, DateTime d2)
        {
            if (d1 == d2)
            {
                return 0;
            }
            else if (d1 < d2)
            {
                return 1;
            }
            else
            {
                return -1;
            }

        }
    }
}
