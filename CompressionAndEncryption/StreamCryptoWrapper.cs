using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace KeepBackup.CompressionAndEncryption
{
    class StreamCryptoWrapper : IDisposable
    {
        
        private enum ModeEnum { Undefinided, Encrypt, Decrypt };

        private readonly ModeEnum _Mode = ModeEnum.Undefinided;
        private readonly string _Password;
        private readonly string SALT;

        private readonly Stream _Stream;
        private readonly CryptoStream _CryptoStream;

        private readonly Rfc2898DeriveBytes _DerivedBytes;
        private readonly AesManaged _Aes;
        private readonly ICryptoTransform _CryptoTransform;

        private StreamCryptoWrapper(ModeEnum mode, Stream stream, string password, string salt)
        {
            _Mode = mode;
            _Password = password;
            _Stream = stream;
            SALT = salt;

            byte[] saltBytes = Encoding.UTF8.GetBytes(SALT);

            _DerivedBytes = new Rfc2898DeriveBytes(password, saltBytes);

            _Aes = new AesManaged();
            _Aes.Key = _DerivedBytes.GetBytes(_Aes.Key.Length);
            _Aes.IV = _DerivedBytes.GetBytes(_Aes.IV.Length);            

            CryptoStreamMode streamMode;

            if (mode == ModeEnum.Encrypt)
            {
                _CryptoTransform = _Aes.CreateEncryptor();
                streamMode = CryptoStreamMode.Write;
            }
            else if (mode == ModeEnum.Decrypt)
            {
                _CryptoTransform = _Aes.CreateDecryptor();
                streamMode = CryptoStreamMode.Read;
            }
            else
                throw new ArgumentOutOfRangeException("mode");

            _CryptoStream = new CryptoStream(_Stream, _CryptoTransform, streamMode);
        }

        public CryptoStream CryptoStream
        {
            get { return _CryptoStream; }
        }

        public static StreamCryptoWrapper Encrypt(Stream target, string password, string salt)
        {
            return new StreamCryptoWrapper(ModeEnum.Encrypt, target, password, salt);
        }

        public static StreamCryptoWrapper Decrypt(Stream source, string password, string salt)
        {
            return new StreamCryptoWrapper(ModeEnum.Decrypt, source, password, salt);
        }

        public void Dispose()
        {
            _DerivedBytes.Dispose();
            _CryptoStream.Dispose();
            _CryptoTransform.Dispose();
            _Aes.Dispose();
        }
    }
}
