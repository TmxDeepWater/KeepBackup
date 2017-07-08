using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KeepBackup.Helper;

namespace KeepBackup.OriginInventory
{
    class FileSystemFile : AbstactFile
    {
        private readonly FileInfo _FileInfo;
        private string _Sha256;

        public FileSystemFile(IFolder folder, FileInfo fileInfo)
        {
            _FileInfo = fileInfo;

            Folder = folder;
            Name = fileInfo.Name;
            CreationTimeUtc = fileInfo.CreationTimeUtc;
            LastWriteTimeUtc = fileInfo.LastWriteTimeUtc;
            SizeBytes = fileInfo.Length;
        }



        public override string Sha256
        {
            get
            {
                if (_Sha256 == null)
                    throw new Exception("no hash");

                return _Sha256;
            }
        }

        public bool NeedsHashing
        {
            get { return _Sha256 == null; }
        }

        public void HashFile()
        {
            _Sha256 = KeepHasher.GetHash(_FileInfo);
        }

        public override void ReuseSha256s(IFile matching)
        {
            if (Name.Equals(matching.Name, StringComparison.Ordinal) &&
                    CreationTimeUtc.Equals(matching.CreationTimeUtc) &&
                    LastWriteTimeUtc.Equals(matching.LastWriteTimeUtc) &&
                    SizeBytes == matching.SizeBytes)
            {
                _Sha256 = matching.Sha256;
            }
                
            else
            {
                _Sha256 = null;
            }
        }

        public override FileInfo GetFileInfo()
        {
            return _FileInfo;
        }
    }
}
