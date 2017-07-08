using KeepBackup.CompressionAndEncryption;
using KeepBackup.OriginInventory;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KeepBackup.Helper;

namespace KeepBackup.Storage
{
    internal class FileJob : AbstractFileJob
    {
        public enum ModeEnum { Undefinied, CompressEncrypt, DecryptDecompress }
        public enum SizeEnum { Undefinied, Small, Medium, Large }
        
        private readonly ModeEnum _Mode = ModeEnum.Undefinied;
        private readonly string _Password;
        private readonly string _Salt;
        private readonly IFile _SourceFile;

        private readonly bool _CompressToMemory = false;
        private readonly bool _EncryptToMemory = false;

        private readonly IList<string> _TempFilesToDelete = new List<string>();


        public FileJob(IFile sourceFile, string fromPath, string pendingPath, string toPath, ModeEnum mode, string password, string salt)
            : base(fromPath, pendingPath, toPath)
        {
            if (mode == ModeEnum.Undefinied)
                throw new ArgumentOutOfRangeException("mode");

            _Mode = mode;
            _Password = password;
            _Salt = salt;
            _SourceFile = sourceFile;

            SizeEnum size = GetSize();

            switch (size)
            {
                case SizeEnum.Undefinied:
                    throw new Exception();
                case SizeEnum.Small:
                    _CompressToMemory = true;
                    _EncryptToMemory = true;
                    break;
                case SizeEnum.Medium:
                    _CompressToMemory = false;
                    _EncryptToMemory = true;
                    break;
                case SizeEnum.Large:
                    _CompressToMemory = false;
                    _EncryptToMemory = false;
                    break;
                default:
                    throw new Exception();
            }
        }

        public SizeEnum GetSize()
        {
            const long size40MB = 1024L * 1024L * 40L;
            const long size160MB = size40MB * 4L;

            if (_SourceFile.SizeBytes <= size40MB)
                return SizeEnum.Small;
            else if (_SourceFile.SizeBytes <= size160MB)
                return SizeEnum.Medium;
            else
                return SizeEnum.Large;
        }

        public static FileJob GetBackupJob(IFile file, DirectoryInfo objectsDir, string password, string salt)
        {
            string pendingFileName;
            string keepFileName;
            ObjectStorage.GetStorageTargets(file.Sha256, objectsDir, out pendingFileName, out keepFileName);

            return new FileJob(file, file.GetFileInfo().FullName, pendingFileName, keepFileName, ModeEnum.CompressEncrypt, password, salt);
        }

        public static FileJob GetRestoreJob(IFile file, DirectoryInfo objectsDir, DirectoryInfo restoreTargetDir, string password, string salt)
        {
            string toPath = Path.Combine(restoreTargetDir.FullName, file.GetPathFromRoot());
            string pending = toPath + ".pending";

            string pendingFileName; // don't use for restore, this is just for backup
            string keepFileName;
            ObjectStorage.GetStorageTargets(file.Sha256, objectsDir, out pendingFileName, out keepFileName);

            return new FileJob(file, keepFileName, pending, toPath, ModeEnum.DecryptDecompress, password, salt);
        }

        public IFile SourceFile
        {
            get { return _SourceFile; }
        }

