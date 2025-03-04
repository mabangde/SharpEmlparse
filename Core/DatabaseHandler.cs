using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using SharpEML.Core.Models;

namespace SharpEML.Core
{
    public class DatabaseHandler : IDisposable
    {
        public const int BatchSize = 500;
        private readonly string _dbPath;
        private SQLiteConnection _memoryConnection;


        public DatabaseHandler(string dbPath)
        {
            _dbPath = dbPath;

            // **初始化内存数据库**
            _memoryConnection = new SQLiteConnection("Data Source=:memory:;Version=3;");
            _memoryConnection.Open();
            SetupDatabase(_memoryConnection);

            // **确保文件数据库也创建 `emails` 表**
            if (!File.Exists(dbPath))
            {
                SQLiteConnection.CreateFile(dbPath);
            }

            using (var fileConn = new SQLiteConnection($"Data Source={dbPath};Version=3;"))
            {
                fileConn.Open();
                SetupDatabase(fileConn);
            }
        }

        private void SetupDatabase(SQLiteConnection connection)
        {
            using (var command = new SQLiteCommand(connection))
            {
                command.CommandText = @"
            PRAGMA journal_mode = WAL;
            PRAGMA synchronous = NORMAL;
            PRAGMA locking_mode = NORMAL;
            PRAGMA temp_store = MEMORY;
            PRAGMA cache_size = 5000;
            PRAGMA page_size = 4096;
            
            CREATE TABLE IF NOT EXISTS emails (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                file_name TEXT NOT NULL,
                file_hash TEXT NOT NULL,  -- 新增 Hash 字段
                file_size INTEGER,
                has_attachments INTEGER NOT NULL,
                sender TEXT, 
                recipients TEXT,
                subject TEXT,
                creation_time TEXT NOT NULL DEFAULT '2000-01-01 00:00:00',
                attachment_names TEXT,
                attachment_sizes TEXT)";
                command.ExecuteNonQuery();
            }
        }



        public void BulkInsert(List<EmailData> batch)
        {
            try
            {
                using (var transaction = _memoryConnection.BeginTransaction())
                {
                    using (var cmd = BuildInsertCommand(_memoryConnection))
                    {
                        foreach (var item in batch)
                        {
                            FillCommandParameters(cmd, item);
                            cmd.ExecuteNonQuery();
                        }
                    }
                    transaction.Commit();
                }

                // **检查插入后的数据条数**
                using (var checkCmd = new SQLiteCommand("SELECT COUNT(*) FROM emails", _memoryConnection))
                {
                    long count = (long)checkCmd.ExecuteScalar();
                    //Console.WriteLine($"[DEBUG] Rows in _memoryConnection after BulkInsert: {count}");
                }
            }
            catch (Exception ex)
            {
                throw new Exception($"Bulk insert failed: {ex.Message}");
            }
        }

        private SQLiteCommand BuildInsertCommand(SQLiteConnection conn)
        {
            var cmd = new SQLiteCommand(
            "INSERT INTO emails (file_name, file_size, file_hash, has_attachments, sender, recipients, subject, creation_time, attachment_names, attachment_sizes) " +
            "VALUES (@fn, @fs, @fh, @ha, @sd, @rc, @sj, @ct, @an, @as)", conn);

            cmd.Parameters.Add("@fn", DbType.String);
            cmd.Parameters.Add("@fs", DbType.Int64);
            cmd.Parameters.Add("@fh", DbType.String); // **新增 Hash 参数**
            cmd.Parameters.Add("@ha", DbType.Int32);
            cmd.Parameters.Add("@sd", DbType.String);
            cmd.Parameters.Add("@rc", DbType.String);
            cmd.Parameters.Add("@sj", DbType.String);
            cmd.Parameters.Add("@ct", DbType.String);
            cmd.Parameters.Add("@an", DbType.String);
            cmd.Parameters.Add("@as", DbType.String);

            return cmd;
        }

        private void FillCommandParameters(SQLiteCommand cmd, EmailData item)
        {
            cmd.Parameters["@fn"].Value = item.FileName ?? (object)DBNull.Value;
            cmd.Parameters["@fs"].Value = item.FileSize;
            cmd.Parameters["@fh"].Value = item.FileHash ?? (object)DBNull.Value; // **存入 Hash**
            cmd.Parameters["@ha"].Value = item.HasAttachments ? 1 : 0;
            cmd.Parameters["@sd"].Value = item.Sender ?? (object)DBNull.Value;
            cmd.Parameters["@rc"].Value = item.Recipients ?? (object)DBNull.Value;
            cmd.Parameters["@sj"].Value = item.Subject ?? (object)DBNull.Value;
            cmd.Parameters["@ct"].Value = item.CreationTime.ToString("yyyy-MM-dd HH:mm:ss");

            var (names, sizes) = FormatAttachments(item);
            cmd.Parameters["@an"].Value = names;
            cmd.Parameters["@as"].Value = sizes;
        }

