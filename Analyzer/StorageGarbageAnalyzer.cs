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
            List<Inventory> inventories = Inventory.GetInventoriesNewestFirst(_Store.Directory, _Configuration).Reverse().ToList();

            Dictionary<Inventory, List<string>> hashes = new Dictionary<Inventory, List<string>>();

            foreach (Inventory inv in inventories)
            {
                var list = inv.Folder.AllFiles.Select(x => x.Sha256).ToList();
                hashes.Add(inv, list);
            }

            // look at single inventories
            foreach (Inventory inventory in inventories)
            {
                HashSet<string> uniqueHashes = GetUniqueHashes(new Inventory[] { inventory }, inventories);

                var ms = _Store.GetManifestObjects(uniqueHashes).ToArray();
                double sizeSourceMB = ms.Sum(x => x.SizeSource) / (1024.0 * 1024.0);
                double sizeTargetMB = ms.Sum(x => x.SizeTarget) / (1024.0 * 1024.0);

                Program.log.InfoFormat("removal of inventory '{0}' would safe {1:0.00} MB, uncompressed {2:0.00} MB", inventory.Name, sizeTargetMB, sizeSourceMB);
            }

            // look at inceremental inventories
            for (int i=1; i<inventories.Count-1; i++)
            {
                IEnumerable<Inventory> nInventories = inventories.Take(i + 1);
                HashSet<string> uniqueHashes = GetUniqueHashes(nInventories, inventories);

                var ms = _Store.GetManifestObjects(uniqueHashes).ToArray();
                double sizeSourceMB = ms.Sum(x => x.SizeSource) / (1024.0 * 1024.0);
                double sizeTargetMB = ms.Sum(x => x.SizeTarget) / (1024.0 * 1024.0);

                Program.log.Info("removal of inventories");
                foreach (var inv in nInventories)
                    Program.log.Info("  "+inv.Name);
                Program.log.InfoFormat("would safe {0:0.00} MB, uncompressed {1:0.00} MB", sizeTargetMB, sizeSourceMB);
            }
        }

        public static HashSet<string> GetUniqueHashes(IEnumerable<Inventory> iventories, IEnumerable<Inventory> others)
        {
            var files = from i in iventories from f in i.Folder.AllFiles select f;

            HashSet<string> hash = new HashSet<string>(files.Select(x => x.Sha256));

            foreach (Inventory other in others)
            {
                if (iventories.Contains(other))
                    continue;

                foreach (var h in other.Folder.AllFiles.Select(x => x.Sha256))
                    hash.Remove(h);
            }

            hash.TrimExcess();
            return hash;
        }
    }
}
