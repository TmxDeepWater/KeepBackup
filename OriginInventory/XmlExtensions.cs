using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml;
using System.Xml.Linq;

namespace KeepBackup.OriginInventory
{
    static class XmlExtensions
    {
        public static XElement ToXml(this IFile file)
        {
            return new XElement("File",
                new XAttribute("name", file.Name),
                new XAttribute("lastWriteTimeUtc", XmlConvert.ToString(file.LastWriteTimeUtc, XmlDateTimeSerializationMode.Utc)),
                new XAttribute("creationTimeUtc", XmlConvert.ToString(file.CreationTimeUtc, XmlDateTimeSerializationMode.Utc)),
                new XAttribute("sizeBytes", file.SizeBytes),
                new XAttribute("sha256", file.Sha256)
                );
        }

        public static XElement ToXml(this IFolder folder)
        {
            return new XElement("Folder",
                new XAttribute("name", folder.Name),
                new XElement("Folders",
                    from x in folder.SubFolders select x.ToXml()
                    ),
                new XElement("Files",
                    from x in folder.Files select x.ToXml()
                    )
                );
        }

    }
}
