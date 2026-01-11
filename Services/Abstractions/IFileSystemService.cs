namespace FileTransferTool.Services.Abstractions
{
    public interface IFileSystemService
    {
        Stream OpenRead(string path);
        Stream OpenWrite(string path);
        Stream CreateFile(string path);
        long GetFileSize(string path);
        void SetFileLength(Stream stream, long length);
    }
}