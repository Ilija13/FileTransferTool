namespace FileTransferTool.Services.Abstractions
{
    public interface IHashCalculator
    {
        string CalculateMD5(byte[] data, int length);
        Task<string> CalculateSHA256(Stream stream);
    }
}