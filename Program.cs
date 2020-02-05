using KeepBackup.CompressionAndEncryption;
using KeepBackup.OriginInventory;
using KeepBackup.Storage;
using log4net;
using log4net.Config;
using log4net.Repository.Hierarchy;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using KeepBackup.Analyzer;

namespace KeepBackup
{
    class Program
    {
        internal static readonly ILog log = LogManager.GetLogger("KeepBackup");

        static void Main(string[] args)
        {
            DateTime timestamp = DateTime.Now;

            Configuration configuration = new Configuration();

            XmlConfigurator.Configure(new FileInfo("log4net.config"));
            log.Info("KeepBackup Prototype v0.2");

            if (args == null || args.Length == 0)
            {
                log.Error("no command line args");
            }
            else
            {
                if (string.Equals("backup", args[0], StringComparison.OrdinalIgnoreCase))
                    Backup(args, configuration, timestamp);

                if (string.Equals("createinventory", args[0], StringComparison.OrdinalIgnoreCase))
                    CreateInventory(args, configuration, timestamp);

                if (string.Equals("restore", args[0], StringComparison.OrdinalIgnoreCase))
                    Restore(args, configuration);

                if (string.Equals("restorefile", args[0], StringComparison.OrdinalIgnoreCase))
                    RestoreFile(args, configuration);

                if (string.Equals("validatestorage", args[0], StringComparison.OrdinalIgnoreCase))
                    ValidateStorage(args, configuration);

                if (string.Equals("garbagecollect", args[0], StringComparison.OrdinalIgnoreCase))
                    GarbageCollect(args, configuration, timestamp);

                if (string.Equals("garbageanalysis", args[0], StringComparison.OrdinalIgnoreCase))
                    GarbageAnalysis(args, configuration, timestamp);

                if (string.Equals("compressiontests", args[0], StringComparison.OrdinalIgnoreCase))
                    CompressionTests(args, configuration);
                
                if (string.Equals("analyzeduplication", args[0], StringComparison.OrdinalIgnoreCase))
                    AnalyzeDuplication(args, configuration);

                if (string.Equals("compare", args[0], StringComparison.OrdinalIgnoreCase))
                    Compare(args, configuration);

                if (string.Equals("largefiles", args[0], StringComparison.OrdinalIgnoreCase))
                    LargeFiles(args, configuration);



            }
            log.Info("-= KeepBackup finished =-");
        }

        private static void LargeFiles(string[] args, Configuration configuration)
        {
            log.Info("files by size");
            if (args.Length != 2)
            {
                log.Error("wrong number of command line args");
                Environment.Exit(-1);
            }

            DirectoryInfo diTarget = new DirectoryInfo(args[1]);
            if (!diTarget.Exists)
            {
                log.Error("storage does not exists");
                Environment.Exit(-1);
            }

            log.Info("init storage " + diTarget.FullName);
            ObjectStorage store = new ObjectStorage(diTarget, configuration);

            LargeFileAnalyzer lfa = new LargeFileAnalyzer(store, configuration);
            lfa.Analyze();

            log.Info("finished analysis");
        }

        private static void AnalyzeDuplication(string[] args, Configuration configuration)
        {
            log.Info("analyze file duplication in inventory");
            if (args.Length != 2)
            {
                log.Error("wrong number of command line args");
                Environment.Exit(-1);
            }

            FileInfo inv = new FileInfo(args[1]);
            if (!inv.Exists)
            {
                log.Error("inventory does not exists");
                Environment.Exit(-1);
            }

            log.Info("open inventory from file " + inv.FullName);
            Inventory i = Inventory.FromXml(inv, configuration);
            Program.log.Info("Start Duplication Analysis");
            InventoryFileDuplication a = new InventoryFileDuplication(i);
            a.Analyze();
            log.Info("finished analysis");
        }

