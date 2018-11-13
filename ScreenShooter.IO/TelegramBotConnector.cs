using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using NLog;
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
        public List<long> Administrators { get; set; } = new List<long>();

        public event UserRequestEventHandler NewRequest;

        public async Task CreateSession()
        {
            Logger.Debug("Enter CreateSession()");
            _bot = new TelegramBotClient(ApiKey);
            var me = await _bot.GetMeAsync();
            Logger.Info($"Binding on Bot @{me.Username}");

            _bot.OnMessage += OnMessageReceived;
            _bot.OnReceiveError += OnReceiveError;

            foreach (var administrator in Administrators)
            {
                await _bot.SendTextMessageAsync(
                    administrator,
                    $"Bot has been started. - {Globals.ProgramIdentifier}"
                );
            }
        }

        public async Task EventLoop()
        {
            _bot.StartReceiving(System.Array.Empty<UpdateType>());
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
            foreach (var administrator in Administrators)
            {
                await _bot.SendTextMessageAsync(
                    administrator,
                    $"Bot is shutting down. - {Globals.ProgramIdentifier}"
                );
            }

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

            Logger.Debug($"Received message from @{message.From.Username}: {message.Text}");

            try
            {
                if (message.Type == MessageType.Text && message.Text.StartsWith('/'))
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
                            if (!Administrators.Contains(message.Chat.Id)) goto default;
                            await _bot.SendTextMessageAsync(message.Chat, $"User ID: {message.Chat.Id}",
                                replyToMessageId: message.MessageId);
                            break;
                        case "/DiagnosticInfo":
                            if (!Administrators.Contains(message.Chat.Id)) goto default;
                            await _bot.SendTextMessageAsync(message.Chat, RuntimeInformation.ToString(),
                                replyToMessageId: message.MessageId);
                            break;
                        case "/ForceGarbageCollection":
                            if (!Administrators.Contains(message.Chat.Id)) goto default;
                            var sb = new StringBuilder();
                            sb.AppendLine($"Before GC: {RuntimeInformation.WorkingSet}Bytes");
                            GC.Collect(2, GCCollectionMode.Optimized, true, true);
                            sb.AppendLine($"After GC: {RuntimeInformation.WorkingSet}Bytes");
                            await _bot.SendTextMessageAsync(message.Chat, sb.ToString(),
                                replyToMessageId: message.MessageId);
                            break;
                        default:
                            await _bot.SendTextMessageAsync(message.Chat, "Unknown command. \n\n/help - get help",
                                replyToMessageId: message.MessageId);
                            break;
                    }
                }
                else
                {
                    // get text from message
                    var content = "";
                    switch (message.Type)
                    {
                        case MessageType.Text:
                            content = message.Text;
                            break;
                        case MessageType.Photo:
                        case MessageType.Video:
                        case MessageType.Audio:
                        case MessageType.Document:
                            content = message.Caption;
                            break;
                        default:
                            content = null;
                            break;
                    }

                    if (content == null)
                    {
                        Logger.Debug($"Received unknown message from @{message.From.Username} type {message.Type}");
                        await _bot.SendTextMessageAsync(message.Chat, "Unacceptable content",
                            replyToMessageId: message.MessageId);
                        return;
                    }

                    // check if there is any valid url

                    var result = Url.ExtractValidUrls(content).ToList();

                    // also try if we can extract URLs from entities
                    if (message.Entities != null)
                        foreach (var e in message.Entities)
                        {
                            switch (e.Type)
                            {
                                case MessageEntityType.Url:
                                    Logger.Debug($"Entity type URL: {e.Url}");
                                    if (Url.IsValidUrl(e.Url)) result.Add(e.Url.Trim());
                                    break;
                                case MessageEntityType.TextLink:
                                    // This is equal to <a> tag in HTML
                                    Logger.Debug($"Entity type TextURL: {e.Url}");
                                    if (Url.IsValidUrl(e.Url)) result.Add(e.Url.Trim());
                                    break;
                            }
                        }

                    // remove duplication
                    result = result.Distinct().ToList();

                    // TODO: if returned multiple URLs?

                    if (result.Count > 0)
                    {
                        // is a valid URL
                        await _bot.SendTextMessageAsync(message.Chat,
                            $"Job enqueued. Sit back and relax - this is going to take minutes. \nRunning: {RuntimeInformation.OnGoingRequests}\nWaiting: {RuntimeInformation.QueuedRequests}\nMax parallel jobs: {Globals.GlobalConfig.ParallelJobs}",
                            replyToMessageId: message.MessageId);
                        NewRequest?.Invoke(this, new UserRequestEventArgs
                        {
                            Url = result[0],
                            RequestContext = message,
                            RequestTypes = new List<UserRequestType> {UserRequestType.Pdf, UserRequestType.Png},
                            IsPriority = Administrators.Contains(message.Chat.Id)
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
            catch (Exception e)
            {
                // if we do not process error, the error will be persist when bot restarts.
                // so we end up here.
                Logger.Error($"Something happened. Exception:\n{e}");
            }
            
        }

        private static void OnReceiveError(object sender, ReceiveErrorEventArgs receiveErrorEventArgs)
        {
            Logger.Error(
                $"Telegram Bot API receive error {receiveErrorEventArgs.ApiRequestException.ErrorCode}: {receiveErrorEventArgs.ApiRequestException.Message}");
        }
    }
}