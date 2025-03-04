using SharpEML.Core;
using SharpEML.Logging;
using System;
using System.Diagnostics;
using System.Threading.Tasks;

namespace SharpEML.Services
{
    public class ApplicationRunner
    {
        private readonly LogService _logger;
        private readonly Stopwatch _globalTimer = new Stopwatch();
        private EmailProcessor _processor;

        public ApplicationRunner(LogService logger)
        {
            _logger = logger;
        }

        public async Task<int> RunAsync(string[] args)
        {
            try
            {
                _globalTimer.Start();

                if (!ArgumentValidator.Validate(args, _logger))
                    return 1;

                var (sourceDir, dbPath) = ArgumentHandler.Parse(args, _logger);

                using (_processor = new EmailProcessor(sourceDir, dbPath, _logger))
                {
                    await _processor.ProcessEmailsAsync();
                    LogPerformanceStatistics();
                }

                return 0;
            }
            catch (Exception ex)
            {
                ExceptionHandler.Handle(ex, _logger);
                return ex is AggregateException ? 3 : 4;
            }
        }

        private void LogPerformanceStatistics()
        {
            var stats = new
            {
                Duration = _globalTimer.Elapsed,
                FilesProcessed = _processor.ProcessedCount,
                ThroughputMB = _processor.TotalBytes / 1024d / 1024d,
                AvgSpeed = _processor.TotalBytes / 1024d / 1024d / _globalTimer.Elapsed.TotalHours
            };

            // 使用INFO级别输出处理摘要
            _logger.LogInfo("Processing Summary".PadRight(40, '-'));

            // 使用INFO级别输出统计信息
            _logger.LogInfo($"{"Total Duration:",-20} {stats.Duration:h\\:mm\\:ss}");
            _logger.LogInfo($"{"Files Processed:",-20} {stats.FilesProcessed:N0}");
            _logger.LogInfo($"{"Data Throughput:",-20} {stats.ThroughputMB:N2} MB");
            _logger.LogInfo($"{"Average Speed:",-20} {stats.AvgSpeed:N1} MB/hour");
            _logger.LogInfo(string.Empty.PadRight(40, '-'));
        }
    }
}
