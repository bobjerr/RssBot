namespace RssBot.Configuration
{
    public class AppSettings
    {
        public int TimeBetweenCheckingFeeds { get; set; }
        public int SubscriptionsInMessage { get; set; }

        public AppSettings()
        {
            SubscriptionsInMessage = 3;
            TimeBetweenCheckingFeeds = 60000;
        }
    }
}
