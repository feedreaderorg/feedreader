using FeedReader.ServerCore.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace FeedReader.ServerCore.Services
{
    public class UserService
    {
        IDbContextFactory<DbContext> DbFactory { get; set; }
        ConcurrentDictionary<Guid, Action<User>> UserEventCallbacks { get; set; } = new ConcurrentDictionary<Guid, Action<User>>();

        public UserService(IDbContextFactory<DbContext> dbFactory)
        {
            DbFactory = dbFactory;
        }

        public async Task SubscribeFeed(Guid userId, Guid feedId)
        {
            using (var db = DbFactory.CreateDbContext())
            {
                if (await db.FeedSubscriptions.FindAsync(userId, feedId) == null)
                {
                    db.FeedSubscriptions.Add(new Models.FeedSubscription()
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

        public async void SubscribeUserEvent(Guid userId, Action<User> updatedCallback)
        {
            UserEventCallbacks.AddOrUpdate(userId, updatedCallback, (k, u) => updatedCallback);

            using (var db = DbFactory.CreateDbContext())
            {
                var user = await db.Users.Include(u => u.SubscribedFeeds).ThenInclude(f => f.Feed).FirstOrDefaultAsync(u => u.Id == userId);
                updatedCallback(user);
            }
        }

        public void UnsubscribeUserEvent(Guid userId)
        {
            Action<User> callback;
            UserEventCallbacks.Remove(userId, out callback);
        }
    }
}
