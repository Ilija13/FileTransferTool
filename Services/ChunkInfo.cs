namespace FileTransferTool.Services
{
    public class ChunkInfo
    {
        public byte[] Buffer { get; set; }
        public string Hash { get; set; }
        public long Position { get; set; }
        public int Size { get; set; }
        public long Index { get; set; }
        public long TotalChunks { get; set; }
    }
}