using SharpEML.Logging;
using System;

namespace SharpEML.Services
{
    public static class ExceptionHandler
    {
        /// <summary>
        /// 处理应用程序中的异常
        /// </summary>
        /// <param name="ex">异常对象</param>
        /// <param name="logger">日志服务</param>
        public static void Handle(Exception ex, LogService logger)
        {
            // 记录错误信息
            logger.LogError("Fatal Error".PadRight(40, '-'));
            logger.LogError($"Message: {ex.Message}");
            logger.LogDebug($"Type: {ex.GetType().Name}\nStackTrace:\n{ex.StackTrace}");

            // 处理 AggregateException（多异常）
            if (ex is AggregateException ae)
            {
                foreach (var innerEx in ae.Flatten().InnerExceptions)
                {
                    logger.LogError($"Inner Exception: {innerEx.Message}");
                }
            }
        }

        /// <summary>
        /// 全局异常处理程序，用于捕获未处理的异常
        /// </summary>
        /// <param name="sender">事件源</param>
        /// <param name="e">未处理异常事件参数</param>
        /// <param name="logger">日志服务</param>
        public static void GlobalExceptionHandler(object sender, UnhandledExceptionEventArgs e, LogService logger)
        {
            if (e.ExceptionObject is Exception ex)
            {
                logger.LogError($"CRITICAL ERROR: {ex.Message}");
                Environment.Exit(5); // 退出代码 5 表示未处理的异常
            }
        }
    }
}