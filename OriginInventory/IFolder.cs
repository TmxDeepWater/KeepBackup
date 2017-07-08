using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeepBackup.OriginInventory
{
    interface IFolder
    {
        string Name { get; }

        IFolder Parent { get; }

        IEnumerable<IFolder> SubFolders { get; }

        IEnumerable<IFolder> AllSubFolders { get; }

        IEnumerable<IFile> Files { get; }

        IEnumerable<IFile> AllFiles { get; }

        void ReuseSha256s(IFolder root);

        string GetPathFromRoot();
        bool IsEqualOrSubfolder(IFolder dfolder);
    }
}
