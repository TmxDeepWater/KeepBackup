using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;
using KeepBackup.Helper;

namespace KeepBackup.OriginInventory
{
    class Inventory
    {
        private readonly DirectoryInfo _Dir;
        private readonly Configuration _Configuration;
        private IFolder Root { get; set; }

        private string _Name;

        private Inventory(DirectoryInfo dir, Configuration configuration)
        {
            _Dir = dir;
            _Configuration = configuration;
        }

        public string Name
        {
            get { return _Name; }
        }

        public string Save(DateTime timestamp)
        {
            return Save(_Dir, timestamp);
        }

        internal string Save(DirectoryInfo dir, DateTime timestamp)
        {
            string timestampStr = timestamp.ToString("yyyy-MM-dd HH-mm-ss");

            long totalSizeBytes = Root.AllFiles.Sum(x => x.SizeBytes);
            double totalSizeMegaBytes = totalSizeBytes / (1024.0 * 1024.0);

            XDocument doc = new XDocument();
            doc.Add(new XElement("Inventory",
                new XAttribute("verion", "1.0"),
                new XAttribute("totalSizeBytes", totalSizeBytes),
                new XAttribute("totalSizeMegaBytes", totalSizeMegaBytes.ToString("0.##", CultureInfo.InvariantCulture)),
                Root.ToXml()
                ));

            string fileName = "KeepBackup-" + timestampStr + ".inventory";
            _Name = fileName;

            string newFileName = Path.Combine(dir.FullName, fileName);
            Program.log.Info("saving " + newFileName);
            doc.Save(newFileName);
            Program.log.Info("saved");
            return newFileName;
        }

        public IFolder Folder
        {
            get { return Root; }
        }

        internal void ReuseSha256s(Inventory old)
        {
            Root.ReuseSha256s(old.Root);
        }

        public static Inventory FromXml(FileInfo file, Configuration configuration)
        {
            XDocument doc;
            using (FileStream fs = new FileStream(file.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
                doc = XDocument.Load(fs);


            Inventory i = new Inventory(file.Directory, configuration)
            {
                Root = new XmlFolder(doc.Root.Element("Folder")),
                _Name = file.Name               
            };            

            return i;               
        }

        public static Inventory FromFileSystem(DirectoryInfo dir, Configuration configuration)
        {
            Program.log.Info("building inventory from file system");

            Inventory inv = new Inventory(dir, configuration)
            {
                Root = new FileSystemFolder(dir, configuration)
            };

            Program.log.Info("done");

            Program.log.Info("trying to find old inventory to recover hashes");
            Inventory old = Inventory.GetInventoriesNewestFirst(dir, configuration).FirstOrDefault();

            if (old == null)
                Program.log.Info("none found");
            else
            {
                Program.log.Info("reusing old hashes");
                inv.ReuseSha256s(old);
                Program.log.Info("done");
            }

            Program.log.Info("collecting files to hash");

            FileSystemFile[] filesToHash = inv.Folder.AllFiles.Cast<FileSystemFile>().Where(x => x.NeedsHashing).ToArray();

            Program.log.Info(filesToHash.Length + " files need to be hashed");

            if (filesToHash.Length > 0)
            {
                filesToHash.ParallelExecute(
                    x => x.SizeBytes,
                    x => x.GetFileInfo().FullName,
                    x => x.Sha256,
                    x => x.HashFile(),
                    4);
            }
            Program.log.Info("hashing done, inventory complete");

            return inv;
        }

        public static IEnumerable<Inventory> GetInventoriesNewestFirst(DirectoryInfo dir, Configuration configuration)
        {
            FileInfo[] invFiles = dir.GetFiles("KeepBackup*.inventory", SearchOption.TopDirectoryOnly);

            foreach (var file in invFiles.OrderByDescending(x => x.Name))
            {
                Program.log.Info("loading inventory " + file.FullName);
                yield return Inventory.FromXml(file, configuration);
            }
        }

        internal IFolder GetSubFolderByPath(string partialRestorePath)
        {
            string[] p = partialRestorePath.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);

            if (p.Length == 0)
                throw new Exception();

            return GetSubFolderByPath(p);
        }

        internal IFolder GetSubFolderByPath(IEnumerable<string> partialRestorePath)
        {
            IFolder currentFolder = Root;

            foreach (string step in partialRestorePath)
            {
                IFolder f = currentFolder.SubFolders.SingleOrDefault(x => x.Name.Equals(step, StringComparison.OrdinalIgnoreCase));
                if (f != null)
                    currentFolder = f;
                else
                {
                    Program.log.Error("path in inventory not found" + partialRestorePath);
                    return null;
                }
            }

            return currentFolder;
        }

        internal IFile GetFileByPath(string partialRestorePath)
        {
            string[] p = partialRestorePath.Split(new char[] { '\\' }, StringSplitOptions.RemoveEmptyEntries);

            IFolder folder = GetSubFolderByPath(p.Take(p.Length - 1));

            IFile file = folder.Files.SingleOrDefault(x => x.Name.Equals(p[p.Length - 1], StringComparison.OrdinalIgnoreCase));

            return file;
        }
    }
}
