using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using ScreenShooter.Actuator;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types.Enums;

namespace ScreenShooter.IO
{
    class TelegramBotConnector: IConnector
    {
        public string ApiKey { get; set; }

        private static TelegramBotClient _bot;
        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private bool onQuit = false;

        public event EventHandler NewRequest;
        public async Task CreateSession()
        {
            Logger.Debug("Enter CreateSession()");
            _bot = new TelegramBotClient(ApiKey);
            var me = await _bot.GetMeAsync();
            Logger.Info($"Binding on Bot @{me.Username}");

            _bot.OnMessage += OnMessageReceived;
            _bot.OnReceiveError += OnReceiveError;
        }

        public async Task EventLoop()
        {
            _bot.StartReceiving(Array.Empty<UpdateType>());
            while (!onQuit) await Task.Delay(1000);
        }

        public async Task SendResult(ExecutionResult result)
        {
            throw new NotImplementedException();
        }

        public async Task DestroySession()
        {
            onQuit = true;
            await Task.Run(() => _bot.StopReceiving());
        }

        private static async void OnMessageReceived(object sender, MessageEventArgs messageEventArgs)
        {
            var message = messageEventArgs.Message;
            if (message == null)
            {
                Logger.Warn("Received null");
                return;
            }
            if (message.Type != MessageType.Text)
            {
                Logger.Debug($"Received unknown message from @{message.From.Username} type {message.Type}");
                await _bot.SendTextMessageAsync(message.Chat, "Unacceptable content", replyToMessageId:message.MessageId);
                return;
            };
            Logger.Debug($"Received message from @{message.From.Username}: {message.Text}");
        }

        private static void OnReceiveError(object sender, ReceiveErrorEventArgs receiveErrorEventArgs)
        {
            Logger.Error($"Telegram Bot API receive error {receiveErrorEventArgs.ApiRequestException.ErrorCode}: {receiveErrorEventArgs.ApiRequestException.Message}");
        }
    }
}
