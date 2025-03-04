using log4net;
using log4net.Core;

namespace SharpEML.Logging
{
    public class LogService
    {
        private readonly ILog _logger;

        public LogService()
        {
            LoggerConfig.Configure();
            _logger = LogManager.GetLogger(typeof(LogService));
        }

        public void LogProcessStart(string message) => LogWithType("INFO", "Process Start: " + message);
        public void LogStep(string step, double duration) => LogWithType("STEP", $"{step} completed in {duration:0.0}ms");
        public void LogError(string message) => LogWithType("ERROR", message);
        public void LogDebug(string message) => LogWithType("DEBUG", message);
        public void LogWarning(string message) => LogWithType("WARNING", message);
        public void LogInfo(string message) => LogWithType("INFO", message);
        public void LogProgress(string message) => LogWithType("PROGRESS", message);
        public void LogFatal(string message) => LogWithType("FATAL", message);


       

        private void LogWithType(string logType, string message)
        {
            var loggingEvent = new LoggingEvent(
                typeof(LogService),
                null,
                _logger.Logger.Name,
                Level.Info,
                message,
                null);

            loggingEvent.Properties["LogType"] = logType;
            _logger.Logger.Log(loggingEvent);
        }

        public void LogBatchProcess(string action, double totalDuration)
        {
            LogWithType("STEP", $"{action} completed in {totalDuration:0.0}ms");
        }
    }
}
