using System;
using System.IO;
using SharpEML.Logging;

namespace SharpEML.Services
{
    public static class ArgumentValidator
    {
        public static bool Validate(string[] args, LogService logger)
        {
            if (args == null || args.Length != 2)
            {
                logger.LogError("Invalid number of arguments.");
                logger.LogError("Usage: sharpeml <source_directory> <database_path>");
                logger.LogError("Example: sharpeml C:\\emails C:\\data\\emails.db");
                return false;
            }

            var sourceDir = Path.GetFullPath(args[0]); // 修正：传递数组的第一个元素
            if (!Directory.Exists(sourceDir))
            {
                logger.LogError($"Source directory not found: {sourceDir}");
                return false;
            }

            return true;
        }
    }
}
