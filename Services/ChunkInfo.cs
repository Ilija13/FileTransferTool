namespace FileTransferTool.Services
{
    public class ChunkInfo
    {
        public required byte[] Buffer { get; set; }
        public required string Hash { get; set; }
        public long Position { get; set; }
        public int Size { get; set; }
        public long Index { get; set; }
        public long TotalChunks { get; set; }
    }
}