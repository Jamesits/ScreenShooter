using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using McMaster.Extensions.CommandLineUtils;
using Nett;
using NLog;
using ScreenShooter.Actuator;
using ScreenShooter.Helper;
using ScreenShooter.IO;

namespace ScreenShooter
{
    [Command(Name = "ScreenShooter.exe", Description = "Simple web page screen shot utility")]
    public class Program
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static Program _staticSelf;

        private static readonly Random Rnd = new Random();
        private readonly List<IActuator> _actuators = new List<IActuator>();
        private readonly List<IConnector> _connectors = new List<IConnector>();
        private readonly List<Task> _connectorTasks = new List<Task>();
        private readonly Helper.Queue<Helper.UserRequestEventArgs> _requestQueue = new Helper.Queue<Helper.UserRequestEventArgs>();
        private TomlTable _config;

        private bool _hasEnteredCleanUpRoutine;
        private Mutex _busyMutex = new Mutex(false);

        public static int Main(string[] args)
        {
            return CommandLineApplication.Execute<Program>(args);
        }

        // ReSharper disable once UnusedMember.Global
        public async Task OnExecuteAsync()
        {
            Logger.Debug("Entering OnExecute()");

            #region Set up basic events

            AppDomain.CurrentDomain.UnhandledException += CurrentDomainUnhandledException;
            AppDomain.CurrentDomain.ProcessExit += (sender, e) => { AsyncHelper.RunSync(CleanUp); };
            Console.CancelKeyPress += (sender, e) =>
            {
                Logger.Info("SIGINT received, cleaning up...");
                AsyncHelper.RunSync(CleanUp);
            };

            #endregion

            #region Global initialization
            _staticSelf = this;

            Globals.ProgramIdentifier =
                $"{Assembly.GetExecutingAssembly().GetName().Name} {Assembly.GetExecutingAssembly().GetName().Version}";
            Console.Title = Globals.ProgramIdentifier;
            Logger.Info(Globals.ProgramIdentifier);
            
            if (ConfigPath != null) _config = Toml.ReadFile(ConfigPath);

            // parse config file
            Globals.GlobalConfig = _config.Get<GlobalConfig>("GlobalConfig");
            #endregion

            #region Apply config

            // set up GC
            if (Globals.GlobalConfig.LowMemoryAddMemoryPressure > 0)
            {
                // Ask @hjc4869 why there is a *2
                Logger.Debug($"Adding memory pressure {Globals.GlobalConfig.LowMemoryAddMemoryPressure * 2 / 1048576}MiB");
                GC.AddMemoryPressure(Globals.GlobalConfig.LowMemoryAddMemoryPressure * 2);
            }

            #endregion

            if (ConfigPath != null && Address == null)
            {
                #region Entering daemon mode

                Logger.Debug("Entering daemon mode");

                Logger.Trace("Enumerating actuators");
                var actuators = _config.Get<TomlTable>("Actuator");
                CreateObjects(actuators, "ScreenShooter.Actuator", _actuators);

                Logger.Trace("Enumerating Connectors");
                var connectors = _config.Get<TomlTable>("Connector");
                CreateObjects(connectors, "ScreenShooter.IO", _connectors);

                if (_connectors.Count == 0)
                {
                    Logger.Fatal("No valid connector created, check your config");
                    Environment.Exit(-1);
                }

                if (_actuators.Count == 0)
                {
                    Logger.Fatal("No valid actuators found, check your config");
                    Environment.Exit(-1);
                }

                Logger.Debug("Starting connectors");
                foreach (var connector in _connectors)
                {
                    await connector.CreateSession();
                    _connectorTasks.Add(connector.EventLoop());
                    connector.NewRequest += QueueUserRequest;
                }

                Logger.Info("Entered running state");
                await Task.WhenAll(_connectorTasks);
                await CleanUp();

                Logger.Debug("All connectors have quit, daemon quitting");

                #endregion
            }
            else if (Address != null)
            {
                #region enter one-shot mode
                Logger.Debug("Entering one-shot mode");
                var request = new UserRequestEventArgs()
                {
                    Url = Address,
                    Requester = new NullConnector(),
                };
                var actuator = new HeadlessChromeActuator();
                Logger.Debug("Capturing page");
                var ret = await actuator.CapturePage(Address, request);
                Logger.Info(ret);
                #endregion
            }
            else
            {
                Logger.Fatal("Confused by the provided command line arguments");
                Environment.Exit(-1);
            }

            Logger.Debug("Exiting OnExecute()");
        }

