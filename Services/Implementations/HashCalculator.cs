using FileTransferTool.Services.Abstractions;
using System.Security.Cryptography;

namespace FileTransferTool.Services.Implementations
{
    public class HashCalculator : IHashCalculator
    {
        public string CalculateMD5(byte[] data, int length)
        {
            using (var md5 = MD5.Create())
            {
                byte[] hash = md5.ComputeHash(data, 0, length);
                return BitConverter.ToString(hash);
            }
        }

        public string CalculateSHA256(Stream stream)
        {
            using (var sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(stream);
                return BitConverter.ToString(hash);
            }
        }
    }
}