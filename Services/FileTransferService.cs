using FileTransferTool.Services.Abstractions;

namespace FileTransferTool.Services
{
    public class FileTransferService
    {
        private const int CHUNK_SIZE = 1024 * 1024;
        private const int MAX_RETRY_ATTEMPTS = 3;

        private readonly List<ChunkInfo> transferedChunks;
        private readonly IFileSystemService fileSystem;
        private readonly ILogger logger;
        private readonly IHashCalculator hashCalculator;

        public FileTransferService(IFileSystemService fileSystem, ILogger logger, IHashCalculator hashCalculator)
        {
            this.fileSystem = fileSystem;
            this.logger = logger;
            this.hashCalculator = hashCalculator;
            this.transferedChunks = new List<ChunkInfo>();
        }

        public async Task TransferFileAsync(string sourceFilePath, string destinationDirectory)
        {
            string fileName = Path.GetFileName(sourceFilePath);
            string destinationFilePath = Path.Combine(destinationDirectory, fileName);
            long fileSize = fileSystem.GetFileSize(sourceFilePath);
            long totalChunks = (fileSize + CHUNK_SIZE - 1) / CHUNK_SIZE;

            logger.LogInfo($"\n=== Starting File Transfer ===");
            logger.LogInfo($"File size: {FormatBytes(fileSize)}");
            logger.LogInfo($"Chunk size: {FormatBytes(CHUNK_SIZE)}");
            logger.LogInfo($"Total chunks: {totalChunks}\n");

            using (var destinationStream = fileSystem.CreateFile(destinationFilePath))
            {
                fileSystem.SetFileLength(destinationStream, fileSize);
            }

            logger.LogInfo("Transferring chunks:\n");

            for (long chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++)
            {
                await TransferChunk(sourceFilePath, destinationFilePath, chunkIndex, fileSize);
            }

            logger.LogInfo("\n\nAll chunks transferred successfully!");

            DisplayChunkChecksums();

            await VerifyCompleteFile(sourceFilePath, destinationFilePath);
        }

        private async Task TransferChunk(string sourceFilePath, string destinationFilePath, long chunkIndex, long fileSize)
        {
            long startingPosition = chunkIndex * CHUNK_SIZE;
            int chunkSize = (int)Math.Min(CHUNK_SIZE, fileSize - startingPosition);
            long totalChunks = (fileSize + CHUNK_SIZE - 1) / CHUNK_SIZE;

            var (buffer, sourceHash) = await ReadAndHashSourceChunk(sourceFilePath, startingPosition, chunkSize);

            var chunkInfo = new ChunkInfo
            {
                Buffer = buffer,
                Hash = sourceHash,
                Position = startingPosition,
                Size = chunkSize,
                Index = chunkIndex,
                TotalChunks = totalChunks
            };

            await WriteAndVerifyChunk(destinationFilePath, chunkInfo);
            transferedChunks.Add(chunkInfo);
        }

        private async Task<(byte[] buffer, string hash)> ReadAndHashSourceChunk(string sourceFilePath, long position, int chunkSize)
        {
            byte[] buffer = new byte[chunkSize];

            using (var sourceFileStream = fileSystem.OpenRead(sourceFilePath))
            {
                sourceFileStream.Seek(position, SeekOrigin.Begin);

                int bytesRead = await sourceFileStream.ReadAsync(buffer, 0, chunkSize);

                if (bytesRead != chunkSize)
                {
                    throw new IOException($"Expected to read {chunkSize} bytes, but only read {bytesRead} bytes at position {position}");
                }

                string hash = hashCalculator.CalculateMD5(buffer, chunkSize);

                return (buffer, hash);
            }
        }

