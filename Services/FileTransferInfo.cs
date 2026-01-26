namespace FileTransferTool.Services
{
    public record FileTransferInfo(
        string FileName,
        string SourceFilePath,
        string DestinationFilePath,
        long FileSize,
        long TotalChunks
    );
}