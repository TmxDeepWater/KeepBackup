using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace KeepBackup.OriginInventory
{
    class XmlFile : AbstactFile
    {
        private string _Sha256;

        public XmlFile(IFolder folder, XElement x)
        {
            Folder = folder;

            Name = x.Attribute("name").Value;
            CreationTimeUtc = XmlConvert.ToDateTime(x.Attribute("creationTimeUtc").Value, XmlDateTimeSerializationMode.Utc);
            LastWriteTimeUtc = XmlConvert.ToDateTime(x.Attribute("lastWriteTimeUtc").Value, XmlDateTimeSerializationMode.Utc);
            SizeBytes = long.Parse(x.Attribute("sizeBytes").Value);

            _Sha256 = x.Attribute("sha256").Value;
        }

        public override string Sha256
        {
            get { return _Sha256; }
        }

        public override void ReuseSha256s(IFile matching)
        {
            throw new NotSupportedException();
        }

        public override FileInfo GetFileInfo()
        {
            throw new NotSupportedException();
        }
    }
}
