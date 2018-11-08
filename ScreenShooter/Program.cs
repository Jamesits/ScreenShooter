using System;
using System.Threading.Tasks;
using ScreenShooter.Actuator;
using McMaster.Extensions.CommandLineUtils;

namespace ScreenShooter
{
    [Command(Name = "ScreenShooter.exe", Description = "Simple web page screenshot utility")]
    class Program
    {
        #region arguments
        // ReSharper disable UnassignedGetOnlyAutoProperty
        [Option("-c|--config", CommandOptionType.SingleValue, Description = "Config file location")]
        public string ConfigPath { get; }

        [Argument(0)]
        public string Address { get; }
        // ReSharper restore UnassignedGetOnlyAutoProperty
        #endregion

        private static readonly NLog.Logger Logger = NLog.LogManager.GetCurrentClassLogger();

        public static int Main(string[] args) => CommandLineApplication.Execute<Program>(args);

        // ReSharper disable once UnusedMember.Global
        public async Task OnExecuteAsync()
        {
            Logger.Debug("Entering OnExecute()");

            AppDomain.CurrentDomain.UnhandledException += CurrentDomainUnhandledException;

            if (ConfigPath != null && Address == null)
            {
                // TODO: enter daemon mode
                Logger.Debug("Entering daemon mode");
            } else if (ConfigPath == null && Address != null)
            {
                // enter one-shot mode
                Logger.Debug("Entering one-shot mode");
                var g = Guid.NewGuid();
                var actuator = new HeadlessChromeActuator();
                Logger.Debug("Creating session");
                await actuator.CreateSession(Address);
                Logger.Debug($"Saving screenshot to {g}.png");
                await actuator.CaptureImage($"{g}.png");
                Logger.Debug($"Saving PDF to {g}.pdf");
                await actuator.CapturePdf($"{g}.pdf");
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
