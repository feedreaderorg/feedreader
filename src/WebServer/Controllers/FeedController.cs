﻿
using FeedReader.ServerCore.Services;
using FeedReader.Share.Protocols;
using Microsoft.AspNetCore.Mvc;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace FeedReader.WebServer.Controllers;

[Route("api/v{version:apiVersion}/feed")]
[ApiVersion("1.0")]
public class FeedController : ApiControllerBase
{
    private Validator Validator { get; } = new Validator();

    [Route("recommends")]
    [HttpGet]
    public async Task<IEnumerable<FeedItem>> GetRecommedFeedItems(FeedService feedService, int start = 0, int count = 50)
    {
        (start, count) = Validator.ValidateStartIndexAndCount(start, count);
        return (await feedService.GetFeedItemsByIdAsync(feedId: null, start, count)).Select(s => s.ToProtocolFeedItem());
    }

    [Route("discover")]
    [HttpGet]
    public async Task<IEnumerable<FeedInfo>> DiscoverFeeds(FeedService feedService, string query, int start = 0, int count = 50)
    {
        (start, count) = Validator.ValidateStartIndexAndCount(start, count);
        return (await feedService.DiscoverFeedsAsync(query, start, count)).Select(s => s.ToProtocolFeedInfo());
    }
}
