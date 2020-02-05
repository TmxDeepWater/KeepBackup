using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KeepBackup.OriginInventory
{
    class FileSystemFolder : AbstractFolder
    {
        private readonly DirectoryInfo _FileSystemDir;
        private readonly string _Path;

        private IList<IFolder> _SubFolders = null;
        private IList<IFile> _Files = null;

        public override IEnumerable<IFolder> SubFolders
        {
            get { return _SubFolders; }
        }

        public override IEnumerable<IFile> Files
        {
            get { return _Files; }
        }
        
        public FileSystemFolder(DirectoryInfo dir, Configuration configuration)
        {
            Name = dir.Name;
            Configuration = configuration;

            _FileSystemDir = dir;
            _Path = dir.FullName;

            _Files = GetFiles();
            _SubFolders = GetSubFolders();
        }

        public FileSystemFolder(IFolder parent, DirectoryInfo dir, Configuration configuration) : this (dir, configuration)
        {
            Parent = parent;
        }


        public IList<IFolder> GetSubFolders()
        {
            IList<IFolder> subFolders = new List<IFolder>();

            foreach (var dir in _FileSystemDir.EnumerateDirectories("*", SearchOption.TopDirectoryOnly))
            {
                if (Configuration.IsBlacklisted(dir, out bool log))
                {
                    if (log)
                    {
                        Program.log.Info("skipping blacklisted directory " + dir.FullName);
                    }
                }
                else
                {
                    subFolders.Add(new FileSystemFolder(this, dir, Configuration));
                }
            }

            return subFolders;
        }

        public IList<IFile> GetFiles()
        {
            IList<IFile> files = new List<IFile>();

            foreach (var file in _FileSystemDir.EnumerateFiles("*", SearchOption.TopDirectoryOnly))
            {
                if (Configuration.IsBlacklisted(file, out bool log))
                {
                    if (log)
                    {
                        Program.log.Info("skipping blacklisted file " + file.FullName);
                    }
                }
                else
                {
                    files.Add(new FileSystemFile(this, file));
                }
            }

            return files;
        }

        public override void ReuseSha256s(IFolder folder)
        {
            foreach (IFile file in Files)
            {
                var matching = folder.Files.FirstOrDefault(x => x.Name.Equals(file.Name, StringComparison.Ordinal));
                if (matching != null)
                    file.ReuseSha256s(matching);
            }

            foreach (IFolder subFolder in SubFolders)
            {
                var matching = folder.SubFolders.FirstOrDefault(x => x.Name.Equals(subFolder.Name, StringComparison.Ordinal));
                if (matching != null)
                    subFolder.ReuseSha256s(matching);
            }
        }
    }
}
