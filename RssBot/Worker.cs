using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RssBot.Configuration;
using Telegram.Bot;
using Telegram.Bot.Types;
using Telegram.Bot.Types.ReplyMarkups;

namespace RssBot
{
    public class Worker : IHostedService, IDisposable
    {
        private const string _prefix = "/d_";

        private readonly int _subscriptionsInMessage;
        private readonly int _timeBetweenCheckingFeeds;
        private readonly ILogger<Worker> _logger;
        private ITelegramBotClient _botClient;
        private readonly RssManager _rss;

        private Timer _timer;

        public Worker(ILogger<Worker> logger, RssManager rss, IOptions<AppSettings> appSettings)
        {
            _rss = rss;
            _logger = logger;
            _subscriptionsInMessage = appSettings.Value.SubscriptionsInMessage;
            _timeBetweenCheckingFeeds = appSettings.Value.TimeBetweenCheckingFeeds;
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            if (TryCreateBotClient(out var botClient))
            {
                _botClient = botClient;
                _botClient.OnMessage += Bot_OnMessage;
                _botClient.OnCallbackQuery += Bot_OnCallbackQuery;
                _botClient.StartReceiving();
                _timer = new Timer(new TimerCallback(Callback), null, 0, _timeBetweenCheckingFeeds);
            }
            else
            {
                StopAsync(cancellationToken);
            }
            return Task.CompletedTask;
        }

        private bool TryCreateBotClient(out ITelegramBotClient botClient)
        {
            botClient = null;

            var telegramKey = Environment.GetEnvironmentVariable("TelegramKey");
            if (string.IsNullOrEmpty(telegramKey))
                return false;

            try
            {
                botClient = new TelegramBotClient(telegramKey);
            }
            catch (ArgumentException ex)
            {
                _logger.LogError(ex.Message);
                return false;
            }
            return true;
        }

        private async void Callback(object state)
        {
            try
            {
                var newArticles = await _rss.GetNewArticles();
                foreach (var change in newArticles)
                {
                    await _botClient.SendTextMessageAsync(change.Key, string.Join('\n', change.Value));
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"{DateTime.Now} - {ex.Message}");
            }
        }

        private async void Bot_OnCallbackQuery(object sender, Telegram.Bot.Args.CallbackQueryEventArgs e)
        {
            try
            {
                if (int.TryParse(e.CallbackQuery.Data, out var page))
                {
                    await ShowSubscriptions(e.CallbackQuery.Message.Chat, e.CallbackQuery.Message, page);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"{DateTime.Now} - {ex.Message}");
            }
        }

        private async void Bot_OnMessage(object sender, Telegram.Bot.Args.MessageEventArgs e)
        {
            try
            {
                switch (e.Message.Text)
                {
                    case "/help":
                        var help = "To subscribe to an rss feed input address to this feed. When bot finds new articles, it will send them for you\n/subscriptions - show your subscriptions\n";
                        await _botClient.SendTextMessageAsync(e.Message.Chat, help);
                        break;
                    case "/subscriptions":
                        await ShowSubscriptions(e.Message.Chat, null, 1);
                        break;
                    default:
                        if (e.Message.Text.StartsWith(_prefix))
                        {
                            var id = e.Message.Text.Substring(_prefix.Length);
                            await _rss.Unsubscribe(e.Message.Chat.Id, id);
                        }
                        else
                        {
                            await _rss.Subscribe(e.Message.Text, e.Message.Chat.Id);
                        }
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"{DateTime.Now} - {ex.Message}");
            }
        }

        private async Task ShowSubscriptions(Chat chat, Message message, int page)
        {
            var sources = (await _rss.GetSubscriptions(chat.Id)).ToList();
            if (sources.Count == 0)
            {
                await _botClient.SendTextMessageAsync(chat, "You don't have any subscription");
            }
            else
            {
                var sourcesOnPage = sources.Skip((page - 1) * _subscriptionsInMessage).Take(_subscriptionsInMessage);
                var subscriptions = string.Join('\n', sources.Select(x => $"{x.Url}\nUnsubscribe: {_prefix}{x.ShortId}"));
                var text = $"You have subscriptions to next sites: {subscriptions}";

                if (message == null)
                    await _botClient.SendTextMessageAsync(chat, text, replyMarkup: GetKeyboard(page, sources.Count));
                else
                    await _botClient.EditMessageTextAsync(chat, message.MessageId, text, replyMarkup: GetKeyboard(page, sources.Count));
            }
        }

        private InlineKeyboardMarkup GetKeyboard(int page, int total)
        {
            IEnumerable<InlineKeyboardButton> GetButtons()
            {
                if (page > 1)
                    yield return InlineKeyboardButton.WithCallbackData("<", (page - 1).ToString());
                if (page * _subscriptionsInMessage < total)
                    yield return InlineKeyboardButton.WithCallbackData(">", (page + 1).ToString());
            }
            return new InlineKeyboardMarkup(GetButtons());
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            if (_botClient != null)
            {
                _botClient.StopReceiving();
                _botClient.OnMessage -= Bot_OnMessage;
                _botClient.OnCallbackQuery -= Bot_OnCallbackQuery;
            }
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _timer?.Dispose();
        }
    }
}
