using System.Collections.Generic;

namespace RssBot.Data
{
    class RssFeed
    {
        public string Url { get; set; }
        public string ShortId { get; set; }

        public List<Subscription> Subscriptions { get; set; } = new List<Subscription>();
    }
}