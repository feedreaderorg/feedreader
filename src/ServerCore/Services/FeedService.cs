using FeedReader.ServerCore.Models;
using FeedReader.ServerCore.Processors;
using HtmlAgilityPack;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;

namespace FeedReader.ServerCore.Services
{
    public class FeedService
    {
        const int PAGE_ITEMS_COUNT = 50;

        IDbContextFactory<DbContext> DbFactory { get; set; }
        HttpClient HttpClient { get; set; }
        ILogger Logger { get; set; }
        string TextClassificationServerAddress { get; }
        int TextClassificationBatchSize { get; }
        string[] TextClassificationLabels { get; }

        public FeedService(IDbContextFactory<DbContext> dbFactory, HttpClient httpClient, ILogger<FeedService> logger, IConfiguration configuration)
        {
            DbFactory = dbFactory;
            HttpClient = httpClient;
            Logger = logger;
            TextClassificationServerAddress = configuration["TextClassificationServer"];
            TextClassificationBatchSize = int.Parse(configuration["TextClassificationBatchSize"] ?? "0");
            TextClassificationLabels = Enum.GetValues(typeof(FeedItemCategories)).Cast<FeedItemCategories>().Select(e => e.ToString().ToLower()).ToArray();
        }

        public async Task<List<FeedInfo>> DiscoverFeedsAsync(string query)
        {
            var feeds = new List<FeedInfo>();

            // Normalize query string.
            query = query.Trim().TrimEnd('/');

            // Treat it as uri.
            Uri uri;
            if (Uri.TryCreate(query, UriKind.Absolute, out uri))
            {
                // Try to get from https if this is http.
                var httpsUri = GetHttpsUri(uri);
                if (httpsUri != null)
                {
                    await TryToDiscoverFeedsFromUriAsync(httpsUri, feeds);
                    if (feeds.Count > 0)
                    {
                        return feeds;
                    }
                }

                if (httpsUri != uri)
                {
                    // Ok, try uri directly.
                    await TryToDiscoverFeedsFromUriAsync(uri, feeds);
                }
            }

            return feeds;
        }

        public async Task RefreshFeedsAsync(CancellationToken cancellationToken)
        {
            Guid[] feedIds;
            using (var db = DbFactory.CreateDbContext())
            {
                feedIds = await db.FeedInfos.Select(f => f.Id).ToArrayAsync(cancellationToken);
            }

            foreach (var feedId in feedIds)
            {
                if (cancellationToken.IsCancellationRequested)
                {
                    throw new OperationCanceledException();
                }

                await RefreshFeedsAsync(feedId, cancellationToken);
            }
        }

        public async Task<FeedInfo> GetFeedInfoAsync(Guid feedId)
        {
            using (var db = DbFactory.CreateDbContext())
            {
                return await db.FeedInfos.FindAsync(feedId);
            }
        }

        public async Task<List<FeedItem>> GetFeedItemsByIdAsync(Guid feedId, int page)
        {
            using (var db = DbFactory.CreateDbContext())
            {
                return await db.FeedItems
                    .Where(f => f.FeedId == feedId)
                    .OrderByDescending(f => f.PublishTime)
                    .Skip(page * PAGE_ITEMS_COUNT)
                    .Take(PAGE_ITEMS_COUNT).ToListAsync();
            }
        }

        public async Task<List<FeedItem>> GetFeedItemsByCategoryAsync(string category, int page)
        {
            using (var db = DbFactory.CreateDbContext())
            {
                return await db.FeedItems
                    .Where(f => f.Category == category)
                    .OrderByDescending(f => f.PublishTime)
                    .Skip(page * PAGE_ITEMS_COUNT)
                    .Take(PAGE_ITEMS_COUNT).ToListAsync();
            }
        }