        private static void Compare(string[] args, Configuration configuration)
        {
            log.Info("comparing two inventories");
            if (args.Length != 3)
            {
                log.Error("wrong number of command line args");
                Environment.Exit(-1);
            }

            FileInfo inv = new FileInfo(args[1]);
            if (!inv.Exists)
            {
                log.Error("inventory does not exists");
                Environment.Exit(-1);
            }

            FileInfo inv2 = new FileInfo(args[2]);
            if (!inv2.Exists)
            {
                log.Error("inventory does not exists");
                Environment.Exit(-1);
            }

            log.Info("open inventories from files");
            Inventory i = Inventory.FromXml(inv, configuration);
            Inventory i2 = Inventory.FromXml(inv2, configuration);
            Program.log.Info("start comparison analysis");
            InventoryComparer a = new InventoryComparer(i, i2);
            a.Analyze();
            log.Info("finished analysis");
        }

        private static void GarbageCollect(string[] args, Configuration configuration, DateTime timestamp)
        {
            log.Info("garbage collect storage");
            if (args.Length != 2)
            {
                log.Error("wrong number of command line args");
                Environment.Exit(-1);
            }
            DirectoryInfo diTarget = new DirectoryInfo(args[1]);
            if (!diTarget.Exists)
            {
                log.Error("path does not exists " + diTarget.FullName);
                Environment.Exit(-1);
            }

            log.Info("init storage " + diTarget.FullName);
            ObjectStorage store = new ObjectStorage(diTarget, configuration);

            store.GarbageCollect(timestamp);
        }

        private static void GarbageAnalysis(string[] args, Configuration configuration, DateTime timestamp)
        {
            log.Info("garbage collect analysis");
            if (args.Length != 2)
            {
                log.Error("wrong number of command line args");
                Environment.Exit(-1);
            }
            DirectoryInfo diTarget = new DirectoryInfo(args[1]);
            if (!diTarget.Exists)
            {
                log.Error("path does not exists " + diTarget.FullName);
                Environment.Exit(-1);
            }

            log.Info("init storage " + diTarget.FullName);
            ObjectStorage store = new ObjectStorage(diTarget, configuration);

            var col = new StorageGarbageAnalyzer(store, configuration);
            col.Analyze();
        }

        private static void Backup(string[] args, Configuration configuration, DateTime timestamp)
        {
            log.Info("backup an inventory");
            if (args.Length != 3)
            {
                log.Error("wrong number of command line args");
                Environment.Exit(-1);
            }

            DirectoryInfo originDir = new DirectoryInfo(args[1]);
            if (!originDir.Exists)
            {
                log.Error("origin does not exists");
                Environment.Exit(-1);
            }

            DirectoryInfo targetDir = new DirectoryInfo(args[2]);
            if (targetDir.Exists)
            {
                log.Info("target folder exist \"" + originDir.FullName + "\"");
            }
            else
            {
                log.Info("target does not exist, creating folder \"" + targetDir.FullName + "\"");
                targetDir.Create();
            }            

            log.Info("creating inventory for " + originDir.FullName);
            Inventory i = Inventory.FromFileSystem(originDir, configuration);
            log.Info("inventory created");
            string file = i.Save(timestamp);

            log.Info("init target " + targetDir.FullName);
            ObjectStorage store = new ObjectStorage(targetDir, configuration);
            log.Info("start backup");
            store.Backup(i, timestamp);
            log.Info("finished backup");
        }

        private static void CreateInventory(string[] args, Configuration configuration, DateTime timestamp)
        {
            log.Info("create an inventory");
            if (args.Length != 2)
            {
                log.Error("wrong number of command line args");
                Environment.Exit(-1);
            }

            DirectoryInfo originDir = new DirectoryInfo(args[1]);
            if (!originDir.Exists)
            {
                log.Error("origin does not exists");
                Environment.Exit(-1);
            }
            
            log.Info("creating inventory for " + originDir.FullName);
            Inventory i = Inventory.FromFileSystem(originDir, configuration);
            log.Info("inventory created");
            string file = i.Save(timestamp);
        }

