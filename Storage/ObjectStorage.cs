using KeepBackup.OriginInventory;
using log4net;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using KeepBackup.Helper;

namespace KeepBackup.Storage
{
    class ObjectStorage
    {
        public const string OBJ_EXTENSION = ".lzma.aes.keep";
        public const string PENDING_EXTENSION = ".pending";        
        private const string GARBAGE_SUBDIR = "garbage";

        private readonly DirectoryInfo _Dir;
        private readonly PartitionManager _PartitionManager;
        private readonly Configuration _Configuration;
        private readonly Manifest _Manifest;

        public ObjectStorage(DirectoryInfo di, Configuration configuration)
        {
            _PartitionManager = new PartitionManager(di);
            _Dir = di;
            _Configuration = configuration;
            _Manifest = new Manifest(di);
        }

        public DirectoryInfo Directory
        {
            get { return _Dir; }
        }

        public PartitionManager PartitionManager
        {
            get
            {
                return _PartitionManager;
            }
        }

        public Manifest Manifest
        {
            get { return _Manifest; }
        }

        public static void GetStorageTargets(string sha, DirectoryInfo objectsDir, out string pendingFileName, out string keepFileName)
        {
            string l1 = sha.Substring(0, 1).ToUpperInvariant();
            string l2 = sha.Substring(0, 4).ToUpperInvariant();

            string targetDir = Path.Combine(objectsDir.FullName, l1, l2);

            pendingFileName = Path.Combine(targetDir, sha + PENDING_EXTENSION);
            keepFileName = Path.Combine(targetDir, sha + OBJ_EXTENSION);
        }

        public static string GetStorageTarget(string sha, DirectoryInfo objectsDir)
        {
            string pendingFileName;
            string keepFileName;
            ObjectStorage.GetStorageTargets(sha, objectsDir, out pendingFileName, out keepFileName);

            return keepFileName;
        }

        internal IEnumerable<ManifestItem> GetManifestObjects(HashSet<string> hashSet)
        {
            return _Manifest.ManifestObjects.Where(x => hashSet.Contains(x.Sha256source));
        }

        private static IEnumerable<FileJob> GetBackupJobs(IEnumerable<IFile> files, DirectoryInfo objectsDir, string password, string salt, out List<FileJob> alternateSource, Predicate<string> alreadyBackuped)
        {
            IList<IFile> fs = files.ToList();
            Program.log.Info("backup files total: " + fs.Count());

            IList<FileJob> copyJobs = fs.Select(x => FileJob.GetBackupJob(x, objectsDir, password, salt)).ToList();

            Dictionary<string, FileJob> hashSetUnequalFiles = new Dictionary<string, FileJob>();

            alternateSource = new List<FileJob>();

            foreach (var cj in copyJobs)
            {
                if (!hashSetUnequalFiles.ContainsKey(cj.SourceFile.Sha256))
                    hashSetUnequalFiles.Add(cj.SourceFile.Sha256, cj);
                else
                    alternateSource.Add(cj);
            }

            Program.log.Info("backup unequal files: " + hashSetUnequalFiles.Count());

            IList<FileJob> notExisting = new List<FileJob>();
            
            foreach (var cj in hashSetUnequalFiles.Values)
            {
                if (!alreadyBackuped(cj.SourceFile.Sha256))
                    notExisting.Add(cj);
                else
                    alternateSource.Add(cj);
            }

            Program.log.Info("backup files after removing already backuped ones: " + notExisting.Count());

            return notExisting;
        }

        private static IList<FileJob> GetRestoreJobs(IEnumerable<IFile> files, Manifest manifest, PartitionManager partitionManager, DirectoryInfo diTarget, string password, string salt)
        {
            IList<IFile> fs = files.ToList();
            Program.log.Info("restore files total: " + fs.Count());

            IList<FileJob> copyJobs = fs.Select(x => FileJob.GetRestoreJob(x, partitionManager.GetDirByNumber(manifest.GetPartition(x.Sha256)), diTarget, password, salt)).ToList();

            IList<FileJob> notExistingTargets = copyJobs.Where(x => !File.Exists(x.ToPath)).ToList();

            Program.log.Info("not existing: " + notExistingTargets.Count());

            return notExistingTargets;
        }

        internal FileInfo[] GetInventoryFiles()
        {
            return _Dir.GetFiles("*.inventory", SearchOption.TopDirectoryOnly);
        }

