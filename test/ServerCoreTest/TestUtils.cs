using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

namespace FeedReader.ServerCoreTest
{
    public static class TestUtils
    {
        public static string LoadTestData(string testFileName)
        {
            return DoLoadTestData(testFileName);
        }

        private static string DoLoadTestData(string testFileName,  [CallerFilePath] string callerFilePath = null)
        {
            var path = Path.GetDirectoryName(callerFilePath);
            path = Path.Combine(path, "TestData", testFileName);
            return File.ReadAllText(path, Encoding.UTF8);
        }
    }
}