        public async Task ClassifyFeedItemsAsync(CancellationToken cancellationToken)
        {
            bool classifySuccess = true;

            while (classifySuccess && !cancellationToken.IsCancellationRequested)
            {
                using (var db = DbFactory.CreateDbContext())
                {
                    classifySuccess = false;

                    foreach (var feedItem in db.FeedItems.Where(f => string.IsNullOrEmpty(f.Category)).OrderByDescending(f => f.PublishTime).Take(TextClassificationBatchSize))
                    {
                        if (cancellationToken.IsCancellationRequested)
                        {
                            break;
                        }

                        try
                        {
                            var contentForTextClassification = string.IsNullOrEmpty(feedItem.Content) ? feedItem.Summary : feedItem.Content;
                            if (string.IsNullOrEmpty(contentForTextClassification))
                            {
                                // TODO: feed item has neither content nor summary. Because parser bug? Log it ...
                                Logger.LogWarning($"Feed item {feedItem.Id} has neither content nor summary, ignore for classification.");
                                feedItem.Category = "_NOT_AVAILABLE_";
                            }
                            else
                            {
                                var content = new StringContent(JsonConvert.SerializeObject(new
                                {
                                    content = contentForTextClassification,
                                    labels = TextClassificationLabels
                                }), UnicodeEncoding.UTF8, "application/json");

                                Logger.LogInformation($"Classify for feed item {feedItem.Id}");
                                var res = await HttpClient.PostAsync(TextClassificationServerAddress, content, cancellationToken);
                                var result = JsonConvert.DeserializeObject<TextClassificationServerResult>(await res.Content.ReadAsStringAsync(cancellationToken));
                                var category = result.Labels[Array.IndexOf(result.Scores, result.Scores.Max())];
                                feedItem.Category = category;
                            }

                            classifySuccess = true;
                        }
                        catch (TaskCanceledException)
                        {
                            break;
                        }
                        catch (Exception ex)
                        {
                            Logger.LogError(ex, $"Classify for feed item {feedItem.Id} failed");
                        }
                    }

                    if (classifySuccess && !cancellationToken.IsCancellationRequested)
                    {
                        Logger.LogInformation($"Classify batch finished, update database");
                        await db.SaveChangesAsync(cancellationToken);
                    }
                }
            }
        }

        public async Task RefreshFeedItemStatisticsAsync(CancellationToken cancellationToken)
        {
            try
            {
                const int PAGE_COUNT = 100;
                int page = 0;

                while (!cancellationToken.IsCancellationRequested)
                {
                    using (var db = DbFactory.CreateDbContext())
                    {
                        int actualCount = 0;

                        foreach (var feedItemId in db.FeedItems.Select(f => f.Id).Skip(page * PAGE_COUNT).Take(PAGE_COUNT).ToArray())
                        {
                            ++actualCount;

                            var totalFavorites = await db.Favorites.CountAsync(f => f.FeedItemId == feedItemId);
                            db.UpdateEntity(() => new FeedItem()
                            {
                                Id = feedItemId,
                                TotalFavorites = totalFavorites
                            });
                        }

                        await db.SaveChangesAsync(cancellationToken);
                        Logger.LogInformation($"RefreshFeedItemStatisticsAsync page: {page}, actualCount: {actualCount}.");

                        if (actualCount < PAGE_COUNT)
                        {
                            break;
                        }

                        ++page;
                    }
                }
            }
            catch (TaskCanceledException)
            {
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, $"RefreshFeedItemStatisticsAsync failed.");
            }
        }

        async Task TryToDiscoverFeedsFromUriAsync(Uri uri, List<FeedInfo> feeds)
        {
            var idFromUri = new Guid(MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(uri.ToString().ToLower())));

            // Try to find from idFromUri.
            using (var db = DbFactory.CreateDbContext())
            {
                var feedInDb = await db.FeedInfos.FirstOrDefaultAsync(f => f.IdFromUri == idFromUri);
                if (feedInDb != null)
                {
                    feeds.Add(feedInDb);
                    return;
                }
            }

            // Get the content from the uri.
            string content = null;
            try
            {
                content = await HttpClient.GetStringAsync(uri);
            }
            catch
            {
            }
            if (string.IsNullOrEmpty(content))
            {
                return;
            }

