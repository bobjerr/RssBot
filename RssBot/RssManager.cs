using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RssBot.Data;
using shortid;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.ServiceModel.Syndication;
using System.Threading.Tasks;
using System.Xml;

namespace RssBot
{
    public class RssManager
    {
        private const int _idLength = 10;

        private DateTime _lastCheck;
        private IServiceScopeFactory _serviceScopeFactory;

        public RssManager(IServiceScopeFactory serviceScopeFactory)
        {
            _lastCheck = DateTime.Now;
            _serviceScopeFactory = serviceScopeFactory;
        }

        public async Task Subscribe(string feedUrl, long userId)
        {
            if (string.IsNullOrEmpty(feedUrl))
                throw new ArgumentNullException(nameof(feedUrl));
            if (!IsValidSource(feedUrl))
                throw new FormatException(nameof(feedUrl));

            using var scope = _serviceScopeFactory.CreateScope();
            using var context = scope.ServiceProvider.GetRequiredService<Context>();
            var rss = await context.Feeds.FirstOrDefaultAsync(r => r.Url == feedUrl);
            if (rss == null)
            {
                rss = new RssFeed() { ShortId = ShortId.Generate(true, false, _idLength), Url = feedUrl };
                context.Feeds.Add(rss);
            }

            await context.Entry(rss).Collection(r => r.Subscriptions).LoadAsync();
            var subscription = rss.Subscriptions.FirstOrDefault(s => s.UserId == userId);
            if (subscription == null)
            {
                subscription = new Subscription() { UserId = userId, ShortId = rss.ShortId, Feed = rss };
                rss.Subscriptions.Add(subscription);
                await context.SaveChangesAsync();
            }
        }

        private bool IsValidSource(string source)
        {
            return Uri.TryCreate(source, UriKind.Absolute, out var uri) && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        }

        public async Task<IEnumerable<Rss>> GetSubscriptions(long userId)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            using var context = scope.ServiceProvider.GetRequiredService<Context>();
            var subscriptions = await context.Subscriptions.Where(s => s.UserId == userId).Include(s => s.Feed).ToListAsync();
            return subscriptions.Select(s => new Rss() { ShortId = s.Feed.ShortId, Url = s.Feed.Url });
        }

        public async Task<Dictionary<long, List<string>>> GetNewArticles()
        {
            var result = new Dictionary<long, List<string>>();
            try
            {
                using var scope = _serviceScopeFactory.CreateScope();
                using var context = scope.ServiceProvider.GetRequiredService<Context>();
                
                await foreach (var rssUrl in context.Feeds.Include(r => r.Subscriptions).Where(r => r.Subscriptions.Any()).AsAsyncEnumerable())
                {
                    var rss = await LoadRss(rssUrl.Url);
                    var links = rss.Items.Where(x => IsNewArticle(x)).SelectMany(x => x.Links).Select(l => GetUrl(l, rssUrl.Url)).Where(l => l != null).ToList();
                    if (links.Count == 0)
                        continue;

                    foreach (var subscription in rssUrl.Subscriptions)
                    {
                        if (!result.TryGetValue(subscription.UserId, out var articles))
                        {
                            articles = new List<string>();
                            result[subscription.UserId] = articles;
                        }
                        articles.AddRange(links);
                    }
                }
            }
            finally
            {
                _lastCheck = DateTime.Now;
            }
            return result;
        }

        private string GetUrl(SyndicationLink link, string rssUrl)
        {
            if (link.Uri.IsAbsoluteUri)
                return link.Uri.ToString();

            try
            {
                var uri = new Uri(rssUrl);
                return $"{uri.Host}{link.Uri}";
            }
            catch
            {
                return null;
            }
        }

        private async Task<SyndicationFeed> LoadRss(string rssUrl)
        {
            using var httpClient = new HttpClient();
            var rss = await httpClient.GetStringAsync(rssUrl);
            using var stringReader = new StringReader(rss);
            return SyndicationFeed.Load(XmlReader.Create(stringReader));
        }

        private bool IsNewArticle(SyndicationItem item)
        {
            return item.LastUpdatedTime > _lastCheck || item.PublishDate > _lastCheck;
        }

        public async Task Unsubscribe(long chatId, string id)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            using var context = scope.ServiceProvider.GetRequiredService<Context>();
            var subscription = await context.Subscriptions.FirstOrDefaultAsync(s => s.ShortId == id && s.UserId == chatId);

            using var scope1 = _serviceScopeFactory.CreateScope();
            using var context1 = scope1.ServiceProvider.GetRequiredService<Context>();
            var subscription1 = await context1.Subscriptions.FirstOrDefaultAsync(s => s.ShortId == id && s.UserId == chatId);            

            if (subscription != null)
            {
                context.Entry(subscription).State = EntityState.Deleted;
                //context.Subscriptions.Remove(subscription);
                await context.SaveChangesAsync();

                context1.Entry(subscription1).State = EntityState.Deleted;
                await context1.SaveChangesAsync();
            }
        }
    }
}
