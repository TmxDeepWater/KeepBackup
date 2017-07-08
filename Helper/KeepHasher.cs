using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace KeepBackup.Helper
{
    static class KeepHasher
    {
        public static string GetHash(FileInfo file)
        {
            Program.log.Debug("hashing " + file.FullName);
            StringBuilder hashSB = new StringBuilder();

            byte[] hash;
            using (SHA256Managed sha = new SHA256Managed())
            using (FileStream fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                hash = sha.ComputeHash(fs);

            foreach (byte b in hash)
                hashSB.Append(b.ToString("x2"));

            return hashSB.ToString();
        }

        public static string GetHash(Stream stream)
        {
            StringBuilder hashSB = new StringBuilder();

            byte[] hash;
            using (SHA256Managed sha = new SHA256Managed())
                hash = sha.ComputeHash(stream);

            foreach (byte b in hash)
                hashSB.Append(b.ToString("x2"));

            return hashSB.ToString();
        }
    }
}
