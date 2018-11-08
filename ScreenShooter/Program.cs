using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ScreenShooter.Actuator;
using McMaster.Extensions.CommandLineUtils;
using Nett;
using ScreenShooter.IO;

namespace ScreenShooter
{
    [Command(Name = "ScreenShooter.exe", Description = "Simple web page screenshot utility")]
    class Program
    {
        #region arguments
        // ReSharper disable UnassignedGetOnlyAutoProperty
        [Option("-c|--config", CommandOptionType.SingleValue, Description = "_config file location")]
        public string ConfigPath { get; }

        [Argument(0)]
        public string Address { get; }
        // ReSharper restore UnassignedGetOnlyAutoProperty
        #endregion

        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();
        private TomlTable _config;
        private readonly List<IActuator> _actuators = new List<IActuator>();
        private readonly List<IConnector> _connectors = new List<IConnector>();

        public static int Main(string[] args) => CommandLineApplication.Execute<Program>(args);

        // ReSharper disable once UnusedMember.Global
        public async Task OnExecuteAsync()
        {
            Logger.Debug("Entering OnExecute()");

            AppDomain.CurrentDomain.UnhandledException += CurrentDomainUnhandledException;

            if (ConfigPath != null)
            {
                _config = Toml.ReadFile(ConfigPath);
            }

            if (ConfigPath != null && Address == null)
            {
                Logger.Debug("Entering daemon mode");

                Logger.Debug("Enumerating actuators");
                var actuators = _config.Get<TomlTable>("Actuator");
                CreateObjects(actuators, "ScreenShooter.Actuator", _actuators);

                Logger.Debug("Enumerating Connectors");
                var connectors = _config.Get<TomlTable>("Connector");
                CreateObjects(connectors, "ScreenShooter.IO", _connectors);

            } else if (Address != null)
            {
                // enter one-shot mode
                Logger.Debug("Entering one-shot mode");
                var g = Guid.NewGuid();
                var actuator = new HeadlessChromeActuator();
                Logger.Debug($"Creating session {g}");
                await actuator.CreateSession(Address, g);
                Logger.Debug("Capturing page");
                var ret = await actuator.CapturePage();
                Logger.Info(ret);
                Logger.Debug("Destoring session");
                await actuator.DestroySession();
            }
            else
            {
                Logger.Fatal("Confused by the provided command line arguments");
                Environment.Exit(-1);
            }
            Logger.Debug("Exiting OnExecute()");
        }

        private static void CreateObjects<TInterfaceType>(TomlTable config, string typeStringPrefix, ICollection<TInterfaceType> outList)
        {
            foreach (var objectType in config)
            {
                var typeString = $"{typeStringPrefix}.{objectType.Key}";
                // Type.GetType requires assembly name
                // Here we assume the actual type and the interface is in the same assembly
                var t = typeof(TInterfaceType).Assembly.GetType(typeString); 
                foreach (var objectConfig in objectType.Value.Get<TomlTableArray>().Items)
                {
                    try
                    {
                        Logger.Info($"Initializing {typeof(TInterfaceType)} {objectType.Key}");
                        var instance = (TInterfaceType) objectConfig.Get(t);
                        outList.Add(instance);
                    }
                    catch (ArgumentNullException)
                    {
                        Logger.Error($"Type {objectType.Key} not found");
                    }
                    
                }
            }
        }

        /// <summary>
        /// The default global exception handler
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="eventArgs"></param>
        private static void CurrentDomainUnhandledException(object sender, UnhandledExceptionEventArgs eventArgs)

        {
            var e = eventArgs.ExceptionObject as Exception;
            Logger.Error($"Something happened. \n\nException:\n{e}\n\nInnerException:{e?.InnerException}");
        }
    }
}
