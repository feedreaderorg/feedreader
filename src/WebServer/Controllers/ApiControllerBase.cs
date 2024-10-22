using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.OutputCaching;

namespace FeedReader.WebServer.Controllers;

[ApiController]
[ResponseCache(Location = ResponseCacheLocation.None, NoStore = true)]
public class ApiControllerBase : ControllerBase
{
}
