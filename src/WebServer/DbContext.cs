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

        public DbSet<File> Files { get; set; }
    }
}
