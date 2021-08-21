using FeedReader.ServerCore.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace FeedReader.ServerCore.Services
{
    public class FileService
    {
        IDbContextFactory<DbContext> DbFactory { get; set; }

        public FileService(IDbContextFactory<DbContext> dbFactory)
        {
            DbFactory = dbFactory;
        }

        public async Task<Guid> SaveFileAsync(byte[] content, string contentType)
        {
            // Caculate file md5 hash which will be treated as file id.
            var guid = new Guid(MD5.Create().ComputeHash(content));

            // Save to db.
            using (var db = DbFactory.CreateDbContext())
            {
                if (await db.Files.FindAsync(guid) == null)
                {
                    db.Files.Add(new File()
                    {
                        Id = guid,
                        Size = (uint)content.Length,
                        Content = content,
                        CreationTime = DateTime.UtcNow,
                        MimeType = contentType
                    });
                    await db.SaveChangesAsync();
                }
            }

            return guid;
        }

        public async Task<File> GetFileAsync(Guid fileId)
        {
            using (var db = DbFactory.CreateDbContext())
            {
                return await db.Files.FindAsync(fileId);
            }
        }
    }
}
