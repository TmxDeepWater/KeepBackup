using KeepBackup.OriginInventory;
using KeepBackup.Storage;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KeepBackup.Helper;

namespace KeepBackup.CompressionAndEncryption
{
    class CompressionTests
    {
        public static FileInfo CreateRandomFile(long size)
        {
            Random r = new Random();
            string fileName = Path.GetTempFileName();

            using (FileStream fs = new FileStream(fileName, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                const int BLOCKSIZE = 100;

                long i = 0;

                while (i + BLOCKSIZE < size)
                {
                    byte[] data = new byte[BLOCKSIZE];
                    r.NextBytes(data);
                    fs.Write(data, 0, BLOCKSIZE);
                    
                    i += BLOCKSIZE;
                }

                while (i < size)
                {
                    byte[] data = new byte[1];
                    r.NextBytes(data);
                    fs.Write(data, 0, 1);

                    i += 1;
                }
            }

            FileInfo fi = new FileInfo(fileName);
            if (fi.Length != size)
                throw new Exception();

            return fi;
        }

        public static void DoTests(string password, string salt)
        {
            long size1MB = 1024L * 1024L;
            long size1GB = size1MB * 1024L;

            Program.log.Info("starting test 1MB");
            DoTest(size1MB, password, salt);
            Program.log.Info("starting test 11MB");
            DoTest(size1MB * 11, password, salt);
            Program.log.Info("starting test 110MB");
            DoTest(size1MB * 110, password, salt);
            Program.log.Info("starting test 1GB");
            DoTest(size1MB * 1024, password, salt);
            Program.log.Info("SUCCESS");
        }

        public static void DoTest(long size, string password, string salt)
        {
            FileInfo source = null; 
            FileInfo pending = null;
            FileInfo target = null;
            FileInfo restored = null;
            try
            {
                source = CreateRandomFile(size);
                string sourceHash = KeepHasher.GetHash(source);

                pending = new FileInfo(source.FullName + ".pending");
                target = new FileInfo(source.FullName + ".target");
                restored = new FileInfo(source.FullName + ".restored");
                
                IFile testFile = new TestFile()
                {
                    Name = source.Name,
                    Sha256 = sourceHash,
                    SizeBytes = size
                };

                FileJob job = new FileJob(testFile, source.FullName, pending.FullName, target.FullName, FileJob.ModeEnum.CompressEncrypt, password, salt);
                job.Execute();

                FileJob job2 = new FileJob(testFile, target.FullName, pending.FullName, restored.FullName, FileJob.ModeEnum.DecryptDecompress, password, salt);
                job2.Execute();

                string restoredHash = KeepHasher.GetHash(restored);

                if (sourceHash != restoredHash)
                    throw new Exception();
            }
            finally
            {
                if (source != null && source.Exists)
                    source.Delete();
                if (pending != null && pending.Exists)
                    pending.Delete();
                if (target != null && target.Exists)
                    target.Delete();
                if (restored != null && restored.Exists)
                    restored.Delete();
            }
        }

        private class TestFile : IFile
        {
            public DateTime CreationTimeUtc
            {
                get
                {
                    return DateTime.UtcNow;
                }
            }

            public IFolder Folder
            {
                get
                {
                    return null;
                }
            }

            public DateTime LastWriteTimeUtc
            {
                get
                {
                    return DateTime.UtcNow;
                }
            }

            public string Name
            {
                get; set;
            }

            public string Sha256
            {
                get; set;
            }

            public long SizeBytes
            {
                get; set;
            }

            public FileInfo GetFileInfo()
            {
                return null;
            }

            public string GetPathFromRoot()
            {
                return Name;
            }

            public void ReuseSha256s(IFile matching)
            {
                
            }
        }



    }
}
