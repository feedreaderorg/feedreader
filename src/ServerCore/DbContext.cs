using Microsoft.EntityFrameworkCore;
using FeedReader.ServerCore.Models;

namespace FeedReader.ServerCore
{
    public class DbContext : Microsoft.EntityFrameworkCore.DbContext
    {
        public DbContext(DbContextOptions<DbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<UserOAuthIds>().HasKey(u => new { u.OAuthIssuer, u.OAuthId });

            modelBuilder.Entity<FeedSubscription>().HasKey(f => new { f.UserId, f.FeedId });
        }

        public DbSet<File> Files { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<UserOAuthIds> UserOAuthIds { get; set; }
        public DbSet<FeedInfo> FeedInfos { get; set; }
        public DbSet<FeedSubscription> FeedSubscriptions { get; set; }
    }
}