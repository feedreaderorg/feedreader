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

            modelBuilder.Entity<Favorite>().HasKey(f => new { f.UserId, f.FeedItemId });

            modelBuilder.Entity<FeedSubscription>().HasKey(f => new { f.UserId, f.FeedId });

            modelBuilder.Entity<FeedInfo>().HasGeneratedTsVectorColumn(
                    p => p.SearchVector,
                    "english",
                    p => new { p.Name, p.SubscriptionName, p.Description }
                )
                .HasIndex(p => p.SearchVector)
                .HasMethod("GIN");
        }

        public DbSet<File> Files { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<UserOAuthIds> UserOAuthIds { get; set; }
        public DbSet<FeedInfo> FeedInfos { get; set; }
        public DbSet<FeedSubscription> FeedSubscriptions { get; set; }
        public DbSet<FeedItem> FeedItems { get; set; }
        public DbSet<Favorite> Favorites { get; set; }
    }

    public class DesignTimeFeedReaderDbFactory : Microsoft.EntityFrameworkCore.Design.IDesignTimeDbContextFactory<DbContext>
    {
        public DbContext CreateDbContext(string[] args)
        {
            string connectionString = "Server=localhost;Port=5432;Database=feedreader;User Id=feedreader;Password=feedreader";
            if (args != null)
            {
                for (int i = 0; i < args.Length; ++i)
                {
                    if (args[i] == "--conns")
                    {
                        connectionString = args[++i];
                    }
                }
            }

            var optsBuilder = new DbContextOptionsBuilder<DbContext>();
            optsBuilder.UseNpgsql(connectionString);
            return new DbContext(optsBuilder.Options);
        }
    }
}