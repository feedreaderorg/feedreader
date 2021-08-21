using Microsoft.EntityFrameworkCore;
using System;
using System.Threading.Tasks;

namespace FeedReader.ServerCore.Services
{
    public class UserService
    {
        IDbContextFactory<DbContext> DbFactory { get; set; }

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
    }
}
