using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MimeKit;
using MimeKit.IO;
using SharpEML.Logging;
using SharpEML.Core.Models;
using MimeKit.Utils;
using MimeKit.Encodings;
using MimeKit.Text;
using sharpeml.Core;


namespace SharpEML.Core
{
    public class EmailProcessor : IDisposable
    {
        public int ProcessedCount => _processedCount;
        public long TotalBytes => _totalBytes;

        private const int BufferSize = 4096;
        private const int MaxPoolSize = 20;
        private readonly string _sourceDir;
        private readonly string _dbPath;
        private readonly DatabaseHandler _dbHandler;
        private int _totalFiles;
        private int _processedCount;
        private long _totalBytes;

        private readonly LogService _logger;
        private readonly CancellationTokenSource _cts = new CancellationTokenSource();
        private readonly BlockingCollection<string> _fileQueue = new BlockingCollection<string>(new ConcurrentQueue<string>(), 1000);

        // 对象池配置
        private static readonly ConcurrentQueue<FileStream> _streamPool = new ConcurrentQueue<FileStream>();
        private static readonly ConcurrentQueue<MimeParser> _parserPool = new ConcurrentQueue<MimeParser>();

        public EmailProcessor(string sourceDir, string dbPath, LogService logger)
        {
            _sourceDir = Path.GetFullPath(sourceDir);
            _dbPath = Path.GetFullPath(dbPath);
            _dbHandler = new DatabaseHandler(_dbPath);
            _logger = logger;

            _logger.LogProcessStart("Processor initialized");
        }

        public async Task ProcessEmailsAsync()
        {
            try
            {
                var consumers = Enumerable.Range(0, Environment.ProcessorCount)
                    .Select(_ => Task.Run(() => ProcessFileBatches()))
                    .ToArray();

                var discoveryTask = Task.Run(() => DiscoverFiles(_sourceDir));
                await Task.WhenAll(discoveryTask);
                _fileQueue.CompleteAdding();

                await Task.WhenAll(consumers);

                while (_fileQueue.Count > 0)
                    await Task.Delay(100);

                // **确保所有批量插入已完成后再执行 SaveToFile**
                lock (_dbHandler)
                {
                    //Console.WriteLine("[DEBUG] Calling SaveToFile()...");
                    _dbHandler.SaveToFile();
                }

                _logger.LogInfo($"Processed file count: {_processedCount}, Total files in queue: {_totalFiles}");
            }
            finally
            {
                _dbHandler?.Dispose();
            }
        }

