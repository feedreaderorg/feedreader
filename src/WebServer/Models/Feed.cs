﻿using Microsoft.EntityFrameworkCore;
using System;

namespace FeedReader.WebServer.Models
{
    [Index(nameof(SubscriptionName), IsUnique = true)]
    public class FeedInfo
    {
        public Guid Id { get; set; }

        public string SubscriptionName { get; set; }

        public string Uri { get; set; }

        public string IconUri { get; set; }

        public string Name { get; set; }

        public string Description { get; set; }

        public string WebsiteLink { get; set; }
    }
}