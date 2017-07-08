using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace KeepBackup.Storage
{
    public class ManifestItem
    {
        public string Sha256source { get; private set; }
        public string Sha256target { get; private set; }
        public long SizeSource { get; private set; }
        public long SizeTarget { get; private set; }
        public int Partition { get; private set; }

        public ManifestItem(XElement x)
        {
            Sha256source = x.Attribute("sha256source").Value;
            Sha256target = x.Attribute("sha256target").Value;
            SizeSource = long.Parse(x.Attribute("sizeSource").Value);
            SizeTarget = long.Parse(x.Attribute("sizeTarget").Value);
            var partitionAttr = x.Attribute("partition");
            if (partitionAttr == null)
                Partition = 0;
            else
                Partition = int.Parse(partitionAttr.Value);
        }
    }
}