        private async Task WriteAndVerifyChunk(string destinationFilePath, ChunkInfo chunkInfo)
        {
            bool verifiedChunk = false;
            int attempts = 0;

            while (!verifiedChunk && attempts < MAX_RETRY_ATTEMPTS)
            {
                attempts++;

                await WriteChunkToDestination(destinationFilePath, chunkInfo);

                string? destinationFileHash = await ReadAndHashDestinationChunk(destinationFilePath, chunkInfo.Position, chunkInfo.Size);

                if (destinationFileHash == null)
                {
                    logger.LogWarning($"Warning: Failed to read chunk for verification at position {chunkInfo.Position}. Retrying...");
                    continue;
                }

                if (chunkInfo.Hash == destinationFileHash)
                {
                    verifiedChunk = true;
                    logger.LogInfo($"Chunk {chunkInfo.Index + 1}/{chunkInfo.TotalChunks}: Position={chunkInfo.Position}, Hash={chunkInfo.Hash} [VERIFIED]");
                }
                else
                {
                    logger.LogWarning($"Chunk {chunkInfo.Index + 1}: Hash mismatch at position {chunkInfo.Position}! " +
                                    $"Source: {chunkInfo.Hash}, Destination: {destinationFileHash}. " +
                                    $"Retry attempt {attempts}/{MAX_RETRY_ATTEMPTS}");
                }
            }

            if (!verifiedChunk)
            {
                throw new Exception($"Failed to verify chunk at position {chunkInfo.Position} after {MAX_RETRY_ATTEMPTS} attempts. " +
                                  $"Source hash: {chunkInfo.Hash}");
            }
        }

        private async Task WriteChunkToDestination(string destinationFilePath, ChunkInfo chunkInfo)
        {
            using (var destinationFileStream = fileSystem.OpenWrite(destinationFilePath))
            {
                destinationFileStream.Seek(chunkInfo.Position, SeekOrigin.Begin);

                await destinationFileStream.WriteAsync(chunkInfo.Buffer, 0, chunkInfo.Size);
                await destinationFileStream.FlushAsync();
            }
        }

        private async Task<string?> ReadAndHashDestinationChunk(string destinationFilePath, long position, int chunkSize)
        {
            byte[] verifyBuffer = new byte[chunkSize];

            using (var destinationFileStream = fileSystem.OpenRead(destinationFilePath))
            {
                destinationFileStream.Seek(position, SeekOrigin.Begin);

                int bytesRead = await destinationFileStream.ReadAsync(verifyBuffer, 0, chunkSize);

                if (bytesRead != chunkSize)
                {
                    return null;
                }

                return hashCalculator.CalculateMD5(verifyBuffer, chunkSize);
            }
        }

        private async Task VerifyCompleteFile(string sourceFilePath, string destinationFilePath)
        {
            logger.LogInfo("Verifying entire file integrity...\n");

            logger.LogInfo("Calculating SHA256 for source file... ");
            string sourceFileHash = await CalculateFileSHA256(sourceFilePath);
            logger.LogInfo("Done");

            logger.LogInfo("Calculating SHA256 for destination file... ");
            string destinationFileHash = await CalculateFileSHA256(destinationFilePath);
            logger.LogInfo("Done");

            logger.LogInfo("\n=== Final File Verification (SHA256) ===");
            logger.LogInfo($"Source:      {sourceFileHash}");
            logger.LogInfo($"Destination: {destinationFileHash}");

            if (sourceFileHash == destinationFileHash)
            {
                logger.LogSuccess("\n File integrity verified successfully!");
            }
            else
            {
                logger.LogError("\nFile verification FAILED!");
                throw new Exception("Final SHA256 hash verification failed - file integrity compromised!");
            }
        }

        private void DisplayChunkChecksums()
        {
            logger.LogInfo("=== Chunk Checksums (MD5) ===");

            var orderedChunks = transferedChunks.OrderBy(c => c.Position).ToList();

            foreach (var chunk in orderedChunks)
            {
                logger.LogInfo($"{chunk.Index + 1}) position = {chunk.Position}, hash = {chunk.Hash}");
            }

            logger.LogInfo("");
        }

        private async Task<string> CalculateFileSHA256(string filePath)
        {
            using (var stream = fileSystem.OpenRead(filePath))
            {
                return await hashCalculator.CalculateSHA256(stream);
            }
        }

        private string FormatBytes(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB", "TB" };
            double length = bytes;
            int order = 0;

            while (length >= 1024 && order < sizes.Length - 1)
            {
                order++;
                length = length / 1024;
            }

            return $"{length:0.##} {sizes[order]}";
        }
    }
}