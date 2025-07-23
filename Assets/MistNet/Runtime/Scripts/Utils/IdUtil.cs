using System.Security.Cryptography;

namespace MistNet.Utils
{
    public static class IdUtil
    {
        public const int BitLength = 128;
        public static byte[] ToBytes(string id)
        {
            using var sha1 = SHA1.Create();
            return sha1.ComputeHash(System.Text.Encoding.UTF8.GetBytes(id));
        }
    }
}
