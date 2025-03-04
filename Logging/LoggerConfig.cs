using log4net;
using log4net.Appender;
using log4net.Repository.Hierarchy;

namespace SharpEML.Logging
{
    public static class LoggerConfig
    {
        public static void Configure()
        {
            var hierarchy = (Hierarchy)LogManager.GetRepository();
            hierarchy.ResetConfiguration();

            // 仅保留控制台输出
            var consoleAppender = new ColorConsoleAppender();
            hierarchy.Root.AddAppender(consoleAppender);

            // 设置日志级别
#if DEBUG
            hierarchy.Root.Level = log4net.Core.Level.Debug;
#else
            hierarchy.Root.Level = log4net.Core.Level.Info;
#endif

            hierarchy.Configured = true;
        }
    }
}