        public static Stream JustDecryptAndDecompress(FileInfo file, string password, string salt)
        {
            MemoryStream decompressedStream = new MemoryStream();

            using (FileStream source = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (StreamCryptoWrapper decryptedSource = StreamCryptoWrapper.Decrypt(source, password, salt))
            using (MemoryStream decryptedMemory = new MemoryStream())
            {
                decryptedSource.CryptoStream.CopyTo(decryptedMemory);
                decryptedMemory.Position = 0;
                KeepLzmaCompressor.Decompress(decryptedMemory, decompressedStream);
                decompressedStream.Position = 0;                
            }
            return decompressedStream;
        }

        private Stream GetCompressedStream()
        {
            if (_CompressToMemory)
                return new MemoryStream();
            else
            {
                string temp = Path.GetTempFileName();
                _TempFilesToDelete.Add(temp);
                return File.Create(temp);
            }   
        }

        private Stream GetEncryptedStream()
        {
            if (_EncryptToMemory)
                return new MemoryStream();
            else
                return new FileStream(PendingPath, FileMode.Create, FileAccess.ReadWrite, FileShare.Read);
        }

        private Stream GetDecryptedStream()
        {
            if (_EncryptToMemory)
                return new MemoryStream();
            else
            {
                string temp = Path.GetTempFileName();
                _TempFilesToDelete.Add(temp);
                return File.Create(temp);
            }
        }

        public override void Execute()
        {
            EnsurePathExists(PendingPath);

            try
            {
                if (_Mode == ModeEnum.CompressEncrypt)
                {
                    Program.log.Debug("starting backup of " + FromPath);

                    Program.log.Debug("=> compress and encrypt to " + PendingPath);

                    using (Stream encryptedStream = GetEncryptedStream())
                    using (StreamCryptoWrapper encryptedTarget = StreamCryptoWrapper.Encrypt(encryptedStream, _Password, _Salt))
                    {
                        using (Stream compressedStream = GetCompressedStream())
                        {
                            using (FileStream source = new FileStream(FromPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                                KeepLzmaCompressor.Compress(source, compressedStream);

                            compressedStream.Position = 0;
                            Program.log.Debug("compression done, start encrypting");
                            compressedStream.CopyTo(encryptedTarget.CryptoStream);
                            encryptedTarget.CryptoStream.FlushFinalBlock();
                            Program.log.Debug("encryption done, hashing result");
                        }

                        encryptedStream.Position = 0;
                        ChecksumTarget = KeepHasher.GetHash(encryptedStream);
                        encryptedStream.Position = 0;
                        SizeTarget = encryptedStream.Length;
                        Program.log.Debug("done");

                        if (encryptedStream is MemoryStream)
                            using (Stream target = new FileStream(PendingPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                                encryptedStream.CopyTo(target);
                    }
                }
                else if (_Mode == ModeEnum.DecryptDecompress)
                {
                    Program.log.Debug("<= decrypt and decompress " + FromPath);
                    using (FileStream source = new FileStream(FromPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                    using (StreamCryptoWrapper decryptedSource = StreamCryptoWrapper.Decrypt(source, _Password, _Salt))
                    using (Stream decryptedMemory = GetDecryptedStream())
                    using (FileStream target = new FileStream(PendingPath, FileMode.Create, FileAccess.Write, FileShare.Read))
                    {
                        decryptedSource.CryptoStream.CopyTo(decryptedMemory);
                        decryptedMemory.Position = 0;
                        Program.log.Debug("decryption to memory done, decompressing to disc");
                        KeepLzmaCompressor.Decompress(decryptedMemory, target);
                        Program.log.Debug("decompression done");
                    }
                }
            }
            finally
            {
                foreach (string file in _TempFilesToDelete)
                {
                    Program.log.Debug("deleting temp file "+file);
                    File.Delete(file);
                }
            }

            EnsurePathExists(ToPath);

            Program.log.Debug("renaming " + PendingPath + " -> "+ ToPath);
            File.Move(PendingPath, ToPath);

            if (_Mode == ModeEnum.DecryptDecompress)
            {
                Program.log.Debug("restoring metadata " + ToPath);
                FileInfo fi = new FileInfo(ToPath);
                fi.LastWriteTimeUtc = SourceFile.LastWriteTimeUtc;
                fi.CreationTimeUtc = SourceFile.CreationTimeUtc;
                if (fi.Length != SourceFile.SizeBytes)
                    Program.log.Error("size mismatch " + ToPath + " " + SourceFile.Sha256);

                //string sha = Hashing.GetHash(fi);
                //if (sha != SourceFile.Sha256)
                //    Program.log.Error("hash mismatch " + ToPath + " " + sha + " but should be "+ SourceFile.Sha256);
            }
        }
    }
}