        private void DiscoverFiles(string sourceDir)
        {
            var scanTimer = Stopwatch.StartNew();
            try
            {
                foreach (var file in Directory.EnumerateFiles(sourceDir, "*.eml", SearchOption.AllDirectories))
                {
                    _fileQueue.Add(file);
                    Interlocked.Increment(ref _totalFiles);
                }
                _logger.LogStep("File Scanning", scanTimer.Elapsed.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Directory scan failed: {ex.Message}");
            }
        }

        private void ProcessFileBatches()
        {
            var batch = new List<EmailData>(DatabaseHandler.BatchSize);
            var batchTimer = Stopwatch.StartNew();

            try
            {
                foreach (var filePath in _fileQueue.GetConsumingEnumerable())
                {
                    try
                    {
                        var data = ParseEmail(filePath);
                        if (data != null)
                        {
                            batch.Add(data);

                            // 原子更新统计
                            Interlocked.Increment(ref _processedCount);
                            Interlocked.Add(ref _totalBytes, data.FileSize);

                            if (batch.Count >= DatabaseHandler.BatchSize)
                            {
                                ExecuteBatchInsert(batch);
                                _logger.LogBatchProcess("Batch insert", batchTimer.Elapsed.TotalMilliseconds);

                                batchTimer.Restart();
                                batch.Clear();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError($"Failed to process file '{filePath}': {ex.Message}");
                    }
                }

                if (batch.Count > 0)
                {
                    ExecuteBatchInsert(batch);
                    _logger.LogBatchProcess("Final batch insert", batchTimer.Elapsed.TotalMilliseconds);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError($"Batch processing failed: {ex.Message}");
                throw;
            }
        }

        private void ExecuteBatchInsert(List<EmailData> batch)
        {
            var batchTimer = Stopwatch.StartNew();
            try
            {
                _dbHandler.BulkInsert(batch);
                _logger.LogStep("Insert Mail Data", batchTimer.Elapsed.TotalMilliseconds);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Insert Mail Data failed: {ex.Message}");
                throw;
            }
        }

        private EmailData ParseEmail(string filePath)
        {
            FileStream stream = null;
            MimeParser parser = null;

            try
            {
                // **确保每次读取新的 EML 文件时，流和解析器是干净的**
                stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.SequentialScan);
                stream.Seek(0, SeekOrigin.Begin); // **确保流从头开始读取**

                var hashGenerator = new FileHashGenerator();
                string fileHash = hashGenerator.GenerateFileHash(filePath);

                parser = new MimeParser(stream, MimeFormat.Default);
                var headers = parser.ParseHeaders(); // **解析邮件头**

                var data = new EmailData
                {
                    FileName = Path.GetFileName(filePath),
                    Subject = ExtractSubject(headers), // **解析标题**
                    FileHash = fileHash,   // **存储 Hash**
                    FileSize = new FileInfo(filePath).Length,
                    HasAttachments = DetectAttachments(headers),
                    CreationTime = ParseDateTime(headers[HeaderId.Date]),
                    Sender = ExtractSender(headers),
                    Recipients = ExtractRecipients(headers),
                    Attachments = new List<AttachmentInfo>()
                };

                if (data.HasAttachments)
                {
                    ParseAttachments(stream, data);
                }

                //Console.WriteLine($"[DEBUG] Parsed Email: {data.FileName}, Subject: {data.Subject}");
                return data;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Error parsing email '{filePath}': {ex.Message}");
                return null;
            }
            finally
            {
                // **释放流，防止数据残留**
                stream?.Dispose();
                parser?.SetStream(Stream.Null);
            }
        }





        private void ParseAttachments(Stream stream, EmailData data)
        {
            stream.Seek(0, SeekOrigin.Begin); // **确保流从头开始读取**
            var parser = new MimeParser(stream, MimeFormat.Default);

            try
            {
                var message = parser.ParseMessage();
                var attachmentNames = new List<string>();
                var attachmentSizes = new List<string>();

                foreach (var attachment in message.Attachments.OfType<MimePart>())
                {
                    var attachmentInfo = new AttachmentInfo
                    {
                        Name = attachment.FileName ?? "Unknown",
                        Size = CalculateAttachmentSize(attachment)
                    };

                    data.Attachments.Add(attachmentInfo);
                    attachmentNames.Add(attachmentInfo.Name);
                    attachmentSizes.Add(attachmentInfo.Size.ToString());

                    //Console.WriteLine($"[DEBUG] Parsed Attachment: {attachmentInfo.Name}, Size: {attachmentInfo.Size} bytes");
                }

                // **确保 `AttachmentNames` 和 `AttachmentSizes` 不为空**
                data.AttachmentNames = attachmentNames.Count > 0 ? string.Join(";", attachmentNames) : "";
                data.AttachmentSizes = attachmentSizes.Count > 0 ? string.Join(";", attachmentSizes) : "";
            }
            finally
            {
                parser.SetStream(Stream.Null); // **释放 `MimeParser`**
            }
        }


        // 保持以下方法不变
        private DateTime ParseDateTime(string headerValue)
        {
            if (string.IsNullOrWhiteSpace(headerValue))
            {
                //_logger.LogWarning("Empty date header");
                return DateTime.MinValue;
            }

            try
            {
                return MimeKit.Utils.DateUtils.TryParse(headerValue, out var result)
                    ? result.ToUniversalTime().DateTime
                    : DateTime.MinValue;
            }
            catch (Exception ex)
            {
                _logger.LogError($"Critical error while parsing date: {headerValue} \n {ex}");
                return DateTime.MinValue;
            }
        }
        private string ExtractSubject(HeaderList headers)
        {
            var subjects = headers
                .Where(h => h.Field.Equals("Subject", StringComparison.OrdinalIgnoreCase))
                .Select(h => h.Value)
                .ToList();

            return subjects.Count > 0 ? subjects.Last().Trim() : "No Subject";
        }





        private string ExtractRecipients(HeaderList headers)
        {
            var recipients = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var headerId in new[] { HeaderId.To, HeaderId.Cc, HeaderId.Bcc })
            {
                var header = headers[headerId];
                if (header != null)
                {
                    try
                    {
                        var list = MimeKit.InternetAddressList.Parse(header);
                        foreach (var addr in list.Mailboxes)
                        {
                            recipients.Add($"<{addr.Address}>");
                        }
                    }
                    catch
                    {
                        // 忽略解析失败的情况
                    }
                }
            }

            return recipients.Any() ? string.Join(", ", recipients.OrderBy(x => x)) : "No recipients";
        }

        private string ExtractSender(HeaderList headers)
        {
            var fromHeader = headers[HeaderId.From];
            if (fromHeader == null)
                return "No sender";

            try
            {
                var addresses = MimeKit.InternetAddressList.Parse(fromHeader);
                return string.Join(", ", addresses.Mailboxes.Select(m => $"<{m.Address}>"));
            }
            catch
            {
                return "Invalid sender";
            }
        }

        private HeaderList ParseHeaders(Stream stream)
        {
            var parser = new MimeParser(stream, MimeFormat.Default);
            return parser.ParseHeaders();
        }
        private bool DetectAttachments(HeaderList headers)
        {
            // **检查 `X-MS-Has-Attach: yes`**
            if (headers["X-MS-Has-Attach"]?.Equals("yes", StringComparison.OrdinalIgnoreCase) == true)
            {
                return true;
            }

            // **检查 `Content-Disposition` 和 `Content-Type`**
            return headers[HeaderId.ContentDisposition]?.Contains("attachment") == true ||
                   headers[HeaderId.ContentType]?.IndexOf("multipart/mixed", StringComparison.OrdinalIgnoreCase) >= 0;
        }
        private long CalculateAttachmentSize(MimePart part)
        {
            using (var measuring = new MeasuringStream())
            {
                part.Content.DecodeTo(measuring);
                return measuring.Length;
            }
        }

        public void Dispose()
        {
            _cts?.Cancel();

            // 清理对象池
            while (_streamPool.TryDequeue(out var stream))
            {
                stream.Dispose();
            }
            while (_parserPool.TryDequeue(out var parser))
            {
                parser.SetStream(Stream.Null);
            }

            _fileQueue?.Dispose();
            _logger.LogDebug("Processor resources released");
        }
    }
}