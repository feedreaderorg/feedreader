using FeedReader.ServerCore.Models;
using FeedReader.ServerCore.Processors;
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

namespace FeedReader.ServerCore.Services
{
    public class FeedService
    {
        IDbContextFactory<DbContext> DbFactory { get; set; }
        HttpClient HttpClient { get; set; }
        ILogger Logger { get; set; }
        string TextClassificationServerAddress { get; }
        int TextClassificationBatchSize { get; }
        string[] TextClassificationLabels { get; }
        private FeedProcessor FeedProcessor { get; }

        public FeedService(IDbContextFactory<DbContext> dbFactory, HttpClient httpClient, ILogger<FeedService> logger, IConfiguration configuration)
        {
            DbFactory = dbFactory;
            HttpClient = httpClient;
            Logger = logger;
            TextClassificationServerAddress = configuration["TextClassificationServer"];
            TextClassificationBatchSize = int.Parse(configuration["TextClassificationBatchSize"] ?? "0");
            TextClassificationLabels = Enum.GetValues(typeof(FeedItemCategories)).Cast<FeedItemCategories>().Select(e => e.ToString().ToLower()).ToArray();
            FeedProcessor = new FeedProcessor(HttpClient);
        }

        public async Task<List<FeedInfo>> DiscoverFeedsAsync(string query)
        {
            var feeds = new List<FeedInfo>();

            // Normalize query string.
            query = query.Trim().TrimEnd('/');
            if (string.IsNullOrEmpty(query))
            {
                return feeds;
            }

            // Treat it as uri.
            Uri uri;
            if (Uri.TryCreate(query, UriKind.Absolute, out uri))
            {
                // Try to get from https if this is http.
                var httpsUri = uri.GetHttpsVersion();
                await TryToDiscoverFeedsFromUriAsync(httpsUri, feeds);
                if (feeds.Count > 0)
                {
                    return feeds;
                }

                if (httpsUri != uri)
                {
                    // Ok, try uri directly.
                    await TryToDiscoverFeedsFromUriAsync(uri, feeds);
                }
                return feeds;
            }
            else
            {
                // Search in name and description.
                using (var db = await DbFactory.CreateDbContextAsync())
                {
                    feeds = db.FeedInfos.Where(f => f.SearchVector.Matches(query)).ToList();
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

        public async Task<FeedInfo> GetFeedInfoById(Guid feedId)
        {
            using (var db = await DbFactory.CreateDbContextAsync())
            {
                return await db.FeedInfos.FindAsync(feedId);
            }
        }

        public async Task<FeedInfo> GetFeedInfoBySubscriptionName(string subscriptionName)
        {
            using (var db = await DbFactory.CreateDbContextAsync())
            {
                return await db.FeedInfos.FirstOrDefaultAsync(f => f.SubscriptionName == subscriptionName);
            }
        }

        public async Task<List<FeedItem>> GetFeedItemsByIdAsync(Guid feedId, int startIndex, int count)
        {
            using (var db = DbFactory.CreateDbContext())
            {
                return await db.FeedItems
                    .Where(f => f.FeedId == feedId)
                    .OrderByDescending(f => f.PublishTime)
                    .Skip(startIndex)
                    .Take(count).ToListAsync();
            }
        }

        public async Task<List<FeedItem>> GetFeedItemsByCategoryAsync(string category, int startIndex, int count)
        {
            using (var db = DbFactory.CreateDbContext())
            {
                return await db.FeedItems
                    .Where(f => f.Category == category)
                    .OrderByDescending(f => f.PublishTime)
                    .Skip(startIndex)
                    .Take(count).ToListAsync();
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

            var feed = await FeedProcessor.TryToParseFeedFromContent(content, parseItems: false);
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
                            _ = RefreshFeedsAsync(feed.Id, default(CancellationToken));
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

        

        Task TryToDiscoverFeedsFromHtmlAsync(string content, List<FeedInfo> feeds)
        {
            return Task.CompletedTask;
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

                var parsedFeed = await FeedProcessor.TryToParseFeedFromContent(content, parseItems: true, cancellationToken);
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