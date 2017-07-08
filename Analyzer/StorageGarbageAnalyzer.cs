using KeepBackup.OriginInventory;
using KeepBackup.Storage;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeepBackup.Analyzer
{
    class StorageGarbageAnalyzer : IAnalyzer
    {
        private readonly ObjectStorage _Store;
        private readonly Configuration _Configuration;

        public StorageGarbageAnalyzer(ObjectStorage store, Configuration configuration)
        {
            _Store = store;
            _Configuration = configuration;
        }

        public void Analyze()
        {
            Inventory[] inventories = Inventory.GetInventoriesNewestFirst(_Store.Directory, _Configuration).ToArray();

            Dictionary<Inventory, List<string>> hashes = new Dictionary<Inventory, List<string>>();

            foreach (Inventory inv in inventories)
            {
                var list = inv.Folder.AllFiles.Select(x => x.Sha256).ToList();
                hashes.Add(inv, list);
            }

            Dictionary<Inventory, HashSet<string>> uniqueHashes = new Dictionary<Inventory, HashSet<string>>();

            foreach (Inventory inventory in inventories)
            {
                var uh = GetUniqueHashes(inventory, (IEnumerable<Inventory>)inventories);
                if (uh.Count > 0)
                    uniqueHashes.Add(inventory, uh);
            }

            foreach (Inventory i in uniqueHashes.Keys.OrderByDescending(x => x.Name))
            {
                var ms = _Store.GetManifestObjects(uniqueHashes[i]).ToArray();
                double sizeSourceMB = ms.Sum(x => x.SizeSource) / (1024.0 * 1024.0);
                double sizeTargetMB = ms.Sum(x => x.SizeTarget) / (1024.0 * 1024.0);

                Program.log.InfoFormat("removal of inventory '{0}' would safe {1:0.00} MB, uncompressed {2:0.00} MB", i.Name, sizeTargetMB, sizeSourceMB);
            }
        }

        public static HashSet<string> GetUniqueHashes(Inventory iventory, IEnumerable<Inventory> other)
        {
            HashSet<string> hash = new HashSet<string>(iventory.Folder.AllFiles.Select(x => x.Sha256));

            foreach (Inventory inv2 in other)
            {
                if (inv2 == iventory)
                    continue;

                foreach (var h in inv2.Folder.AllFiles.Select(x => x.Sha256))
                    hash.Remove(h);
            }
            hash.TrimExcess();
            return hash;
        }
    }
}
