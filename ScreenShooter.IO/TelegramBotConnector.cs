using System;
using System.IO;
using System.Threading.Tasks;
using NLog;
using ScreenShooter.Actuator;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using File = System.IO.File;

namespace ScreenShooter.IO
{
    public class TelegramMessageEventArgs : NewRequestEventArgs
    {
        public Message OriginMessage { get; set; }
    }

    internal class TelegramBotConnector : IConnector
    {
        private static TelegramBotClient _bot;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private bool onQuit;
        public string ApiKey { get; set; }

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

        public async Task SendResult(ExecutionResult result, NewRequestEventArgs e)
        {
            var ex = e as TelegramMessageEventArgs;
            if (ex == null)
            {
                Logger.Warn($"{e} is not a TelegramMessageEventArgs, ignoring");
                return;
            }

            foreach (var filePath in result.Attachments)
                using (var fs = File.OpenRead(filePath))
                {
                    var fileName = Path.GetFileName(filePath);
                    Logger.Debug($"Uploading file {fileName}");
                    var inputOnlineFile = new InputOnlineFile(fs, fileName);
                    await _bot.SendDocumentAsync(ex.OriginMessage.Chat, inputOnlineFile,
                        replyToMessageId: ex.OriginMessage.MessageId);
                }

            Logger.Debug("Sending session information");
            await _bot.SendTextMessageAsync(
                ex.OriginMessage.Chat,
                $"<pre>{result}</pre>",
                replyToMessageId: ex.OriginMessage.MessageId,
                parseMode: ParseMode.Html
            );
        }

        public async Task DestroySession()
        {
            onQuit = true;
            await Task.Run(() => _bot.StopReceiving());
        }

        private async void OnMessageReceived(object sender, MessageEventArgs messageEventArgs)
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
                await _bot.SendTextMessageAsync(message.Chat, "Unacceptable content",
                    replyToMessageId: message.MessageId);
                return;
            }

            ;
            Logger.Debug($"Received message from @{message.From.Username}: {message.Text}");
            // TODO: check if the text is a URL
            NewRequest?.Invoke(this, new TelegramMessageEventArgs
            {
                OriginMessage = message,
                Url = message.Text
            });
        }

        private void OnReceiveError(object sender, ReceiveErrorEventArgs receiveErrorEventArgs)
        {
            Logger.Error(
                $"Telegram Bot API receive error {receiveErrorEventArgs.ApiRequestException.ErrorCode}: {receiveErrorEventArgs.ApiRequestException.Message}");
        }
    }
}