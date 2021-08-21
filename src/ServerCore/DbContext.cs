using Microsoft.EntityFrameworkCore;
using FeedReader.ServerCore.Models;
using System;

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

    public class DesignTimeFeedReaderDbFactory : Microsoft.EntityFrameworkCore.Design.IDesignTimeDbContextFactory<DbContext>
    {
        public DbContext CreateDbContext(string[] args)
        {
            var optsBuilder = new DbContextOptionsBuilder<DbContext>();
            optsBuilder.UseNpgsql("Server=localhost;Port=5432;Database=feedreader;User Id=feedreader;Password=feedreader");
            return new DbContext(optsBuilder.Options);
        }
    }
}