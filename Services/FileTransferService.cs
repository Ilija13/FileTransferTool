using FileTransferTool.Services.Abstractions;
using System.Collections.Concurrent;
using System.Diagnostics;

namespace FileTransferTool.Services
{
    public class FileTransferService : IDisposable
    {
        private const int CHUNK_SIZE = 1024 * 1024;
        private const int MAX_RETRY_ATTEMPTS = 3;

        private readonly IFileSystemService fileSystem;
        private readonly ILogger logger;
        private readonly IHashCalculator hashCalculator;
        private readonly bool concurency;
        private readonly ConcurrentBag<(long position, string hash)> transferedChunks = new();

        public FileTransferService(IFileSystemService fileSystem, ILogger logger, IHashCalculator hashCalculator, bool concurency)
        {
            this.fileSystem = fileSystem;
            this.logger = logger;
            this.hashCalculator = hashCalculator;
            this.concurency = concurency;
        }

        public async Task TransferFileAsync(string sourceFilePath, string destinationDirectoryPath)
        {
            string fileName = Path.GetFileName(sourceFilePath);
            string destinationFilePath = Path.Combine(destinationDirectoryPath, fileName);
            long fileSize = fileSystem.GetFileSize(sourceFilePath);
            long totalChunks = (long)Math.Ceiling((double)fileSize / CHUNK_SIZE);

            var fileInfo = new FileTransferInfo(
                FileName: fileName,
                SourceFilePath: sourceFilePath,
                DestinationFilePath: destinationFilePath,
                FileSize: fileSize,
                TotalChunks: totalChunks
            );

            using (var destinationStream = fileSystem.CreateFile(destinationFilePath))
            {
                fileSystem.SetFileLength(destinationStream, fileSize);
            }

            try
            {
                if (concurency)
                {
                    await TransferFileConcurrently(fileInfo);
                }
                else
                {
                    await TransferFileSequentially(fileInfo);
                }

                logger.LogInfo("\nAll chunks transferred successfully!\n");
            }
            catch (Exception ex)
            {
                logger.LogError($"\nTransfer failed: {ex.Message}");

                if (File.Exists(destinationFilePath))
                {
                    try
                    {
                        File.Delete(destinationFilePath);
                        logger.LogInfo("Cleaned up incomplete destination file.");
                    }
                    catch
                    {
                        logger.LogWarning("Could not delete incomplete destination file.");
                    }
                }
                throw;
            }
        }

        private async Task TransferFileSequentially(FileTransferInfo fileInfo)
        {
            var stopwatch = Stopwatch.StartNew();
            using var sourceStream = fileSystem.OpenRead(fileInfo.SourceFilePath);
            using var destinationWritingStream = new FileStream(fileInfo.DestinationFilePath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite,
                CHUNK_SIZE, useAsync: true);
            using var destinationReadingStream = new FileStream(fileInfo.DestinationFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
                CHUNK_SIZE, useAsync: true);


            for (long chunkIndex = 0; chunkIndex < fileInfo.TotalChunks; chunkIndex++)
            {
                long position = chunkIndex * CHUNK_SIZE;
                int chunkSize = (int)Math.Min(CHUNK_SIZE, fileInfo.FileSize - position);
                var (buffer, sourceHash) = await ReadAndHashSourceChunk(sourceStream, position, chunkSize);
                await WriteAndVerifyChunk(destinationWritingStream, destinationReadingStream, position, chunkSize, buffer, sourceHash, chunkIndex, fileInfo.TotalChunks);
            }
            stopwatch.Stop();
            logger.LogInfo($"\nTransfer completed in {stopwatch.Elapsed.TotalSeconds:F2} seconds");

            await VerifyFileSHA256(fileInfo);
        }

        private async Task<(byte[] buffer, string sourceHash)> ReadAndHashSourceChunk(FileStream sourceStream, long position, int chunkSize)
        {
            byte[] buffer = new byte[chunkSize];

            sourceStream.Seek(position, SeekOrigin.Begin);
            int bytesRead = await sourceStream.ReadAsync(buffer, 0, chunkSize);

            if (bytesRead != chunkSize)
            {
                throw new IOException($"Expected to read {chunkSize} bytes, but only read {bytesRead} bytes at position {position}");
            }

            string hash = hashCalculator.CalculateMD5(buffer, chunkSize);
            return (buffer, hash);
        }

        private async Task WriteAndVerifyChunk(FileStream destinationWritingStream, FileStream destinationReadingStream, long position, int chunkSize,
            byte[] buffer, string sourceHash, long chunkIndex, long totalChunks)
        {
            bool verifiedChunk = false;
            int attempts = 0;

            while (!verifiedChunk && attempts < MAX_RETRY_ATTEMPTS)
            {
                attempts++;

                await WriteChunkToDestination(destinationWritingStream, position, buffer, chunkSize);

                string destinationFileHash;
                try
                {
                    destinationFileHash = await ReadAndHashDestinationChunk(destinationReadingStream, position, chunkSize);
                }
                catch (IOException)
                {
                    logger.LogWarning($"Warning: Failed to read chunk at position {position}. Retrying... (Attempt {attempts}/{MAX_RETRY_ATTEMPTS})");
                    continue;
                }

                if (sourceHash == destinationFileHash)
                {
                    verifiedChunk = true;
                    this.transferedChunks.Add((position, destinationFileHash));
                    logger.LogInfo($"Chunk {chunkIndex + 1}/{totalChunks}: Position={position}, Hash={sourceHash} [VERIFIED]");
                }
                else
                {
                    logger.LogWarning($"Chunk {chunkIndex + 1}/{totalChunks}: Hash mismatch at position {position}! " +
                                      $"Source: {sourceHash}, Destination: {destinationFileHash}. " +
                                      $"Retry attempt {attempts}/{MAX_RETRY_ATTEMPTS}");
                }
            }

            if (!verifiedChunk)
            {
                throw new Exception($"Failed to verify chunk at position {position} after {MAX_RETRY_ATTEMPTS} attempts.");
            }
        }


