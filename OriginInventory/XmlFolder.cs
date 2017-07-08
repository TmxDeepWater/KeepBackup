using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace KeepBackup.OriginInventory
{
    class XmlFolder : AbstractFolder
    {
        private XmlFile[] _Files;
        private XmlFolder[] _SubFolders;        

        public XmlFolder(XElement xml)
        {
            Name = xml.Attribute("name").Value;

            _Files = xml.Element("Files").Elements("File").Select(x => new XmlFile(this, x)).ToArray();
            _SubFolders = xml.Element("Folders").Elements("Folder").Select(x => new XmlFolder(this, x)).ToArray();
        }

        public XmlFolder(IFolder parent, XElement x) : this (x)
        {
            Parent = parent;
        }

        public override IEnumerable<IFile> Files
        {
            get { return _Files; }
        }

        public override IEnumerable<IFolder> SubFolders
        {
            get { return _SubFolders; }
        }

        public override void ReuseSha256s(IFolder root)
        {
            throw new NotImplementedException();
        }
    }
}
