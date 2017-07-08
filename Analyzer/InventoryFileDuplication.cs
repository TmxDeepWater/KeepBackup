using KeepBackup.OriginInventory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeepBackup.Analyzer
{
    class InventoryFileDuplication : IAnalyzer
    {
        private readonly Inventory _Inventory;

        private readonly IDictionary<string, IList<IFile>> _AllFilesByHash = new Dictionary<string, IList<IFile>>();

        public InventoryFileDuplication(Inventory inventory)
        {
            _Inventory = inventory;

            foreach (IFile file in inventory.Folder.AllFiles)
            {
                IList<IFile> files;
                if (_AllFilesByHash.TryGetValue(file.Sha256, out files))
                    files.Add(file);
                else
                {
                    files = new List<IFile>();
                    files.Add(file);
                    _AllFilesByHash.Add(file.Sha256, files);
                }
            }
        }

        public void Analyze()
        {
            Dictionary<string, long> duplHash2Size = new Dictionary<string, long>();

            foreach (var hash in _AllFilesByHash.Keys)
            {
                if (_AllFilesByHash[hash].Count < 2)
                    continue;

                duplHash2Size.Add(hash, _AllFilesByHash[hash][0].SizeBytes * _AllFilesByHash[hash].Count);
            }

            var list = duplHash2Size.OrderByDescending(x => x.Value).ToList();

            double totalMBwasted = 0;

            foreach (var x in list)
            {
                string hash = x.Key;

                Program.log.Info("Duplicate "+hash);

                double sizeMB = _AllFilesByHash[hash][0].SizeBytes / (1024.0 * 1024.0);
                double sizeMBwasted = sizeMB * (_AllFilesByHash[hash].Count - 1);

                totalMBwasted += sizeMBwasted;

                Program.log.InfoFormat("  {0} locations, file size is {1:0.00} MB, wasted space {2:0.00} MB", _AllFilesByHash[hash].Count, sizeMB, sizeMBwasted);

                foreach (var path in _AllFilesByHash[hash])
                    Program.log.Info("  " + path.GetPathFromRoot());
            }

            Program.log.InfoFormat("==> total MB wasted {0:0.00}", totalMBwasted);

            Program.log.InfoFormat("Looking for folders, where all files are duplicates", totalMBwasted);

            List<KeyValuePair<double, string>> sizeAndFolder = new List<KeyValuePair<double, string>>();

            foreach (var folder in _Inventory.Folder.AllSubFolders)
            {
                var hashes = folder.AllFiles.Select(x => x.Sha256).ToList();

                //look, if any file has no duplicate
                if (hashes.Any(x => !duplHash2Size.ContainsKey(x)))
                    continue;

                //look, if any duplicate is not in this folder or a subfolder
                foreach (string hash in hashes)
                {
                    var duplicateFolders = _AllFilesByHash[hash].Select(x => x.Folder);
                    bool allInside = duplicateFolders.All(x => folder.IsEqualOrSubfolder(x));
                    if (allInside)
                        continue;
                }

                double mb = folder.AllFiles.Select(x => x.SizeBytes).Sum() / (1024.0 * 1024.0);
                sizeAndFolder.Add(new KeyValuePair<double, string>(mb, folder.GetPathFromRoot()));
            }

            foreach (var x in sizeAndFolder.OrderByDescending(x => x.Key))
                Program.log.InfoFormat("{0:0.00} MB-Folder of all duplicates {1}", x.Key, x.Value);
        }
        

    }
}
