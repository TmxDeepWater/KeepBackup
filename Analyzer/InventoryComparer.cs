using KeepBackup.OriginInventory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace KeepBackup.Analyzer
{
    class InventoryComparer : IAnalyzer
    {
        private readonly Inventory _A;
        private readonly Inventory _B;

        private readonly IList<IFile> _AllFilesA;
        private readonly IList<IFile> _AllFilesB;

        private readonly IDictionary<string, IList<IFile>> _AllFilesAByHash = new Dictionary<string, IList<IFile>>();
        private readonly IDictionary<string, IList<IFile>> _AllFilesBByHash = new Dictionary<string, IList<IFile>>();

        private readonly IDictionary<string, IFile> _AllFilesAByPath = new Dictionary<string, IFile>();
        private readonly IDictionary<string, IFile> _AllFilesBByPath = new Dictionary<string, IFile>();

        public InventoryComparer(Inventory a, Inventory b)
        {
            _A = a;
            _B = b;

            _AllFilesA = _A.Folder.AllFiles.ToList().AsReadOnly();
            _AllFilesB = _B.Folder.AllFiles.ToList().AsReadOnly();

            Pupulate(_AllFilesAByHash, _AllFilesA);
            Pupulate(_AllFilesBByHash, _AllFilesB);

            Pupulate2(_AllFilesAByPath, _AllFilesA);
            Pupulate2(_AllFilesBByPath, _AllFilesB);
        }

        private static void Pupulate(IDictionary<string, IList<IFile>> allFilesByHash, IList<IFile> allFiles)
        {
            foreach (IFile file in allFiles)
            {
                IList<IFile> files;
                if (allFilesByHash.TryGetValue(file.Sha256, out files))
                    files.Add(file);
                else
                {
                    files = new List<IFile>();
                    files.Add(file);
                    allFilesByHash.Add(file.Sha256, files);
                }
            }
        }

        private static void Pupulate2(IDictionary<string, IFile> allFilesByPath, IList<IFile> allFiles)
        {
            foreach (IFile file in allFiles)
            {
                string path = file.GetPathFromRoot();
                allFilesByPath.Add(path, file);
            }
        }

        public void Analyze()
        {
            Program.log.Info("Start Comparison");

            Program.log.Info("#### Vanished Files - SHA256 doesn't exist anymore ####");
            {
                var vanshedHashes = _AllFilesAByHash.Keys.Where(x => !_AllFilesBByHash.ContainsKey(x)).ToList();

                foreach (var x in vanshedHashes)
                {
                    IList<IFile> files = _AllFilesAByHash[x];
                    if (files.Count < 1)
                        throw new Exception();
                    else if (files.Count == 1)
                    {
                        string path = files[0].GetPathFromRoot();
                        if (_AllFilesBByPath.ContainsKey(path))
                            Program.log.Info("VANISHED (overwritten) " + path + " \"" + x + "\"");
                        else
                            Program.log.Info("VANISHED (removed) " + path + " \"" + x + "\"");
                    }
                    else
                    {
                        Program.log.Info("VANISHED GROUP " + x);
                        foreach (var f in files)
                        {
                            string path = f.GetPathFromRoot();
                            if (_AllFilesBByPath.ContainsKey(path))
                                Program.log.Info("   overwritten " + path + " \"" + x + "\"");
                            else
                                Program.log.Info("   removed " + path + " \"" + x + "\"");
                        }
                            
                    }

                }
            }
            
            Program.log.Info("#### New Files - SHA256 didn't exist before ####");
            {
                var newHashes = _AllFilesBByHash.Keys.Where(x => !_AllFilesAByHash.ContainsKey(x)).ToList();

                foreach (var x in newHashes)
                {
                    IList<IFile> files = _AllFilesBByHash[x];
                    if (files.Count < 1)
                        throw new Exception();
                    else if (files.Count == 1)
                    {
                        string path = files[0].GetPathFromRoot();
                        if (_AllFilesAByPath.ContainsKey(path))
                            Program.log.Info("NEW (overwritten) \"" + path + "\" " + x);
                        else
                            Program.log.Info("NEW \"" + path + "\" " + x);
                    }
                    else
                    {
                        Program.log.Info("NEW GROUP " + x);
                        foreach (var f in files)
                        {
                            string path = f.GetPathFromRoot();
                            if (_AllFilesAByPath.ContainsKey(path))
                                Program.log.Info("   new: " + f.GetPathFromRoot() + "\"");
                            else
                                Program.log.Info("   overwritten: " + f.GetPathFromRoot() + "\"");
                        }
                    }
                }
            }

            Program.log.Info("#### Moved Files - SHA256 did exist in different locations (paths) ####");
            {
                var hashes = _AllFilesBByHash.Keys.Where(x => _AllFilesAByHash.ContainsKey(x)).ToList();

                foreach (var h in hashes)
                {
                    var oldLocations = _AllFilesAByHash[h].Select(x => x.GetPathFromRoot()).OrderBy(x => x).ToList();
                    var newLocations = _AllFilesBByHash[h].Select(x => x.GetPathFromRoot()).OrderBy(x => x).ToList();

                    if (oldLocations.SequenceEqual(newLocations))
                        continue;

                    Program.log.Info("LOCS CHANGED " + h);

                    foreach (var oldLoc in oldLocations)
                    {
                        if (newLocations.Contains(oldLoc))
                            continue;

                        if (_AllFilesBByPath.ContainsKey(oldLoc))
                            Program.log.Info("  REMOVED (overwritten) \"" + oldLoc + "\"");
                        else
                            Program.log.Info("  REMOVED \"" + oldLoc + "\"");
                    }

                    foreach (var newLoc in newLocations)
                    {
                        if (oldLocations.Contains(newLoc))
                            continue;

                        if (_AllFilesAByPath.ContainsKey(newLoc))
                            Program.log.Info("  NEW (overwritten) \"" + newLoc + "\"");
                        else
                            Program.log.Info("  NEW \"" + newLoc + "\"");
                    }

                    foreach (var newLoc in newLocations)
                        if (oldLocations.Contains(newLoc))
                            Program.log.Info("  UNCHANGED \"" + newLoc+ "\"");
                }


            }




        }







    }
}
