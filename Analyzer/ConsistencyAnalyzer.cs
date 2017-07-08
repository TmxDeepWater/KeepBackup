using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KeepBackup.Helper;
using KeepBackup.OriginInventory;
using KeepBackup.Storage;
using log4net;

namespace KeepBackup.Analyzer
{
    class ConsistencyAnalyzer : IAnalyzer
    {
        private readonly bool _Fast;
        private readonly bool _Decompress;
        private readonly Configuration _Configuration;
        private readonly ObjectStorage _Storage;

        public ConsistencyAnalyzer(bool fast, bool decompress, Configuration configuration, ObjectStorage storage)
        {
            _Fast = fast;
            _Decompress = decompress;
            _Configuration = configuration;
            _Storage = storage;

            if (fast && decompress)
                throw new Exception();
        }

        public void Analyze()
        {
            int errorCount = 0;
            ILog log = Program.log;

            log.Info("getting all files ... might take some time");

            FileInfo[] allFiles = (from x in  _Storage.PartitionManager.AllDirectories
                                   from f in x.EnumerateFiles("*", SearchOption.AllDirectories)
                                   select f).ToArray();

            log.Info("done");

            log.Info("parsing the sha256 from filename");
            IEnumerable<FileInfo> files = allFiles.Where(x => x.Name.EndsWith(ObjectStorage.OBJ_EXTENSION, StringComparison.OrdinalIgnoreCase));
            Dictionary<string, FileInfo> filesBySha256 = new Dictionary<string, FileInfo>();
            foreach (FileInfo f in files)
            {
                string sha256 = f.Name.Substring(0, f.Name.Length - ObjectStorage.OBJ_EXTENSION.Length);
                filesBySha256.Add(sha256, f);
            }

            log.Info("collecting all stored objects from manifest");
            Dictionary<string, ManifestItem> manifestBySha256 = _Storage.Manifest.ManifestObjects.ToDictionary(x => x.Sha256source);

            /// Phase 1
            log.Info("check if all manifest files are present as files");
            foreach (var x in manifestBySha256.Keys)
            {
                if (!filesBySha256.ContainsKey(x))
                {
                    log.Error("missing file in storage, which is present in the manifest! " + x);
                    errorCount++;
                }
            }

            /// Phase 2
            log.Info("check if all files are present in the manifest");
            foreach (var x in filesBySha256.Keys)
            {
                if (!manifestBySha256.ContainsKey(x))
                {
                    log.Error("missing file in manifest, that was found in storage! " + x);
                    errorCount++;
                }
            }

            /// Phase 3
            log.Info("check size of all files with the manifest");

            foreach (var hash in filesBySha256.Keys)
            {
                ManifestItem mf;
                if (!manifestBySha256.TryGetValue(hash, out mf))
                    continue;

                long size = mf.SizeTarget;
                long size2 = filesBySha256[hash].Length;

                if (size != size2)
                {
                    log.Error("storage size mismatch! for " + hash);
                    errorCount++;
                }
            }

            /// Phase 4
            log.Info("look for pending files");
            IEnumerable<FileInfo> pendingFiles = allFiles.Where(x => x.Name.EndsWith(ObjectStorage.PENDING_EXTENSION, StringComparison.OrdinalIgnoreCase));

            foreach (var x in pendingFiles)
            {
                log.Error("found pending file " + x.FullName);
                errorCount++;
            }

            /// Phase 5
            log.Info("look for unknown files");

            foreach (var x in allFiles)
            {
                if (!x.Name.EndsWith(ObjectStorage.OBJ_EXTENSION, StringComparison.OrdinalIgnoreCase) &&
                    !x.Name.EndsWith(ObjectStorage.PENDING_EXTENSION, StringComparison.OrdinalIgnoreCase))
                {
                    log.Error("unknown file " + x.FullName);
                    errorCount++;
                }
            }

            /// Phase 6
            log.Info("look, if files from all inventories are present in manifest and nothing more");
            FileInfo[] invFiles = _Storage.GetInventoryFiles();

            HashSet<string> hashesFromAllInventories = new HashSet<string>();

            foreach (FileInfo fi in invFiles)
            {
                log.Info(fi.FullName);

                Inventory i = Inventory.FromXml(fi, _Configuration);

                IList<string> shas = i.Folder.AllFiles.Select(x => x.Sha256).ToList();

                foreach (string sha in shas)
                {
                    hashesFromAllInventories.Add(sha);

                    if (!manifestBySha256.ContainsKey(sha))
                    {
                        log.Error("missing file in manifest from inventory " + fi.FullName + " " + sha);
                        errorCount++;
                    }
                }
            }

            foreach (string sha in manifestBySha256.Keys)
            {
                if (!hashesFromAllInventories.Contains(sha))
                {
                    log.Error("found file in manifest, which is in no inventory " + sha);
                    errorCount++;
                }
            }


            ManifestItem[] ml = manifestBySha256.Values.ToArray();
            int count = ml.Count();

            if (!_Fast)
            {
                /// Phase 7
                {
                    log.Info("test cecksums of all files in storage");

                    Action<ManifestItem> check = delegate (ManifestItem x)
                    {
                        string shaTarget = x.Sha256target;
                        FileInfo fi = new FileInfo(ObjectStorage.GetStorageTarget(x.Sha256source, _Storage.PartitionManager.GetDirByNumber(x.Partition)));
                        string shaFile = KeepHasher.GetHash(fi);

                        if (!string.Equals(shaTarget, shaFile, StringComparison.Ordinal))
                        {
                            log.Error("storage hash mismatch! for " + x.Sha256source);
                            Interlocked.Increment(ref errorCount);
                        }
                    };

                    if (ml.Length > 0)
                    {
                        ml.ParallelExecute(
                            x => x.SizeSource,
                            x => x.Sha256source,
                            x => x.Sha256target,
                            x => check(x),
                            6);
                    }
                }
            }

            if (_Decompress)
            {
                // Phase 8 - SLOOWWW :-)
                log.Info("actually decode file and check size and checksum");
                for (int i = 0; i < count; i++)
                {
                    double percentFiles = ((double)i / (double)count) * 100.0;
                    Program.log.Info(string.Format("file {0} / {1} ({2:0.00}%)", i + 1, count, percentFiles));

                    var x = ml[i];

                    log.Info("checking decompression and decryption for " + x.Sha256source);

                    FileInfo fi = new FileInfo(ObjectStorage.GetStorageTarget(x.Sha256source, _Storage.PartitionManager.GetDirByNumber(x.Partition)));

                    long sizeFile;
                    string shaFile = null;
                    using (Stream s = FileJob.JustDecryptAndDecompress(fi, _Configuration.Password, _Configuration.Salt))
                    {
                        sizeFile = s.Length;
                        shaFile = KeepHasher.GetHash(s);
                    }

                    if (!string.Equals(x.Sha256source, shaFile, StringComparison.Ordinal))
                    {
                        log.Error("decompressed and decrypted hash mismatch! for " + x.Sha256source);
                        errorCount++;
                    }

                    if (x.SizeSource != sizeFile)
                    {
                        log.Error("decompressed and decrypted size mismatch! for " + x.Sha256source);
                        errorCount++;
                    }
                }
            }

            log.Info(errorCount + " errors !!");

        }
    }
}
