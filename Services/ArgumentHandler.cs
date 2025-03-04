using System;
using System.IO;
using SharpEML.Logging;

namespace SharpEML.Services
{
    public static class ArgumentHandler
    {
        public static (string sourceDir, string dbPath) Parse(string[] args, LogService logger)
        {
            if (args == null || args.Length != 2)
            {
                logger.LogError("Invalid number of arguments.");
                throw new ArgumentException("Expected 2 arguments: <source_directory> <database_path>");
            }

            var sourceDir = Path.GetFullPath(args[0]); // 修正：传递数组的第一个元素
            var dbPath = Path.GetFullPath(args[1]);    // 修正：传递数组的第二个元素

            if (!Directory.Exists(sourceDir))
            {
                logger.LogError($"Source directory not found: {sourceDir}");
                throw new DirectoryNotFoundException($"Source directory not found: {sourceDir}");
            }

            return (sourceDir, dbPath);
        }
    }
}