        private static void Restore(string[] args, Configuration configuration)
        {
            log.Info("restore an inventory");
            if (args.Length < 3 || args.Length > 4)
            {
                log.Error("wrong number of command line args");
                Environment.Exit(-1);
            }

            FileInfo inventoryFile = new FileInfo(args[1]);
            if (!inventoryFile.Exists)
            {
                log.Error("inventory does not exists");
                Environment.Exit(-1);
            }

            DirectoryInfo diTarget = new DirectoryInfo(args[2]);

            log.Info("loading inventory " + inventoryFile.FullName);
            Inventory i = Inventory.FromXml(inventoryFile, configuration);
            
            log.Info("init storage " + diTarget.FullName);
            ObjectStorage store = new ObjectStorage(inventoryFile.Directory, configuration);

            string partialRestorePath = null;
            if (args.Length == 4 && !string.IsNullOrWhiteSpace(args[3]))
                partialRestorePath = args[3];

            if (partialRestorePath == null)
            {
                log.Info("start restore");
                store.Restore(i, diTarget);
            }
            else
            {
                log.Info("start partial restore: " + partialRestorePath);
                store.PartialRestore(i, diTarget, partialRestorePath);
            }

            log.Info("finished restore");
        }

        private static void RestoreFile(string[] args, Configuration configuration)
        {
            log.Info("restore a single file from inventory");
            if (args.Length != 4)
            {
                log.Error("wrong number of command line args");
                Environment.Exit(-1);
            }

            FileInfo inventoryFile = new FileInfo(args[1]);
            if (!inventoryFile.Exists)
            {
                log.Error("origin does not exists");
                Environment.Exit(-1);
            }

            DirectoryInfo targetDir = new DirectoryInfo(args[2]);
            if (targetDir.Exists)
            {
                log.Error("target already exists");
                Environment.Exit(-1);
            }

            log.Info("loading inventory " + inventoryFile.FullName);
            Inventory i = Inventory.FromXml(inventoryFile, configuration);
            ObjectStorage store = new ObjectStorage(inventoryFile.Directory, configuration);

            log.Info("target" + targetDir.FullName);            

            string partialRestorePath = args[3];

            log.Info("start file restore: " + partialRestorePath);
            store.FileRestore(i, targetDir, partialRestorePath);

            log.Info("finished restore");
        }

        private static void ValidateStorage(string[] args, Configuration configuration)
        {
            log.Info("validate storage");
            if (args.Length != 2 && args.Length != 3)
            {
                log.Error("wrong number of command line args");
                Environment.Exit(-1);
            }

            DirectoryInfo targetDir = new DirectoryInfo(args[1]);
            if (!targetDir.Exists)
            {
                log.Error("target does not exists");
                Environment.Exit(-1);
            }

            log.Info("init storage " + targetDir.FullName);
            ObjectStorage store = new ObjectStorage(targetDir, configuration);

            bool decompress = false;
            if (args.Length == 3 && string.Equals("decompress", args[2], StringComparison.OrdinalIgnoreCase))
                decompress = true;

            bool fast = false;
            if (args.Length == 3 && string.Equals("fast", args[2], StringComparison.OrdinalIgnoreCase))
                fast = true;

            if (decompress)
                log.Info("start analysis with decompression");
            else if (fast)
                log.Info("start analysis without checking checksums");
            else
                log.Info("start analysis");

            IAnalyzer consCheck = new ConsistencyAnalyzer(fast, decompress, configuration, store);
            consCheck.Analyze();

            log.Info("finished analysis");
        }

        private static void CompressionTests(string[] args, Configuration configuration)
        {
            KeepBackup.CompressionAndEncryption.CompressionTests.DoTests(configuration.Password, configuration.Salt);
        }
    }
}
