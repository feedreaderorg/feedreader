using Microsoft.EntityFrameworkCore;
using System;

namespace FeedReader.MessageServer
{
    public class MessageServer
    {
        public Guid Id { get; set; }
        public string IpAddress { get; set; }
        public DateTime LastHeartbeatTime { get; set; }
    }

    public class DbContext : Microsoft.EntityFrameworkCore.DbContext
    {
        public DbContext(DbContextOptions<DbContext> options)
            : base(options)
        {
        }

        public DbSet<MessageServer> MessageServers { get; set; }
    }

    public class DesignTimeFeedReaderDbFactory : Microsoft.EntityFrameworkCore.Design.IDesignTimeDbContextFactory<DbContext>
    {
        public DbContext CreateDbContext(string[] args)
        {
            var optsBuilder = new DbContextOptionsBuilder<DbContext>();
            optsBuilder.UseNpgsql("Server=localhost;Port=5432;Database=feedreader-messageserver;User Id=feedreader;Password=feedreader");
            return new DbContext(optsBuilder.Options);
        }
    }

}
