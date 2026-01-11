using FileTransferTool.Services.Abstractions;

namespace FileTransferTool.Services.Implementations
{
    public class FileSystemService : IFileSystemService
    {
        public Stream OpenRead(string path)
        {
            return new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        }

        public Stream OpenWrite(string path)
        {
            return new FileStream(path, FileMode.Open, FileAccess.Write, FileShare.Write);
        }

        public Stream CreateFile(string path)
        {
            return new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        }

        public long GetFileSize(string path)
        {
            return new FileInfo(path).Length;
        }

        public void SetFileLength(Stream stream, long length)
        {
            stream.SetLength(length);
        }
    }
}