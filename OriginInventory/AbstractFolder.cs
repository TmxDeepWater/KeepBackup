using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeepBackup.OriginInventory
{
    abstract class AbstractFolder : IFolder
    {
        private IFolder _Parent;
        private string _Name;
        private Configuration _Configuration;

        public string Name
        {
            get { return _Name; }
            protected set { _Name = value; }
        }

        public IFolder Parent
        {
            get { return _Parent; }
            protected set { _Parent = value; }
        }

        public Configuration Configuration
        {
            get { return _Configuration; }
            protected set { _Configuration = value; }
        }

        public abstract IEnumerable<IFolder> SubFolders { get; }
        public abstract IEnumerable<IFile> Files { get; }

        public IEnumerable<IFile> AllFiles
        {
            get
            {
                foreach (var f in Files)
                    yield return f;

                foreach (var sub in SubFolders)
                    foreach (var f in sub.AllFiles)
                        yield return f;
            }
        }

        public IEnumerable<IFolder> AllSubFolders
        {
            get
            {
                foreach (var sub in SubFolders)
                {
                    yield return sub;

                    foreach (var f in sub.AllSubFolders)
                        yield return f;
                }
            }
        }

        public abstract void ReuseSha256s(IFolder root);

        public string GetPathFromRoot()
        {
            List<IFolder> folders = new List<IFolder>();
            IFolder currentFolder = this;

            while (currentFolder != null)
            {
                folders.Add(currentFolder);
                currentFolder = currentFolder.Parent;
            }

            folders.Reverse();

            string path = folders.First().Name;

            foreach (IFolder folder in folders.Skip(1))
                path = Path.Combine(path, folder.Name);

            return path;
        }

        public bool IsEqualOrSubfolder(IFolder folder)
        {
            if (folder == null)
                return false;

            if (folder.Equals(this))
                return true;

            return AllSubFolders.Any(x => x.Equals(folder));
        }
    }
}
