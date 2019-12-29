namespace RssBot.Data
{
    class Subscription
    {
        public long UserId { get; set; }
        public string ShortId { get; set; }
        public RssFeed Feed { get; set; }
    }
}