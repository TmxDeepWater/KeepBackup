using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeepBackup.OriginInventory
{
    abstract class AbstactFile : IFile
    {
        private string _Name;
        private DateTime _CreationTimeUtc;
        private DateTime _LastWriteTimeUtc;
        private long _SizeBytes;

        private IFolder _Folder;       

        public abstract string Sha256 { get; }

        public IFolder Folder
        {
            get { return _Folder; }
            protected set { _Folder = value; }
        }

        public string Name
        {
            get { return _Name; }
            protected set { _Name = value; }
        }
        public long SizeBytes
        {
            get { return _SizeBytes; }
            protected set { _SizeBytes = value; }
        }

        public DateTime CreationTimeUtc
        {
            get { return _CreationTimeUtc; }
            protected set { _CreationTimeUtc = value; }
        }
        public DateTime LastWriteTimeUtc
        {
            get { return _LastWriteTimeUtc; }
            protected set { _LastWriteTimeUtc = value; }
        }

        public abstract void ReuseSha256s(IFile matching);

        public abstract FileInfo GetFileInfo();

        public string GetPathFromRoot()
        {
            return Path.Combine(Folder.GetPathFromRoot(), Name);
        }
    }
}
