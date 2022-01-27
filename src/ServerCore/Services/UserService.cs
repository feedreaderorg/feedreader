using FeedReader.ServerCore.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FeedReader.ServerCore.Services
{
    public class UserService
    {
        const int PAGE_ITEMS_COUNT = 50;

        IDbContextFactory<DbContext> DbFactory { get; set; }

        public UserService(IDbContextFactory<DbContext> dbFactory)
        {
            DbFactory = dbFactory;
        }

        public async Task<User> GetUserProfile(Guid userId)
        {
            using (var db = await DbFactory.CreateDbContextAsync())
            {
                return await db.Users
                    .Include(u => u.SubscribedFeeds).ThenInclude(s => s.Feed)
                    .FirstOrDefaultAsync(u => u.Id == userId);
            }
        }

        public async Task<List<FeedItem>> GetFavoritesAsync(Guid userId, int page)
        {
            using (var db = DbFactory.CreateDbContext())
            {
                return await db.Favorites
                    .Include(f => f.FeedItem)
                    .Where(f => f.UserId == userId)
                    .Select(f => f.FeedItem)
                    .OrderByDescending(f => f.PublishTime)
                    .Skip(page * PAGE_ITEMS_COUNT)
                    .Take(PAGE_ITEMS_COUNT)
                    .ToListAsync();
            }
        }

        public async Task FavoriteFeedItemAsync(Guid userId, Guid feedItemId)
        {
            using (var db = DbFactory.CreateDbContext())
            {
                var favorite = await db.Favorites.FirstOrDefaultAsync(f => f.UserId == userId && f.FeedItemId == feedItemId);
                if (favorite == null)
                {
                    var feedId = await db.FeedItems.Where(f => f.Id == feedItemId).Select(f => f.FeedId).FirstOrDefaultAsync();
                    if (feedId == Guid.Empty)
                    {
                        throw new KeyNotFoundException();
                    }

                    db.Favorites.Add(new Favorite()
                    {
                        FeedItemId = feedItemId,
                        UserId = userId,
                        FeedInfoId = feedId
                    });
                    await db.SaveChangesAsync();
                }
            }
        }

        public async Task UnFavoriteFeedItemAsync(Guid userId, Guid feedItemId)
        {
            using (var db = DbFactory.CreateDbContext())
            {
                var favorite = await db.Favorites.FirstOrDefaultAsync(f => f.UserId == userId && f.FeedItemId == feedItemId);
                if (favorite != null)
                {
                    db.Favorites.Remove(favorite);
                    await db.SaveChangesAsync();
                }
            }
        }

        public async Task SubscribeFeed(Guid userId, Guid feedId)
        {
            using (var db = DbFactory.CreateDbContext())
            {
                if (await db.FeedSubscriptions.FindAsync(userId, feedId) == null)
                {
                    db.FeedSubscriptions.Add(new FeedSubscription()
                    {
                        UserId = userId,
                        FeedId = feedId,
                    });
                    await db.SaveChangesAsync();
                }
            }
        }

        public async Task UnsubscribeFeed(Guid userId, Guid feedId)
        {
            using (var db = DbFactory.CreateDbContext())
            {
                var item = await db.FeedSubscriptions.FindAsync(userId, feedId);
                if (item != null)
                {
                    db.FeedSubscriptions.Remove(item);
                    await db.SaveChangesAsync();
                }
            }
        }

        public async Task UpdateFeedSubscription(Guid userId, Guid feedId, DateTime? lastReadedTime)
        {
            using (var db = await DbFactory.CreateDbContextAsync())
            {
                var subscription = await db.FeedSubscriptions.Include(f => f.Feed).FirstOrDefaultAsync(s => s.UserId == userId && s.FeedId == feedId);
                if (subscription == null)
                {
                    throw new KeyNotFoundException();
                }

                if (lastReadedTime != null)
                {
                    subscription.LastReadedTime = lastReadedTime.Value;
                }

                await db.SaveChangesAsync();
            }
        }
    }
}
