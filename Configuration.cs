using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace KeepBackup
{
    class Configuration
    {
        private readonly string _Password;
        private readonly string _Salt;
        private readonly IList<string> _DirectoriesBlacklistEndsWith;

        public Configuration()
        {
            FileInfo fi = new FileInfo("KeepBackupConfiguration.xml");
            if (!fi.Exists)
                Program.log.Error("missing configuraton file " + fi.FullName);

            XDocument xml = XDocument.Load(fi.FullName);

            _Password = xml.Root.Element("Configuration").Element("Enrcyption").Element("Password").Attribute("passwd").Value;
            _Salt = xml.Root.Element("Configuration").Element("Enrcyption").Element("Password").Attribute("salt").Value;

            var dirs = xml.Root.Element("Configuration").Element("Backlisting").Element("Directories").Elements("Directory");
            _DirectoriesBlacklistEndsWith = dirs.Select(x => x.Attribute("endsWith").Value).ToList();
        }

        public string Password
        {
            get { return _Password; }
        }

        public string Salt
        {
            get { return _Salt; }
        }

        public bool IsBlacklisted(FileInfo fi, out bool log)
        {
            if (fi.Name.StartsWith("KeepBackup", StringComparison.OrdinalIgnoreCase) &&
                fi.Name.EndsWith(".inventory", StringComparison.OrdinalIgnoreCase))
            {
                log = false;
                return true;
            }

            if (fi.Name.Equals("Thumbs.db", StringComparison.OrdinalIgnoreCase))
            {
                log = true;
                return true;
            }

            log = false;
            return false;
        }
        public bool IsBlacklisted(DirectoryInfo di, out bool log)
        {
            if (_DirectoriesBlacklistEndsWith.Any(x => di.Name.EndsWith(x, StringComparison.Ordinal)))
            {
                log = true;
                return true;
            }

            log = false;
            return false;
        }
    }
}
