using KeepBackup.OriginInventory;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace KeepBackup.Storage
{
    internal abstract class AbstractFileJob
    {
        private readonly string _FromPath;
        private readonly string _PendingPath;
        private readonly string _ToPath;
        
        private long? _SizeTarget;
        private string _ChecksumTarget;

        public AbstractFileJob(string fromPath, string pendingPath, string toPath)
        {
            _FromPath = fromPath;
            _PendingPath = pendingPath;
            _ToPath = toPath;
        }

        public string FromPath
        {
            get { return _FromPath; }
        }

        protected string PendingPath
        {
            get { return _PendingPath; }
        }

        public string ToPath
        {
            get { return _ToPath; }
        }

        public long? SizeTarget
        {
            get { return _SizeTarget; }
            protected set { _SizeTarget = value; }
        }

        public string ChecksumTarget
        {
            get { return _ChecksumTarget; }
            protected set { _ChecksumTarget = value; }
        }

        public abstract void Execute();

        protected void EnsurePathExists(string target)
        {
            Program.log.Debug("EnsurePathExists " + target);

            int count = 0;
            while (count < 10)
            {
                try
                {
                    count++;

                    FileInfo fi = new FileInfo(target);
                    DirectoryInfo di = fi.Directory;

                    if (di.Exists)
                    {
                        Program.log.Debug("already exists " + di.FullName);
                        return;
                    }
                    else
                    {
                        Program.log.Debug("not existing " + di.FullName);
                    }

                    Program.log.Debug("create directory " + di.FullName);

                    di.Create();
                    di.Refresh();

                    if (!di.Exists)
                    {
                        Program.log.Debug("the just created directory does not exist!");
                        throw new Exception("the just created directory does not exist!");
                    }
                    else
                    {
                        return;
                    }
                }
                catch (Exception e)
                {
                    Program.log.Debug(e.GetType().Name);
                    Program.log.Debug("retry");
                    Thread.Sleep(5000);
                }
            }
            Program.log.Fatal("could not create directory "+target);
            Environment.Exit(-1);
        }
    }
}