            var feed = await TryToParseFeedFromContentAsync(content);
            if (feed != null)
            {
                feed.Id = Guid.NewGuid();
                feed.IdFromUri = idFromUri;
                feed.RegistrationTime = DateTime.UtcNow;
                feed.Uri = uri.ToString();

                // Save to db.
                using (var db = DbFactory.CreateDbContext())
                {
                    if (await db.FeedInfos.FirstOrDefaultAsync(f => f.IdFromUri == idFromUri) == null)
                    {
                        bool findUnusedSubscriptionName = false;
                        for (var i = 0; i < 10; ++i)
                        {
                            feed.SubscriptionName = GenerateFeedSubscriptionName(feed, feed.SubscriptionName);
                            if (await db.FeedInfos.FirstOrDefaultAsync(f => f.SubscriptionName == feed.SubscriptionName) == null)
                            {
                                findUnusedSubscriptionName = true;
                                break;
                            }
                        }
                        if (findUnusedSubscriptionName)
                        {
                            db.FeedInfos.Add(feed);
                            await db.SaveChangesAsync();
                        }
                        else
                        {
                            throw new Exception("Failed to generate unused subscription name");
                        }
                    }
                }

                // Return
                feeds.Add(feed);
                return;
            }

            // Try to discover feed from html content directly.
            await TryToDiscoverFeedsFromHtmlAsync(content, feeds);
        }

        string GenerateFeedSubscriptionName(FeedInfo feed, string lastProposedName)
        {
            var proposedName = lastProposedName;
            if (!string.IsNullOrEmpty(proposedName))
            {
                var lastHyphen = proposedName.LastIndexOf("-0");
                if (lastHyphen > -1)
                {
                    proposedName.Substring(0, lastHyphen);
                }
                return $"{proposedName}-{new Random().Next(1000)}";
            }
            else
            {
                proposedName = "";
                var uri = new Uri(feed.Uri);
                var domainsPart = uri.Host.ToLower().Split('.');
                for (var i = 0; i < domainsPart.Length - 1; ++i)
                {
                    var str = new string(domainsPart[i].Where(c => char.IsLetterOrDigit(c)).ToArray());
                    if (str == "www")
                    {
                        continue;
                    }
                    else
                    {
                        if (proposedName == "")
                        {
                            proposedName = str;
                        }
                        else
                        {
                            proposedName = $"{proposedName}-{str}";
                        }
                    }
                }
                if (proposedName == "")
                {
                    proposedName = new Random().Next(1000).ToString();
                }
                return proposedName;
            }
        }

        async Task<FeedInfo> TryToParseFeedFromContentAsync(string content, CancellationToken cancellationToken = default(CancellationToken))
        {
            var feed = FeedParser.TryCreateFeedParser(content)?.TryParseFeed();
            if (feed != null)
            {
                if (string.IsNullOrEmpty(feed.IconUri))
                {
                    Uri websiteUri;
                    if (Uri.TryCreate(feed.WebsiteLink, UriKind.Absolute, out websiteUri))
                    {
                        var httpsUri = GetHttpsUri(websiteUri);
                        if (httpsUri != null)
                        {
                            feed.IconUri = await TryToGetIconUriFromWebsiteUriAsync(httpsUri, cancellationToken);
                        }

                        if (string.IsNullOrEmpty(feed.IconUri) && httpsUri != websiteUri)
                        {
                            feed.IconUri = await TryToGetIconUriFromWebsiteUriAsync(websiteUri, cancellationToken);
                        }
                    }
                }
            }
            return feed;
        }

        Task TryToDiscoverFeedsFromHtmlAsync(string content, List<FeedInfo> feeds)
        {
            return Task.CompletedTask;
        }

        async Task<string> TryToGetIconUriFromWebsiteUriAsync(Uri uri, CancellationToken cancellationToken)
        {
            try
            {
                var link = uri.ToString();
                var web = new HtmlWeb();
                var html = await web.LoadFromWebAsync(link, cancellationToken);
                var el = html.DocumentNode.SelectSingleNode("/html/head/link[@rel='apple-touch-icon' and @href]") ??
                         html.DocumentNode.SelectSingleNode("/html/head/link[@rel='shortcut icon' and @href]") ??
                         html.DocumentNode.SelectSingleNode("/html/head/link[@rel='icon' and @href]");
                if (el != null)
                {
                    var uriBuilder = new UriBuilder(link);
                    uriBuilder.Path = el.Attributes["href"].Value;
                    return uriBuilder.ToString();
                }
            }
            catch
            {
            }
            return null;
        }

        Uri GetHttpsUri(Uri uri)
        {
            if (uri.Scheme == Uri.UriSchemeHttps)
            {
                return uri;
            }
            else if (uri.Scheme == Uri.UriSchemeHttp && uri.IsDefaultPort)
            {
                var ub = new UriBuilder(uri);
                ub.Scheme = Uri.UriSchemeHttps;
                ub.Port = 443;
                return ub.Uri;
            }
            else
            {
                return null;
            }
        }

        async Task RefreshFeedsAsync(Guid feedId, CancellationToken cancellationToken)
        {
            using (var db = DbFactory.CreateDbContext())
            {
                var feed = await db.FeedInfos.FindAsync(new object[] { feedId }, cancellationToken);
                if (feed == null)
                {
                    return;
                }

                DateTime starTime = DateTime.Now;
                Logger.LogInformation($"Refresh feed: {feed.Uri} at {starTime}");

                var content = await HttpClient.GetStringAsync(feed.Uri, cancellationToken);
                if (string.IsNullOrEmpty(content))
                {
                    feed.LastParseError = "Fetch content failed.";
                    return;
                }

                var parsedFeed = await TryToParseFeedFromContentAsync(content, cancellationToken);
                if (parsedFeed == null)
                {
                    feed.LastParseError = "Parse feed failed.";
                }

                // Update feed information.
                feed.Description = parsedFeed.Description;
                feed.IconUri = parsedFeed.IconUri;
                feed.Name = parsedFeed.Name;
                feed.WebsiteLink = parsedFeed.WebsiteLink;
                feed.LastUpdatedTime = DateTime.UtcNow;

                // Update feed items.
                foreach (var parsedFeedItem in parsedFeed.FeedItems.Where(f => f.Id != Guid.Empty).OrderByDescending(f => f.PublishTime).GroupBy(f => f.Id).Select(g => g.First()))
                {
                    parsedFeedItem.Id = $"{feed.Id}-{parsedFeedItem.Id}".Md5HashToGuid();
                    parsedFeedItem.FeedId = feed.Id;
                    var feedItem = await db.FeedItems.FindAsync(parsedFeedItem.Id);
                    if (feedItem != null)
                    {
                        // Update feedItemInDb
                        feedItem.Content = parsedFeedItem.Content;
                        feedItem.Link = parsedFeedItem.Link;
                        feedItem.PictureUri = parsedFeedItem.PictureUri;
                        feedItem.PublishTime = parsedFeedItem.PublishTime;
                        feedItem.Summary = parsedFeedItem.Summary;
                        feedItem.Title = parsedFeedItem.Title;
                    }
                    else
                    {
                        db.FeedItems.Add(parsedFeedItem);
                    }
                }

                // Update feed stat.
                feed.TotalFavorites = await db.Favorites.CountAsync(f => f.FeedInfoId == feed.Id);
                feed.TotalSubscribers = await db.FeedSubscriptions.Where(f => f.FeedId == feed.Id).CountAsync(cancellationToken);
                feed.TotalPosts = await db.FeedItems.Where(f => f.FeedId == feed.Id).CountAsync();
                
                // Save to db.
                await db.SaveChangesAsync(cancellationToken);
                
                DateTime endTime = DateTime.Now;
                Logger.LogInformation($"Refresh feed: {feed.Uri} finished at {endTime}, elasped {(endTime - starTime).TotalSeconds} seconds");
            }
        }

        #region TextClassificationServerResult
        class TextClassificationServerResult
        {
            public string[] Labels { get; set; }
            public float[] Scores { get; set; }
        }
        #endregion
    }
}