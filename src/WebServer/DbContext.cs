using Microsoft.EntityFrameworkCore;
using FeedReader.WebServer.Models;

namespace FeedReader.WebServer
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
        }

        public DbSet<File> Files { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<UserOAuthIds> UserOAuthIds { get; set; }
    }
}