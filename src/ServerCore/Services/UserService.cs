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
        ConcurrentDictionary<Guid, List<Session>> UserEventCallbacks { get; set; } = new ConcurrentDictionary<Guid, List<Session>>();

        public UserService(IDbContextFactory<DbContext> dbFactory)
        {
            DbFactory = dbFactory;
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
                    db.FeedSubscriptions.Add(new Models.FeedSubscription()
                    {
                        UserId = userId,
                        FeedId = feedId,
                    });
                    await db.SaveChangesAsync();
                    UserStateUpdated(userId);
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
                    UserStateUpdated(userId);
                }
            }
        }

        public void SubscribeUserEvent(Guid userId, int sessionId, Action<User> updatedCallback)
        {
            var sessions = UserEventCallbacks.GetOrAdd(userId, new List<Session>());
            lock(sessions)
            {
                sessions.Add(new Session()
                {
                    Id = sessionId,
                    UserUpdateCallback = updatedCallback
                });
            }
            UserStateUpdated(userId);
        }

        public void UnsubscribeUserEvent(Guid userId, int sessionId)
        {
            // TODO: Unsubscribe should only unsubscribe one session.
            List<Session> sessions;
            if (UserEventCallbacks.TryGetValue(userId, out sessions))
            {
                lock (sessions)
                {
                    var session = sessions.Find(s => s.Id == sessionId);
                    if (session != null)
                    {
                        sessions.Remove(session);
                        if (sessions.Count() == 0)
                        {
                            List<Session> tmpSessions;
                            UserEventCallbacks.Remove(userId, out tmpSessions);
                        }
                    }
                }
            }
        }

        async void UserStateUpdated(Guid userId)
        {
            List<Session> sessions;
            if (UserEventCallbacks.TryGetValue(userId, out sessions))
            {
                Action<User>[] callbacks;
                lock (sessions)
                {
                    callbacks = sessions.Select(s => s.UserUpdateCallback).ToArray();
                }

                using (var db = DbFactory.CreateDbContext())
                {
                    var user = await db.Users.Include(u => u.SubscribedFeeds).ThenInclude(f => f.Feed).FirstOrDefaultAsync(u => u.Id == userId);
                    foreach (var callback in callbacks)
                    {
                        try
                        {
                            callback(user);
                        }
                        catch
                        {
                        }
                    }
                }
            }        
        }

        class Session
        {
            public int Id { get; set; }
            public Action<User> UserUpdateCallback { get; set; }
        }
    }
}
