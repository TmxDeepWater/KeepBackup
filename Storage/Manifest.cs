using KeepBackup.OriginInventory;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace KeepBackup.Storage
{
    class Manifest// : IDisposable
    {
        private const string MANIFEST_FILENAME = "KeepBackup-ObjectStore.manifest";

        private readonly FileInfo _ManifestFile;
        private readonly XDocument _Xml;

        private Dictionary<string,int> _Hashes2Partition = new Dictionary<string, int>(StringComparer.Ordinal);

        public Manifest(DirectoryInfo dir)
        {
            _ManifestFile = new FileInfo(Path.Combine(dir.FullName, MANIFEST_FILENAME));

            if (!_ManifestFile.Exists)
                _Xml = GetNewManifest();
            else
                _Xml = OpenManifest();
        }

        private XDocument OpenManifest()
        {
            XDocument xml = XDocument.Load(_ManifestFile.FullName);

            foreach (var m in ManifestObjects)
                _Hashes2Partition.Add(m.Sha256source, m.Partition);

            long sizeSource = 0;
            long sizeTarget = 0;

            foreach (var m in ManifestObjects)
            {
                sizeSource += m.SizeSource;
                sizeTarget += m.SizeTarget;
            }

            double sizeSourceMB = sizeSource / (1024.0 * 1024.0);
            double sizeTargetMB = sizeTarget / (1024.0 * 1024.0);

            double percent = (sizeTargetMB / sizeSourceMB) * 100.0;

            Program.log.InfoFormat("manifest: {0:0.00} MB ({1:0.00} MB compressed) -> {2:0.00}% ratio", sizeSourceMB, sizeTargetMB, percent);

            return xml;
        }

        internal int GetPartition(string sha256)
        {
            return _Hashes2Partition[sha256];
        }

        private XDocument GetNewManifest()
        {
            return new XDocument(
                new XDeclaration("1.0", "UTF-8", "yes"),
                new XElement("KeepBackup-ObjektStore-Manifest")
                );
        }

        public IEnumerable<ManifestItem> ManifestObjects
        {
            get
            {
                foreach (var x in _Xml.Root.Elements("File"))
                    yield return new ManifestItem(x);
            }
        }

        public int HighestPartition
        {
            get
            {
                return ManifestObjects.Select(x => x.Partition).Max();
            }
        }

        public void Add(IFile file, string targetHash, long targetSize, DateTime timestamp, int partition)
        {
            lock (this)
            {
                _Xml.Root.Add(
                    new XElement("File",
                            new XAttribute("sha256source", file.Sha256),
                            new XAttribute("sha256target", targetHash),
                            new XAttribute("sizeSource", file.SizeBytes),
                            new XAttribute("sizeTarget", targetSize),
                            (partition != 0) ? new XAttribute("partition", partition.ToString()) : null
                    )
                );
                Save();

                _Hashes2Partition.Add(file.Sha256, partition);
            }
        }

        internal bool ContainsSha(string sha256)
        {
            return _Hashes2Partition.ContainsKey(sha256);
        }

        internal void Garbage(string sha, DateTime timestamp)
        {
            XElement fileEle = _Xml.Root.Elements("File").Where(x => x.Attribute("sha256source").Value.Equals(sha)).Single();
            fileEle.Remove();

            XElement garbage = _Xml.Root.Element("Garbage");
            if (garbage == null)
            {
                garbage = new XElement("Garbage");
                _Xml.Root.AddFirst(garbage);
            }

            garbage.Add(fileEle);

            Save();
            _Hashes2Partition.Remove(sha);
        }

        public void Save()
        {
            _Xml.Save(_ManifestFile.FullName);
        }
    }
}
