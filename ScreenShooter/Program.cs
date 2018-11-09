using System;
using System.Collections.Generic;
using System.Reflection;
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
    internal class Program
    {
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static Program _staticSelf;
        private static string _programIdentifier;

        private static readonly Random Rnd = new Random();
        private readonly List<IActuator> _actuators = new List<IActuator>();
        private readonly List<IConnector> _connectors = new List<IConnector>();
        private readonly List<Task> _connectorTasks = new List<Task>();
        private TomlTable _config;

        private bool _hasEnteredCleanUpRoutine;

        public static int Main(string[] args)
        {
            return CommandLineApplication.Execute<Program>(args);
        }

        // ReSharper disable once UnusedMember.Global
        public async Task OnExecuteAsync()
        {
            Logger.Debug("Entering OnExecute()");

            AppDomain.CurrentDomain.UnhandledException += CurrentDomainUnhandledException;
            AppDomain.CurrentDomain.ProcessExit += (sender, e) => { AsyncHelper.RunSync(CleanUp); };
            Console.CancelKeyPress += (sender, e) =>
            {
                Logger.Info("SIGINT received, cleaning up...");
                AsyncHelper.RunSync(CleanUp);
            };
            _staticSelf = this;

            _programIdentifier =
                $"{Assembly.GetExecutingAssembly().GetName().Name} {Assembly.GetExecutingAssembly().GetName().Version}";
            Logger.Info(_programIdentifier);
            Console.Title = _programIdentifier;

            if (ConfigPath != null) _config = Toml.ReadFile(ConfigPath);

            if (ConfigPath != null && Address == null)
            {
                Logger.Debug("Entering daemon mode");

                Logger.Debug("Enumerating actuators");
                var actuators = _config.Get<TomlTable>("Actuator");
                CreateObjects(actuators, "ScreenShooter.Actuator", _actuators);

                Logger.Debug("Enumerating Connectors");
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
                    connector.NewRequest += Connect;
                }

                Logger.Info("Entered running state");
                await Task.WhenAll(_connectorTasks);
                await CleanUp();

                Logger.Debug("All connectors have quit");
            }
            else if (Address != null)
            {
                // enter one-shot mode
                Logger.Debug("Entering one-shot mode");
                var g = Guid.NewGuid();
                var actuator = new HeadlessChromeActuator();
                Logger.Debug("Capturing page");
                var ret = await actuator.CapturePage(Address, g);
                Logger.Info(ret);
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

        private async void Connect(object sender, EventArgs e)
        {
            var s = sender as IConnector;
            if (s == null)
            {
                Logger.Warn("sender is not a IConnector, ignoring");
                return;
            }

            var ex = e as NewRequestEventArgs;
            if (ex == null)
            {
                Logger.Warn("eventArgs is not a NewRequestEventArgs, ignoring");
                return;
            }

            // randomly select a actuator
            var r = Rnd.Next(_actuators.Count);
            var a = _actuators[r];

            // execute
            var g = Guid.NewGuid();
            Logger.Debug($"Creating session {g}");
            RuntimeInformation.OnGoingRequests += 1;

            try
            {
                var ret = await a.CapturePage(ex.Url, g);
                Logger.Info(ret);

                Logger.Debug("Sending result");
                await s.SendResult(ret, ex);
                RuntimeInformation.FinishedRequests += 1;
            }
            catch (Exception exception)
            {
                CurrentDomainUnhandledException(this, new UnhandledExceptionEventArgs(exception, false));
                await s.SendResult(new ExecutionResult()
                {
                    Identifier = g,
                    Url = ex.Url,
                    HasPotentialUnfinishedDownloads = true,
                    StatusText = $"Something happened. \nException: {exception}",
                }, ex);

                RuntimeInformation.FailedRequests += 1;
            }
            finally
            {
                RuntimeInformation.OnGoingRequests -= 1;
                Logger.Debug($"Ending session {g}");
            }
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