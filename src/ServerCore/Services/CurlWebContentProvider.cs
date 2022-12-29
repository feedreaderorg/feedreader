using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace FeedReader.ServerCore.Services
{
	internal class CurlWebContentProvider : IWebContentProvider
	{
		public Task<string> GetAsync(string uri)
		{
			return GetAsync(uri, headerOnly: false);
		}

		public Task<string> GetHeaderAsync(string uri)
		{
			return GetAsync(uri, headerOnly: true);
		}

		private async Task<string> GetAsync(string uri, bool headerOnly)
		{
			using var p = new Process();
			p.StartInfo.UseShellExecute = false;
			p.StartInfo.RedirectStandardError = true;
			p.StartInfo.RedirectStandardOutput = true;
			p.StartInfo.FileName = "curl";
			p.StartInfo.Arguments = $"-sSL{(headerOnly ? "I" : "")} -f -A \"Mozilla/5.0 (Windows NT 10.0; Win64; x64)\" \"{uri.ToString()}\"";
			p.Start();
			var content = await p.StandardOutput.ReadToEndAsync();
			var error = await p.StandardError.ReadToEndAsync();
			await p.WaitForExitAsync();
			if (p.ExitCode != 0)
			{
				throw new Exception(error);
			}
			return content;
		}
	}
}
