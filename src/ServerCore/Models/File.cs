using System;

namespace FeedReader.ServerCore.Models
{
    public class File
    {
        public Guid Id { get; set; }
        public DateTime CreationTime { get; set; }
        public string MimeType { get; set; }
        public uint Size { get; set; }
        public byte[] Content { get; set; }
    }
}
