namespace FileTransferTool.Services.Abstractions
{
    public interface IFileSystemService
    {
        FileStream OpenRead(string path);
        FileStream OpenWrite(string path);
        FileStream CreateFile(string path);
        long GetFileSize(string path);
        void SetFileLength(Stream stream, long length);
    }
}