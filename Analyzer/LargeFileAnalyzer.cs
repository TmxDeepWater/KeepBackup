using System;
using System.Collections.Generic;
using System.Linq;
using KeepBackup.OriginInventory;
using KeepBackup.Storage;

namespace KeepBackup.Analyzer
{
    class LargeFileAnalyzer : IAnalyzer
    {
        private class FileAppereance
        {
            public string Path { get; set; }
            public string InvName { get; set; }
        }

        private class FileGroup
        {
            public long Size { get; set; }
            public string Sha { get; set; }
            public List<FileAppereance> Appereacens { get; set; }
        }

        private ObjectStorage _Store;
        private Configuration _Configuration;

        public LargeFileAnalyzer(ObjectStorage store, Configuration configuration)
        {
            _Store = store;
            _Configuration = configuration;
        }

        public void Analyze()
        {
            var invertories = Inventory.GetInventoriesNewestFirst(_Store.Directory, _Configuration).ToList();

            var q = from i in invertories
                    from f in i.Folder.AllFiles
                    group new { Size = f.SizeBytes, Path = f.GetPathFromRoot(), InvName = i.Name } by f.Sha256;

            List<FileGroup> files = new List<FileGroup>();

            foreach (var sha in q)
            {
                var fg = new FileGroup()
                {
                    Sha = sha.Key,
                    Size = sha.Select(x => x.Size).Distinct().Single(),
                    Appereacens = new List<FileAppereance>()
                };

                foreach (var x in sha)
                {
                    fg.Appereacens.Add(
                        new FileAppereance()
                        {
                            InvName = x.InvName,
                            Path = x.Path
                        }
                    );
                }

                files.Add(fg);
            }

            var result = files.OrderByDescending(x => x.Size).TakeWhile(x => x.Size > 100 * 1024 * 1024).ToList();


            foreach (var r in result)
            {
                double mb = r.Size / (1024.0 * 1024.0);

                Program.log.InfoFormat("{0:0.00} MB   {1}", mb, r.Sha);

                foreach (var a in r.Appereacens.OrderByDescending(x => x.InvName))
                    Program.log.InfoFormat("    \"{0}\" [{1}]", a.Path, a.InvName);
            }

        }
    }
}