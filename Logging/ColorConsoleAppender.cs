using log4net.Appender;
using log4net.Core;
using System;
using System.Collections.Generic;

namespace SharpEML.Logging
{
    public class ColorConsoleAppender : AppenderSkeleton
    {
        private static readonly Dictionary<string, ConsoleColor> _colorMap = new Dictionary<string, ConsoleColor>
        {
            { "DEBUG", ConsoleColor.DarkGray },
            { "INFO", ConsoleColor.White },
            { "WARNING", ConsoleColor.Yellow },
            { "ERROR", ConsoleColor.Red },
            { "FATAL", ConsoleColor.DarkRed },
            { "PROGRESS", ConsoleColor.Cyan },
            { "STEP", ConsoleColor.Green }
        };

        protected override void Append(LoggingEvent loggingEvent)
        {
            var logType = loggingEvent.Properties["LogType"]?.ToString() ?? "INFO";
            var message = $"{loggingEvent.TimeStamp:HH:mm:ss} [{logType.PadRight(8)}] {loggingEvent.MessageObject}";

            Console.ForegroundColor = _colorMap.TryGetValue(logType, out var color)
                ? color
                : ConsoleColor.Gray;

            Console.WriteLine(message);
            Console.ResetColor();
        }
    }
}