        private static void CreateObjects<TInterfaceType>(TomlTable config, string typeStringPrefix,
            ICollection<TInterfaceType> outList)
        {
            foreach (var objectType in config)
            {
                var typeString = $"{typeStringPrefix}.{objectType.Key}";
                // Type.GetType requires assembly name
                // Here we assume the actual type and the interface is in the same assembly
                var t = typeof(TInterfaceType).Assembly.GetType(typeString);
                foreach (var objectConfig in objectType.Value.Get<TomlTableArray>().Items)
                    try
                    {
                        Logger.Info($"Initializing {typeof(TInterfaceType).Name} {objectType.Key}");
                        var instance = (TInterfaceType) objectConfig.Get(t);
                        outList.Add(instance);
                    }
                    catch (ArgumentNullException)
                    {
                        Logger.Warn($"Type {objectType.Key} not found");
                    }
            }
        }

        private async void QueueUserRequest(object sender, UserRequestEventArgs e)
        {
            var s = sender as IConnector;
            if (s == null)
            {
                Logger.Warn("sender is not a IConnector, ignoring");
                return;
            }

            e.Requester = s;

            // add this request to the request queue
            _requestQueue.Put(e, e.IsPriority);

            // if user configured aggressive GC, perform it now
            if (Globals.GlobalConfig.AggressiveGc)
            {
                Logger.Debug("GC requested");
                await Task.Run(() => { GC.Collect(); });
            }

            await ProcessRequests();
        }

        private async Task ProcessRequests()
        {
            if (!_busyMutex.WaitOne(0))
            {
                Logger.Trace("ProcessRequests() early return, already running");
                return;
            }
            while (_requestQueue.Count > 0)
            {
                var currentRequest = _requestQueue.Get();
                var requester = currentRequest.Requester as IConnector;
                if (requester == null)
                {
                    Logger.Error("Requester is not a IConnector");
                    return;
                }
                RuntimeInformation.OnGoingRequests += 1;
                Logger.Debug($"Processing request {currentRequest.Id}");

                // randomly select a actuator
                // TODO: verify if actuator has sufficient capability
                var r = Rnd.Next(_actuators.Count);
                var a = _actuators[r];

                try
                { // try get a result from actuator
                    var ret = await a.CapturePage(this, currentRequest);
                    Logger.Info(ret);

                    Logger.Debug("Sending result");
                    await requester.SendResult(this, ret);
                    RuntimeInformation.FinishedRequests += 1;
                }
                catch (Exception exception)
                { // if failed, we make a result ourselves
                    CurrentDomainUnhandledException(this, new UnhandledExceptionEventArgs(exception, false));
                    await requester.SendResult(this, new CaptureResponseEventArgs()
                    {
                        Request = currentRequest,
                        HasPotentialUnfinishedDownloads = true,
                        StatusText = $"Something happened. \nException: {exception}",
                    });

                    RuntimeInformation.FailedRequests += 1;
                }
                finally
                {
                    RuntimeInformation.OnGoingRequests -= 1;
                    Logger.Debug($"Finished request {currentRequest.Id}");
                }
            }
            _busyMutex.ReleaseMutex();
        }

        private async Task CleanUp()
        {
            if (_hasEnteredCleanUpRoutine)
            {
                Logger.Warn("CleanUp() already executed, ignoring request");
                return;
            }

            Logger.Debug("Enter CleanUp()");
            _hasEnteredCleanUpRoutine = true;
            foreach (var connector in _connectors) await connector.DestroySession();
            await Task.WhenAll(_connectorTasks);
            Logger.Debug("Exiting CleanUp()");
        }

        /// <summary>
        ///     The default global exception handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="eventArgs"></param>
        private static void CurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs eventArgs)

        {
            var e = eventArgs.ExceptionObject as Exception;
            Logger.Error($"Something happened. \n\nException:\n{e}\n\nInnerException:{e?.InnerException}");
            if (eventArgs.IsTerminating)
            {
                _staticSelf?.CleanUp();
            }
        }

        #region arguments

        // ReSharper disable UnassignedGetOnlyAutoProperty
        [Option("-c|--config", CommandOptionType.SingleValue, Description = "_config file location")]
        public string ConfigPath { get; }

        [Argument(0)] public string Address { get; }
        // ReSharper restore UnassignedGetOnlyAutoProperty

        #endregion
    }
}