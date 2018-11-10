using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using NLog;
using ScreenShooter.Actuator;
using ScreenShooter.Helper;
using Telegram.Bot;
using Telegram.Bot.Args;
using Telegram.Bot.Exceptions;
using Telegram.Bot.Types;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types.InputFiles;
using File = System.IO.File;
using Path = System.IO.Path;

namespace ScreenShooter.IO
{
    // ReSharper disable once UnusedMember.Global
    internal class TelegramBotConnector : IConnector
    {
        private static TelegramBotClient _bot;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private bool _onQuit;
        public string ApiKey { get; set; }
        public uint MaxUploadRetries { get; set; } = 3;

        public event UserRequestEventHandler NewRequest;

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
            while (!_onQuit) await Task.Delay(1000);
        }

        public async Task SendResult(object sender, CaptureResponseEventArgs e)
        {
            if (e == null)
            {
                Logger.Warn("result is null, ignoring");
                return;
            }

            var message = e.Request.RequestContext as Message;
            if (message == null)
            {
                Logger.Error("Returned RequestContext is not a valid Telegram UserRequest object");
                return;
            }

            if (e.Attachments != null)
            foreach (var filePath in e.Attachments)
                using (var fs = File.OpenRead(filePath))
                {
                    var trial = 0;
                    var succeed = false;
                    var fileName = Path.GetFileName(filePath);
                    while (!succeed && trial < MaxUploadRetries)
                    {
                        try
                        {
                            Logger.Debug($"(retry {trial}/{MaxUploadRetries}) Uploading file \"{fileName}\"");
                            var inputOnlineFile = new InputOnlineFile(fs, fileName);
                            await _bot.SendDocumentAsync(message.Chat, inputOnlineFile,
                                replyToMessageId: message.MessageId);
                            succeed = true;
                        }
                        catch (ApiRequestException)
                        {
                            Logger.Warn("Telegram API timeout");
                            trial += 1;
                        }
                    }

                    if (!succeed)
                    {
                        Logger.Error("Unable to upload file \"{fileName}\"");
                        e.StatusText += "Unable to upload file \"{fileName}\".\n";
                    }
                }

            Logger.Debug("Sending session information");
            await _bot.SendTextMessageAsync(
                message.Chat,
                $"<pre>{e}</pre>",
                replyToMessageId: message.MessageId,
                parseMode: ParseMode.Html
            );
        }

        public async Task DestroySession()
        {
            _onQuit = true;
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

            Logger.Debug($"Received message from @{message.From.Username}: {message.Text}");
            if (message.Text.StartsWith('/'))
            {
                // is a command
                switch (message.Text.Split(' ').First())
                {
                    case "/start":
                    case "/help":
                        await _bot.SendTextMessageAsync(message.Chat, 
                            $"Welcome!\nDrop a URL, get a PNG + PDF.\nDemo development bot, service not guaranteed.\nSet up yours at https://github.com/Jamesits/ScreenShooter \n\n{Globals.ProgramIdentifier}",
                            replyToMessageId: message.MessageId);
                        break;
                    case "/UserInfo":
                        await _bot.SendTextMessageAsync(message.Chat, $"User ID: {message.Chat.Id}", replyToMessageId: message.MessageId);
                        break;
                    case "/DiagnosticInfo":
                        await _bot.SendTextMessageAsync(message.Chat, RuntimeInformation.ToString(), replyToMessageId: message.MessageId);
                        break;
                    case "/ForceGarbageCollection":
                        var sb = new StringBuilder();
                        sb.AppendLine($"Before GC: {RuntimeInformation.WorkingSet}Bytes");
                        GC.Collect(2, GCCollectionMode.Optimized, true, true);
                        sb.AppendLine($"After GC: {RuntimeInformation.WorkingSet}Bytes");
                        await _bot.SendTextMessageAsync(message.Chat, sb.ToString(), replyToMessageId: message.MessageId);
                        break;
                    default:
                        await _bot.SendTextMessageAsync(message.Chat, "Unknown command. \n\n/help - get help",
                            replyToMessageId: message.MessageId);
                        break;
                }
            }
            else
            {
                // check if is valid url

                var result = Uri.TryCreate(message.Text, UriKind.Absolute, out var uriResult);

                if (result && (
                                 uriResult.Scheme == Uri.UriSchemeHttp
                                 || uriResult.Scheme == Uri.UriSchemeHttps
                                 || uriResult.Scheme == Uri.UriSchemeFtp
                             )
                             && uriResult.IsLoopback == false
                    )
                {
                    // is a valid URL
                    await _bot.SendTextMessageAsync(message.Chat, $"Job added to queue, please wait. \nRunning: {RuntimeInformation.OnGoingRequests}\nCurrent in queue: {RuntimeInformation.QueuedRequests}\nMax parallel jobs: {Globals.GlobalConfig.ParallelJobs}",
                        replyToMessageId: message.MessageId);
                    NewRequest?.Invoke(this, new UserRequestEventArgs()
                    {
                        Url = uriResult.AbsoluteUri,
                        RequestContext = message,
                        RequestTypes = new List<UserRequestType>{UserRequestType.Pdf, UserRequestType.Png},
                    });

                }
                else
                {
                    // if is not valid URL
                    await _bot.SendTextMessageAsync(
                        message.Chat, 
                        "Sorry, this is not a valid URL",
                        replyToMessageId: message.MessageId
                        );
                }

            }

            
        }

        private static void OnReceiveError(object sender, ReceiveErrorEventArgs receiveErrorEventArgs)
        {
            Logger.Error(
                $"Telegram Bot API receive error {receiveErrorEventArgs.ApiRequestException.ErrorCode}: {receiveErrorEventArgs.ApiRequestException.Message}");
        }
    }
}