        private async Task<string> ReadAndHashDestinationChunk(FileStream destinationReadingStream, long position, int chunkSize)
        {
            byte[] buffer = new byte[chunkSize];
            destinationReadingStream.Seek(position, SeekOrigin.Begin);
            int bytesRead = await destinationReadingStream.ReadAsync(buffer, 0, chunkSize);

            if (bytesRead != chunkSize)
            {
                throw new IOException($"Expected to read {chunkSize} bytes, but only read {bytesRead} bytes at position {position}");
            }

            return hashCalculator.CalculateMD5(buffer, chunkSize);
        }

        private async Task WriteChunkToDestination(FileStream destinationFileStream, long position, byte[] buffer, int chunkSize)
        {
            destinationFileStream.Seek(position, SeekOrigin.Begin);
            await destinationFileStream.WriteAsync(buffer, 0, chunkSize);
            await destinationFileStream.FlushAsync();
        }

        private async Task VerifyFileSHA256(FileTransferInfo fileInfo)
        {
            logger.LogInfo("\nVerifying entire file integrity using SHA256...");

            using var sourceStream = fileSystem.OpenRead(fileInfo.SourceFilePath);
            using var destinationStream = fileSystem.OpenRead(fileInfo.DestinationFilePath);

            string sourceFileHash = hashCalculator.CalculateSHA256(sourceStream);
            string destinationFileHash = hashCalculator.CalculateSHA256(destinationStream);

            logger.LogInfo($"Source SHA256:      {sourceFileHash}");
            logger.LogInfo($"Destination SHA256: {destinationFileHash}");

            if (sourceFileHash == destinationFileHash)
            {
                logger.LogSuccess("File integrity verified successfully!");
            }
            else
            {
                throw new Exception("File verification FAILED! SHA256 hash mismatch.");
            }
        }

        private async Task TransferFileConcurrently(FileTransferInfo fileInfo)
        {
            var stopwatch = Stopwatch.StartNew();

            int regionCount = 4;
            var regions = SplitFileIntoRegions(fileInfo.FileSize, regionCount);
            var tasks = new List<Task>(regionCount);

            for (int i = 0; i < regionCount; i++)
            {
                var region = regions[i];
                tasks.Add(TransferRegion(fileInfo, region.Start, region.End));
            }

            await Task.WhenAll(tasks);

            stopwatch.Stop();
            logger.LogInfo($"It took {stopwatch.Elapsed.TotalSeconds:F2} seconds to transfer the file concurrently");

            DisplayChunkChecksums();

            await VerifyFileSHA256(fileInfo);
        }

        private async Task TransferRegion(FileTransferInfo fileInfo, long start, long end)
        {
            using var sourceStream = fileSystem.OpenRead(fileInfo.SourceFilePath);
            using var destinationWritingStream = new FileStream(fileInfo.DestinationFilePath, FileMode.Open, FileAccess.Write, FileShare.ReadWrite,
                CHUNK_SIZE, useAsync: true);
            using var destinationReadingStream = new FileStream(fileInfo.DestinationFilePath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite,
                CHUNK_SIZE, useAsync: true);

            long position = start;
            long chunkIndex = start / CHUNK_SIZE;
            long totalChunks = fileInfo.TotalChunks;

            while (position < end)
            {
                int chunkSize = (int)Math.Min(CHUNK_SIZE, Math.Min(end - position, fileInfo.FileSize - position));

                var (buffer, sourceHash) = await ReadAndHashSourceChunk(sourceStream, position, chunkSize);

                await WriteAndVerifyChunk(destinationWritingStream, destinationReadingStream, position, chunkSize, buffer, sourceHash, chunkIndex, totalChunks);
                position += chunkSize;
                chunkIndex++;
            }
        }

        private (long Start, long End)[] SplitFileIntoRegions(long fileSize, int regionCount)
        {
            var regions = new (long Start, long End)[regionCount];
            long baseRegionSize = fileSize / regionCount;
            long remainder = fileSize % regionCount;
            long currentStart = 0;

            for (int i = 0; i < regionCount; i++)
            {
                long size = baseRegionSize + (i == regionCount - 1 ? remainder : 0);
                long start = currentStart;
                long end = start + size;
                regions[i] = (start, end);
                currentStart = end;
            }

            return regions;
        }

        private void DisplayChunkChecksums()
        {
            logger.LogInfo("=== Chunk Checksums (MD5) ===");

            var orderedChunks = transferedChunks.OrderBy(c => c.position).ToList();

            foreach (var chunk in orderedChunks)
            {
                logger.LogInfo($"position = {chunk.position}, hash = {chunk.hash}");
            }

            logger.LogInfo("");
        }

        public void Dispose()
        {

        }
    }
}