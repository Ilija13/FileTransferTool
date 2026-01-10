using System.Security.Cryptography;

namespace FileTransferTool.Services
{
    public class FileTransferService
    {
        private const int CHUNK_SIZE = 1024 * 1024;
        private const int MAX_RETRY_ATTEMPTS = 3;

        public async Task TransferFileAsync(string sourceFilePath, string destinationDirectory)
        {
            string fileName = Path.GetFileName(sourceFilePath);
            string destinationFilePath = Path.Combine(destinationDirectory, fileName);
            var fileInfo = new FileInfo(sourceFilePath);
            long fileSize = fileInfo.Length;
            long totalChunks = (fileSize + CHUNK_SIZE - 1) / CHUNK_SIZE;

            Console.WriteLine($"\n=== Starting File Transfer ===");
            Console.WriteLine($"File size: {FormatBytes(fileSize)}");
            Console.WriteLine($"Chunk size: {FormatBytes(CHUNK_SIZE)}");
            Console.WriteLine($"Total chunks: {totalChunks}\n");

            using (var destinationStream = new FileStream(destinationFilePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                destinationStream.SetLength(fileSize);
            }

            Console.WriteLine("Transferring chunks:\n");

            for (long chunkIndex = 0; chunkIndex < totalChunks; chunkIndex++)
            {
                await TransferChunk(sourceFilePath, destinationFilePath, chunkIndex, fileSize);
            }

            Console.WriteLine("\n\nAll chunks transferred successfully!");

            await VerifyCompleteFile(sourceFilePath, destinationFilePath);
        }

        private async Task TransferChunk(string sourcePath, string destinationPath, long chunkIndex, long fileSize)
        {
            long startingPosition = chunkIndex * CHUNK_SIZE;
            int chunkSize = (int)Math.Min(CHUNK_SIZE, fileSize - startingPosition);
            long totalChunks = (fileSize + CHUNK_SIZE - 1) / CHUNK_SIZE;

            var (buffer, sourceHash) = await ReadChunkFromSource(sourcePath, startingPosition, chunkSize);

            var chunkInfo = new ChunkInfo
            {
                Buffer = buffer,
                Hash = sourceHash,
                Position = startingPosition,
                Size = chunkSize,
                Index = chunkIndex,
                TotalChunks = totalChunks
            };

            await WriteAndVerifyChunk(destinationPath, chunkInfo);
        }

        private async Task<(byte[] buffer, string hash)> ReadChunkFromSource(string sourcePath, long position, int chunkSize)
        {
            byte[] buffer = new byte[chunkSize];

            using (var sourceStream = new FileStream(sourcePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                sourceStream.Seek(position, SeekOrigin.Begin);

                int bytesRead = await sourceStream.ReadAsync(buffer, 0, chunkSize);

                if (bytesRead != chunkSize)
                {
                    throw new IOException($"Expected to read {chunkSize} bytes, but only read {bytesRead} bytes at position {position}");
                }

                string hash = CalculateMD5(buffer, chunkSize);

                return (buffer, hash);
            }
        }

        private async Task WriteAndVerifyChunk(string destinationPath, ChunkInfo chunk)
        {
            bool verified = false;
            int attempts = 0;

            while (!verified && attempts < MAX_RETRY_ATTEMPTS)
            {
                attempts++;

                await WriteChunkToDestination(destinationPath, chunk);

                string? destinationHash = await ReadAndHashDestinationChunk(destinationPath, chunk.Position, chunk.Size);

                if (destinationHash == null)
                {
                    Console.WriteLine($"Warning: Failed to read chunk for verification at position {chunk.Position}. Retrying...");
                    continue;
                }

                if (chunk.Hash == destinationHash)
                {
                    verified = true;
                    Console.WriteLine($"Chunk {chunk.Index + 1}/{chunk.TotalChunks}: Position={chunk.Position}, Hash={chunk.Hash} [VERIFIED]");
                }
                else
                {
                    Console.WriteLine($"Chunk {chunk.Index + 1}: Hash mismatch at position {chunk.Position}! " +
                                    $"Source: {chunk.Hash}, Destination: {destinationHash}. " +
                                    $"Retry attempt {attempts}/{MAX_RETRY_ATTEMPTS}");
                }
            }

            if (!verified)
            {
                throw new Exception($"Failed to verify chunk at position {chunk.Position} after {MAX_RETRY_ATTEMPTS} attempts. " +
                                  $"Source hash: {chunk.Hash}");
            }
        }

        private async Task WriteChunkToDestination(string destinationPath, ChunkInfo chunk)
        {
            using (var destStream = new FileStream(destinationPath, FileMode.Open, FileAccess.Write, FileShare.Write))
            {
                destStream.Seek(chunk.Position, SeekOrigin.Begin);

                await destStream.WriteAsync(chunk.Buffer, 0, chunk.Size);
                await destStream.FlushAsync();
            }
        }

        private async Task<string?> ReadAndHashDestinationChunk(string destinationPath, long position, int chunkSize)
        {
            byte[] verifyBuffer = new byte[chunkSize];

            using (var destStream = new FileStream(destinationPath, FileMode.Open, FileAccess.Read, FileShare.Read))
            {
                destStream.Seek(position, SeekOrigin.Begin);

                int bytesRead = await destStream.ReadAsync(verifyBuffer, 0, chunkSize);

                if (bytesRead != chunkSize)
                {
                    return null;
                }

                return CalculateMD5(verifyBuffer, chunkSize);
            }
        }

        private async Task VerifyCompleteFile(string sourceFilePath, string destinationFilePath)
        {
            Console.WriteLine("Verifying entire file integrity...\n");

            Console.Write("Calculating SHA256 for source file... ");
            string sourceFileHash = await CalculateFileSHA256(sourceFilePath);
            Console.WriteLine("Done");

            Console.Write("Calculating SHA256 for destination file... ");
            string destinationFileHash = await CalculateFileSHA256(destinationFilePath);
            Console.WriteLine("Done");

            Console.WriteLine("\n=== Final File Verification (SHA256) ===");
            Console.WriteLine($"Source:      {sourceFileHash}");
            Console.WriteLine($"Destination: {destinationFileHash}");

            if (sourceFileHash == destinationFileHash)
            {
                Console.WriteLine("\n✓ File integrity verified successfully!");
            }
            else
            {
                Console.WriteLine("\nFile verification FAILED!");
                throw new Exception("Final SHA256 hash verification failed - file integrity compromised!");
            }
        }

        private async Task<string> CalculateFileSHA256(string filePath)
        {
            using (var sha256 = SHA256.Create())
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 8192, true))
            {
                byte[] hash = await Task.Run(() => sha256.ComputeHash(stream));
                return BitConverter.ToString(hash);
            }
        }

        private string CalculateMD5(byte[] data, int length)
        {
            using (var md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(data, 0, length);

                return BitConverter.ToString(hash);
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