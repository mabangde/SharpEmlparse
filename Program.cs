using log4net;
using System;
using SharpEML.Logging;
using SharpEML.Services;

namespace SharpEML
{
    class Program
    {
        private static LogService _logger;

        static int Main(string[] args)
        {
            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
                ExceptionHandler.GlobalExceptionHandler(sender, e, _logger);

            try
            {
                InitializeLogger();
                var runner = new ApplicationRunner(_logger);
                return runner.RunAsync(args).GetAwaiter().GetResult();
            }
            finally
            {
                LogManager.Shutdown();
            }
        }

        private static void InitializeLogger()
        {
            LoggerConfig.Configure();
            _logger = new LogService();
            _logger.LogDebug("Logger initialized in " +
                (Environment.UserInteractive ? "Interactive" : "Service") + " mode");
        }
    }
}