        private (object names, object sizes) FormatAttachments(EmailData item)
        {
            if (!item.HasAttachments || !item.Attachments.Any())
                return (DBNull.Value, DBNull.Value);

            return (
                string.Join(";", item.Attachments.Select(a => a.Name)),
                string.Join(";", item.Attachments.Select(a => a.Size.ToString()))
            );
        }

        public void SaveToFile()
        {
            try
            {
               // Console.WriteLine("[DEBUG] 开始保存数据到 s.db");

                using (var fileConn = new SQLiteConnection($"Data Source={_dbPath};Version=3;BusyTimeout=5000;"))
                {
                    fileConn.Open();

                    using (var transaction = fileConn.BeginTransaction())
                    {
                        using (var readCmd = new SQLiteCommand("SELECT * FROM emails", _memoryConnection))
                        using (var reader = readCmd.ExecuteReader())
                        {
                            using (var insertCmd = new SQLiteCommand(fileConn))
                            {
                                insertCmd.CommandText = @"INSERT INTO emails 
                                          (file_name, file_size, file_hash, has_attachments, sender, recipients, subject, 
                                           creation_time, attachment_names, attachment_sizes) 
                                          VALUES 
                                          (@file_name, @file_size, @file_hash, @has_attachments, @sender, @recipients, @subject, 
                                           @creation_time, @attachment_names, @attachment_sizes)";

                                insertCmd.Parameters.Add(new SQLiteParameter("@file_name"));
                                insertCmd.Parameters.Add(new SQLiteParameter("@file_size"));
                                insertCmd.Parameters.Add(new SQLiteParameter("@file_hash"));
                                insertCmd.Parameters.Add(new SQLiteParameter("@has_attachments"));
                                insertCmd.Parameters.Add(new SQLiteParameter("@sender"));
                                insertCmd.Parameters.Add(new SQLiteParameter("@recipients"));
                                insertCmd.Parameters.Add(new SQLiteParameter("@subject"));
                                insertCmd.Parameters.Add(new SQLiteParameter("@creation_time"));
                                insertCmd.Parameters.Add(new SQLiteParameter("@attachment_names"));
                                insertCmd.Parameters.Add(new SQLiteParameter("@attachment_sizes"));

                                int rowsCopied = 0;

                                while (reader.Read())
                                {
                                    insertCmd.Parameters["@file_name"].Value = reader["file_name"];
                                    insertCmd.Parameters["@file_size"].Value = reader["file_size"];
                                    insertCmd.Parameters["@file_hash"].Value = reader["file_hash"]; // **确保 file_hash 被正确赋值**
                                    insertCmd.Parameters["@has_attachments"].Value = reader["has_attachments"];
                                    insertCmd.Parameters["@sender"].Value = reader["sender"];
                                    insertCmd.Parameters["@recipients"].Value = reader["recipients"];
                                    insertCmd.Parameters["@subject"].Value = reader["subject"];
                                    insertCmd.Parameters["@creation_time"].Value = reader["creation_time"];
                                    insertCmd.Parameters["@attachment_names"].Value = reader["attachment_names"];
                                    insertCmd.Parameters["@attachment_sizes"].Value = reader["attachment_sizes"];

                                    insertCmd.ExecuteNonQuery();
                                    rowsCopied++;
                                }

                                //Console.WriteLine($"[DEBUG] 成功复制 {rowsCopied} 行数据到 s.db");
                            }
                        }

                        transaction.Commit();
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] SaveToFile 失败: {ex.Message}");
                throw new Exception($"SaveToFile 失败: {ex.Message}", ex);
            }
        }

        private List<AttachmentInfo> ParseAttachmentData(string names, string sizes)
        {
            var attachments = new List<AttachmentInfo>();

            if (string.IsNullOrEmpty(names) || string.IsNullOrEmpty(sizes))
                return attachments;

            var nameArray = names.Split(';');
            var sizeArray = sizes.Split(';');

            for (int i = 0; i < Math.Min(nameArray.Length, sizeArray.Length); i++)
            {
                if (long.TryParse(sizeArray[i], out var size))
                {
                    attachments.Add(new AttachmentInfo
                    {
                        Name = nameArray[i],
                        Size = size
                    });
                }
            }
            return attachments;
        }

        private void ExecuteNonQuery(SQLiteConnection conn, string sql)
        {
            using (var cmd = new SQLiteCommand(sql, conn))
            {
                cmd.ExecuteNonQuery();
            }
        }

        public void Dispose()
        {
            _memoryConnection?.Dispose();
            // 移除文件连接释放
        }
    }
}