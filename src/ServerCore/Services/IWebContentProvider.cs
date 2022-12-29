using System.Threading.Tasks;

namespace FeedReader.ServerCore.Services
{
    public interface IWebContentProvider
	{
		public Task<string> GetAsync(string uri);
		public Task<string> GetHeaderAsync(string uri);
	}
}
