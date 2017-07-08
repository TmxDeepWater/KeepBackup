using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace KeepBackup.OriginInventory
{
    interface IFile
    {
        IFolder Folder { get; }
        string Name { get; }
        DateTime CreationTimeUtc { get; }
        DateTime LastWriteTimeUtc { get; }
        long SizeBytes { get; }
        string Sha256 { get; }
        void ReuseSha256s(IFile matching);
        FileInfo GetFileInfo();
        string GetPathFromRoot();
    }
}
