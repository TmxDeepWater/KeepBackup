using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeepBackup.Storage
{
    class PartitionManager
    {
        private const string OBJECTS_SUBDIR = "obj";
        private const string PARTITION_SUBDIRS = "obj_";

        private DirectoryInfo _MainPartition;
        private Dictionary<int, DirectoryInfo> _PartitionDirs;
        private int _CurrentPartitionNumber;

        public PartitionManager(DirectoryInfo storageDir)
        {
            _MainPartition = new DirectoryInfo(Path.Combine(storageDir.FullName, OBJECTS_SUBDIR));
            if (!_MainPartition.Exists)
                _MainPartition.Create();

            var partitionDirs = storageDir.GetDirectories(PARTITION_SUBDIRS+"*", SearchOption.TopDirectoryOnly).OrderBy(x => x.Name).ToList();

            _PartitionDirs = partitionDirs.ToDictionary(x => GetNumbersFromDirNames(x), x => x);
            
            if (_PartitionDirs.ContainsKey(0))
            {
                _PartitionDirs.Remove(0);
                Program.log.Warn("partition dir with 0 is not supported and will be ignored");
            }

            if (partitionDirs.Count == 0)
                _CurrentPartitionNumber = 0;
            else
                _CurrentPartitionNumber = _PartitionDirs.Keys.Max();

            Program.log.Info("partition for backups: " + _CurrentPartitionNumber);
        }

        private int GetNumbersFromDirNames(DirectoryInfo dir)
        {
            string nr = dir.Name.Substring(PARTITION_SUBDIRS.Length);
            return int.Parse(nr);
        }

        public IEnumerable<DirectoryInfo> AllDirectories
        {
            get
            {
                yield return _MainPartition;
                foreach (var x in _PartitionDirs.Values)
                    yield return x;
            }
        }

        public DirectoryInfo CurrentPartitionDir
        {
            get
            {
                if (_CurrentPartitionNumber == 0)
                    return _MainPartition;
                else
                    return _PartitionDirs[_CurrentPartitionNumber];
            }
        }

        public int CurrentPartitionNumber
        {
            get
            {
                return _CurrentPartitionNumber;
            }
        }

        internal DirectoryInfo GetDirByNumber(int partition)
        {
            if (partition == 0)
                return _MainPartition;
            else
                return _PartitionDirs[partition];
        }
    }
}
