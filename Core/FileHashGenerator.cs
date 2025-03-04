using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.IO.Hashing;

namespace sharpeml.Core
{
    public class FileHashGenerator
    {
        private const int SampleSize = 4096;    // 每段采样大小（4KB）
        private const int SegmentCount = 4;     // 采样段数量
        private const long SmallFileThreshold = 1024 * 1024; // 1MB以下文件全哈希

        public string GenerateFileHash(string filePath)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                long fileSize = fileInfo.Length;

                byte[] hashData = fileSize <= SmallFileThreshold ?
                    CalculateFullHash(filePath) :
                    CalculateSampledHash(filePath, fileSize);

                var hashBytes = XxHash64.Hash(hashData);
                return BitConverter.ToString(hashBytes).Replace("-", "") + fileSize.ToString("X16");
            }
            catch
            {
                // 忽略所有错误并返回空字符串或其他指示错误的值
                return string.Empty;
            }
        }

        private byte[] CalculateFullHash(string filePath)
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096))
            {
                using (var memoryStream = new MemoryStream())
                {
                    stream.CopyTo(memoryStream);
                    return XxHash64.Hash(memoryStream.ToArray());
                }
            }
        }

        private byte[] CalculateSampledHash(string filePath, long fileSize)
        {
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096))
            {
                var buffer = new byte[SampleSize * SegmentCount];

                ReadSegment(stream, buffer, 0, 0);
                ReadSegment(stream, buffer, SampleSize, fileSize / 2);
                ReadSegment(stream, buffer, SampleSize * 2, fileSize - SampleSize);

                var sizeBytes = BitConverter.GetBytes(fileSize);
                var combined = new byte[sizeBytes.Length + buffer.Length];
                Buffer.BlockCopy(sizeBytes, 0, combined, 0, sizeBytes.Length);
                Buffer.BlockCopy(buffer, 0, combined, sizeBytes.Length, buffer.Length);

                return combined;
            }
        }

        private void ReadSegment(FileStream stream, byte[] buffer, int bufferOffset, long fileOffset)
        {
            stream.Seek(Math.Max(0, fileOffset), SeekOrigin.Begin);
            int bytesRead = stream.Read(buffer, bufferOffset, SampleSize);

            if (bytesRead < SampleSize)
            {
                Array.Clear(buffer, bufferOffset + bytesRead, SampleSize - bytesRead);
            }
        }
    }

}