        public void Backup(Inventory inventory, DateTime timestamp)
        {
            List<FileJob> alternateSource;
            List<FileJob> jobs = GetBackupJobs(inventory.Folder.AllFiles, _PartitionManager.CurrentPartitionDir, _Configuration.Password, _Configuration.Salt, out alternateSource, _Manifest.ContainsSha).ToList();

            int countTotal = jobs.Count();
            long sizeTotal = jobs.Sum(x => x.SourceFile.SizeBytes);
            
            FileJob[] smallJobs = jobs
                .Where(x => x.GetSize() == FileJob.SizeEnum.Small)
                .OrderByDescending(x => x.SourceFile.SizeBytes)
                .ToArray();

            FileJob[] mediumJobs = jobs
                .Where(x => x.GetSize() == FileJob.SizeEnum.Medium)
                .OrderByDescending(x => x.SourceFile.SizeBytes)
                .ToArray();

            FileJob[] largeJobs = jobs
                .Where(x => x.GetSize() == FileJob.SizeEnum.Large)
                .OrderByDescending(x => x.SourceFile.SizeBytes)
                .ToArray();

            Program.log.InfoFormat("{0} total, {1} small, {2} medium, {3} large", countTotal, smallJobs.Length, mediumJobs.Length, largeJobs.Length);

            Action<FileJob> go = delegate (FileJob job)
            {
                job.Execute();
                _Manifest.Add(job.SourceFile, job.ChecksumTarget, job.SizeTarget ?? 0, timestamp, _PartitionManager.CurrentPartitionNumber);
            };

            if (smallJobs.Length > 0)
            {
                Program.log.InfoFormat("starting small files - 6 in parallel");
                smallJobs.ParallelExecute(
                    x => x.SourceFile.SizeBytes,
                    x => x.FromPath,
                    x => x.SourceFile.Sha256,
                    x => go(x),
                    6,
                    countTotal,
                    0,
                    sizeTotal,
                    0
                    );
                Program.log.InfoFormat("done");
            }

            if (mediumJobs.Length > 0)
            {
                Program.log.InfoFormat("starting medium jobs - 2 in parallel");
                mediumJobs.ParallelExecute(
                    x => x.SourceFile.SizeBytes,
                    x => x.FromPath,
                    x => x.SourceFile.Sha256,
                    x => go(x),
                    2,
                    countTotal,
                    smallJobs.Length,
                    sizeTotal,
                    smallJobs.Sum(x => x.SourceFile.SizeBytes)
                    );
                Program.log.InfoFormat("done");
            }

            if (largeJobs.Length > 0)
            {
                Program.log.InfoFormat("starting large jobs - 4 in parallel");
                largeJobs.ParallelExecute(
                    x => x.SourceFile.SizeBytes,
                    x => x.FromPath,
                    x => x.SourceFile.Sha256,
                    x => go(x),
                    4,
                    countTotal,
                    smallJobs.Length + mediumJobs.Length,
                    sizeTotal,
                    smallJobs.Concat(mediumJobs).Sum(x => x.SourceFile.SizeBytes)
                    );
                Program.log.InfoFormat("done");
            }

            _Manifest.AddAlternateSources(alternateSource.Select(x => x.SourceFile), timestamp);

            inventory.Save(_Dir, timestamp);
        }

        internal void GarbageCollect(DateTime timestamp)
        {
            DirectoryInfo garbageDir = new DirectoryInfo(Path.Combine(_Dir.FullName, GARBAGE_SUBDIR));
            if (!garbageDir.Exists)
                garbageDir.Create();

            var allInventoryHashes = 
                from i in Inventory.GetInventoriesNewestFirst(_Dir, _Configuration)
                from f in i.Folder.AllFiles
                select f.Sha256;

            HashSet<string> invHashes = new HashSet<string>(allInventoryHashes);

            var shasStorage = (from x in _Manifest.ManifestObjects
                               select new { Sha = x.Sha256source, Partition = x.Partition })
                               .ToArray();

            var garbage = shasStorage.Where(x => !invHashes.Contains(x.Sha)).ToList();

            Program.log.InfoFormat("{0} files are garbage", garbage.Count);

            foreach (var g in garbage)
            {
                Program.log.Info("  "+g.Sha+" p"+g.Partition);

                FileInfo fi = new FileInfo(GetStorageTarget(g.Sha, _PartitionManager.GetDirByNumber(g.Partition)));
                if (!fi.Exists)
                    throw new Exception();

                string target = Path.Combine(garbageDir.FullName, fi.Name);

                File.Move(fi.FullName, target);
                _Manifest.Garbage(g.Sha, timestamp);
            }

            Program.log.Info("deleting empty directories");
            foreach (DirectoryInfo dir in _PartitionManager.AllDirectories)
            {
                DirectoryInfo[] di = dir.GetDirectories("*", SearchOption.AllDirectories);
                foreach (var d in di)
                {
                    bool empty = !d.EnumerateFiles().Any() && !d.EnumerateDirectories().Any();
                    if (empty)
                    {
                        Program.log.Info("  " + d.FullName);
                        d.Delete();
                    }
                }
            }
            
        }

        internal void Restore(Inventory inventory, DirectoryInfo diTarget)
        {
            PartialRestore(inventory, diTarget, null);
        }

        internal void PartialRestore(Inventory inventory, DirectoryInfo diTarget, string partialRestorePath)
        {
            if (!diTarget.Exists)
                diTarget.Create();

            IFolder subFolder;
            if (string.IsNullOrWhiteSpace(partialRestorePath))
                subFolder = inventory.Folder;
            else
                subFolder = inventory.GetSubFolderByPath(partialRestorePath);

            var jobs = GetRestoreJobs(subFolder.AllFiles, _Manifest, _PartitionManager, diTarget, _Configuration.Password, _Configuration.Salt).ToList();

            jobs.ParallelExecute(
                x => x.SourceFile.SizeBytes,
                x => x.ToPath,
                x => x.SourceFile.Sha256,
                x => x.Execute(),
                4);
        }

        internal void FileRestore(Inventory inventory, DirectoryInfo diTarget, string partialRestorePath)
        {
            diTarget.Create();

            IFile file = inventory.GetFileByPath(partialRestorePath);
            var jobs = GetRestoreJobs(new IFile[] { file }, _Manifest, _PartitionManager, diTarget, _Configuration.Password, _Configuration.Salt).ToList();
                        
            foreach (var j in jobs)
                j.Execute();
        }

    }
}
