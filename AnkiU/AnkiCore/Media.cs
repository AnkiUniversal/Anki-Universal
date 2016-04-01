using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;
using System.IO;
using Windows.Storage;
using System.Threading.Tasks;
using Windows.Security.Cryptography;
using Windows.Storage.FileProperties;
using System.Diagnostics;
using Windows.Data.Json;
using System.Linq;
using Windows.Storage.Compression;
using Windows.Storage.Streams;
using System.IO.Compression;

namespace AnkiU.AnkiCore
{
    
    class Media
    {
        private readonly static Regex illegalCharRegex;
        private readonly static Regex fRemoteRegex;

     
        //Group 1 = Contents of [sound:] tag <br>
        //Group 2 = "fname"
        private readonly static Regex fSoundRegExps;

        
        //Group 1 = Contents of<img> tag<br>
        //Group 2 = "str" <br>
        //Group 3 = "fname" <br>
        //Group 4 = Backreference to "str" (i.e., same type of quote character)
        private readonly static Regex fImgRegExpQuote;

        //Group 1 = Contents of <img> tag <br>
        //Group 2 = "fname"
        private readonly static Regex fImgRegExpUnQuote;

        private Collection collection;
        private string mediaFolderName;
        private StorageFolder mediaFolder;
        private StorageFolder folder;
        private DB database;

        private static System.Globalization.CultureInfo locale = new System.Globalization.CultureInfo("en-US");

        public static List<Regex> fImgRegExps = new List<Regex>();
        public static List<Regex> RegExps = new List<Regex>();

        public int LastUsn
        {
            get { return database.QueryScalar<int>("select lastUsn from meta"); }
            set { database.Execute("update meta set lastUsn = ?", new object[] { value }); }
        }

        static Media()
        {
            string regexSearch = new string(Path.GetInvalidFileNameChars()) + new string(Path.GetInvalidPathChars());

            illegalCharRegex = new Regex(string.Format("[{0}]", Regex.Escape(regexSearch)), RegexOptions.Compiled);
            fRemoteRegex = new Regex("(https?|ftp)://", RegexOptions.Compiled);
            fSoundRegExps = new Regex(@"(?i)(\[sound:(?<fname>[^]]+)\])", RegexOptions.Compiled);
            fImgRegExpQuote = new Regex(@"(?i)(\<img[^>]* src=(?<str>[\""'])(?<fname>[^>]+?)(\k<str>)[^>]*\>)", RegexOptions.Compiled);
            fImgRegExpUnQuote = new Regex(@"(?i)(\<img[^>]* src=(?!['\""])(?<fname>[^ >]+)[^>]*?\>)", RegexOptions.Compiled);

            fImgRegExps.Add(fImgRegExpQuote);
            fImgRegExps.Add(fImgRegExpUnQuote);

            RegExps.Add(fSoundRegExps);
            RegExps.Add(fImgRegExpQuote);
            RegExps.Add(fImgRegExpUnQuote);
        }

        public Media(Collection colection, bool server)
            : this(colection, server, ApplicationData.Current.LocalFolder)
        { }

        public Media(Collection colection, bool server, StorageFolder folder)
        {
            this.folder = folder;
            this.collection = colection;
            if (server)
            {
                mediaFolderName = null;
                return;
            }

            mediaFolderName = colection.GetPath().ReplaceFirst("\\.anki2$", ".media");
            Task task = Task.Factory.StartNew(() =>
            {
                CreateFolder();
            });
            task.Wait();

            if (folder == null)
                throw new Exception("Cannot create media directory: " + mediaFolderName);

            Connect();
        }

        public async void CreateFolder()
        {
            mediaFolder = await folder.CreateFolderAsync(mediaFolderName, CreationCollisionOption.OpenIfExists);
        }

        public void Connect()
        {
            if (collection.IsServer)
                return;

            // NOTE: Similar to AnkiDroid, use a custom prefix for AnkiU to avoid issues caused by copying
            // the db to the desktop or vice versa.
            // Consider revert it to ".db2" after throughout testing
            string path = mediaFolderName + ".au.db2";
            database = new DB(path);
            Task task = Task.Factory.StartNew(() =>
            {
                MakeSureFileExist(path);
            });
            task.Wait();

            task = Task.Factory.StartNew(() =>
            {
                MaybeUpgrade();
            });
            task.Wait();
        }

        private async void MakeSureFileExist(string fileName)
        {
            StorageFile store = (await folder.TryGetItemAsync(fileName)) as StorageFile;
            if (store == null)
                InitDB();
        }

        public void InitDB()
        {
            string sql = "create table media (\n" +
                     " fname text not null primary key,\n" +
                     " csum text,           -- null indicates deleted file\n" +
                     " mtime int not null,  -- zero if deleted\n" +
                     " dirty int not null\n" +
                     ");\n" +
                     "create index idx_media_dirty on media (dirty);\n" +
                     "create table meta (dirMod int, lastUsn int); insert into meta values (0, 0);";
            database.ExecuteScript(sql);
        }

        public async void MaybeUpgrade()
        {
            string oldpath = mediaFolderName + ".db";
            StorageFile oldFile = (await folder.TryGetItemAsync(oldpath)) as StorageFile;
            if (oldFile != null)
            {
                database.Execute(String.Format("attach \"{0}\" as old", oldpath));
                try
                {
                    string sql = "insert into media\n" +
                                 " select m.fname, csum, mod, ifnull((select 1 from log l2 where l2.fname=m.fname), 0) as dirty\n" +
                                 " from old.media m\n" +
                                 " left outer join old.log l using (fname)\n" +
                                 " union\n" +
                                 " select fname, null, 0, 1 from old.log where type=1;";
                    database.Execute(sql);
                    database.Execute("delete from meta");
                    database.Execute("insert into meta select dirMod, usn from old.meta");
                }
                catch (Exception e)
                {
                    collection.Log("failed to import old media db:" + e.StackTrace.ToString());
                }

                database.Execute("detach old");
                StorageFile newDbFile = (await folder.TryGetItemAsync(oldpath + ".old")) as StorageFile;
                if (newDbFile != null)
                    await newDbFile.DeleteAsync();
                await oldFile.RenameAsync(newDbFile.Name);
            }
        }

        public void Close()
        {
            if (collection.IsServer)
                return;

            database.Close();
            database = null;
        }

        public async Task<string> AddFile(string oPath)
        {
            StorageFile oFile = await folder.GetFileAsync(oPath);
            string fName = await WriteData(oFile);
            MarkFileAdd(fName);
            return fName;
        }

        private async Task<string> WriteData(StorageFile sourceFile)
        {
            string fileName = Path.GetFileName(sourceFile.Path);
            Debug.WriteLine(sourceFile.Path);
            string normalize = fileName.Normalize(NormalizationForm.FormC);

            string legalName = StripIllegal(normalize);
            string[] split = Utils.SplitNameAndExtension(legalName);
            string root = split[0];
            string ext = split[1];

            string checkSum = Utils.FileChecksum(sourceFile).Result;

            StorageFile newFile;
            while (true)
            {
                fileName = root + ext;
                string path = mediaFolderName + "\\" + fileName;
                newFile = (await folder.TryGetItemAsync(path)) as StorageFile;

                if (newFile == null)
                {
                    newFile = await sourceFile.CopyAsync(mediaFolder);
                    await newFile.RenameAsync(fileName);
                    return fileName;
                }

                if (Utils.FileChecksum(newFile).Result.Equals(checkSum, StringComparison.OrdinalIgnoreCase))
                {
                    return fileName;
                }

                Regex reg = new Regex(@" \((\d+)\)$", RegexOptions.Compiled);
                MatchCollection matches = reg.Matches(root);
                if (matches.Count > 0)
                {
                    Match match = matches[0];
                    GroupCollection groupCollection = match.Groups;
                    int n = int.Parse(groupCollection[1].ToString());
                    root = String.Format(" ({0})", n + 1);
                    Debug.WriteLine(root);
                }
                else
                    root = root + " (1)";
            }
        }

        public void MarkFileAdd(string fileName)
        {
            string path = folder.Path + "\\" + fileName;
            database.Execute("insert or replace into media values (?,?,?,?)",
                new object[] { fileName, Checksum(fileName), GetTimeLastModified(path), 1 });
        }

        /// <summary>
        /// Return the checksum of a file
        /// </summary>
        /// <param name="path">Relative path</param>
        /// <param name="sFolder">Parent folder</param>
        /// <returns></returns>
        private string Checksum(string path, StorageFolder sFolder = null)
        {
            Task<string> task = Task<string>.Factory.StartNew(() =>
            {
                StorageFile file;
                if (sFolder == null)
                    file = GetFileAsync(folder, path).Result;
                else
                    file = GetFileAsync(sFolder, path).Result;

                return Utils.FileChecksum(file).Result;
            });
            task.Wait();
            return task.Result;
        }

        private async Task<StorageFile> GetFileAsync(StorageFolder sFolder, string path)
        {
            return await sFolder.GetFileAsync(path);
        }

        private long GetTimeLastModified(string path)
        {
            Task<BasicProperties> task = Task<BasicProperties>.Factory.StartNew(() =>
            {
                BasicProperties prop = GetBasicProperties(path).Result;
                return prop;
            });
            task.Wait();
            BasicProperties property = task.Result;
            return property.DateModified.ToUnixTimeSeconds();
        }

        private async Task<BasicProperties> GetBasicProperties(string path)
        {
            StorageFile file = await folder.GetFileAsync(path);
            return await file.GetBasicPropertiesAsync();
        }

        public string StripIllegal(string str)
        {
            return illegalCharRegex.Replace(str, "");
        }

        public bool HasIllegal(string str)
        {
            return illegalCharRegex.IsMatch(str);
        }

        public List<string> FilesInStr(long mid, string strIn, bool includeRemote = false)
        {
            List<string> returnList = new List<string>();
            List<string> stringList = new List<string>();
            JsonObject model = collection.GetModels().get(mid);

            int type = (int)model.GetNamedNumber("type");
            if ((type == (int)ModelType.CLOZE) && strIn.Contains("{{c"))
                stringList = ExpandClozes(strIn);
            else
                stringList.Add(strIn);

            string str;
            foreach (string s in stringList)
            {
                str = LaTeX.mungeQA(s, collection);
                MatchCollection matches;
                foreach (Regex p in RegExps)
                {
                    matches = p.Matches(str);
                    foreach (Match m in matches)
                    {
                        string fName = m.Groups["fname"].ToString();
                        bool isLocal = !fRemoteRegex.IsMatch(fName.ToLower());
                        if (isLocal || includeRemote)
                            returnList.Add(fName);
                    }
                }
            }
            return returnList;
        }

        private List<string> ExpandClozes(string str)
        {
            List<string> stringList = new List<string>();
            SortedSet<string> ords = new SortedSet<string>();
            string clozeReg = Template.Template.clozeReg;

            Regex reg = new Regex(@"{{c(\d+)::.+?}}", RegexOptions.Compiled);
            MatchCollection matches = reg.Matches(str);
            if (matches.Count > 0)
                foreach (Match m in matches)
                    ords.Add(m.Groups[1].ToString());

            string replacePattern = String.Format(locale, clozeReg, ".+?");
            foreach (string ord in ords)
            {
                StringBuilder sBuild = new StringBuilder();
                string pattern = String.Format(locale, clozeReg, ord);
                matches = (new Regex(pattern)).Matches(str);
                foreach (Match m in matches)
                {
                    if (String.IsNullOrEmpty(m.Groups[3].ToString()))
                        sBuild.AppendAndReplace("[$3]", str, m.Index);
                    else
                        sBuild.AppendAndReplace("[...]", str, m.Index);
                }
                sBuild.AppendTail(str);
                string s = sBuild.ToString().Replace(replacePattern, "$1");
                stringList.Add(s);
            }
            stringList.Add(str.Replace(replacePattern, "$1"));
            return stringList;
        }

        public string Strip(string txt)
        {
            foreach (Regex p in RegExps)
                txt = p.Replace(txt, "");
            return txt;
        }

        public string EscapeImages(string str, bool unescape = false)
        {
            foreach (Regex pattern in fImgRegExps)
            {
                MatchCollection matches = pattern.Matches(str);
                foreach (Match m in matches)
                {
                    string tag = m.Groups[0].ToString();
                    string fName = m.Groups["fname"].ToString();
                    if (!fRemoteRegex.IsMatch(fName))
                    {
                        if (unescape)
                            str = str.Replace(tag, tag.Replace(fName, fName.UrlDecode()));
                        else
                            str = str.Replace(tag, tag.Replace(fName, fName.UrlEncode()));
                    }
                }
            }
            return str;
        }

        public async Task<List<List<string>>> Check(StorageFile[] local)
        {
            HashSet<string> allRefs = new HashSet<string>();
            var arrayNote = (from s in collection.Database.QueryColumn<Note>("select id, mid, flds from notes")
                            select new { s.Id, s.Mid, s.JointFields}).ToArray();

            foreach (var note in arrayNote)
            {
                List<string> noteRefs = FilesInStr(note.Mid, note.JointFields);
                foreach (string f in noteRefs)
                {
                    if (!f.IsNormalized(NormalizationForm.FormC))
                    {
                        NormalizeNoteRefs(note.Id);
                        noteRefs = FilesInStr(note.Mid, note.JointFields);
                        break;
                    }
                }
                Utils.AddAll<string>(allRefs, noteRefs);
            }

            List<string> unUsed = new List<string>();
            //Warning: Invalid is kept for compatible with the source code in python
            //Since window is unicode base, we won't take for file that does not have unicode coding
            List<string> inValid = new List<string>();
            StorageFile[] files;
            if (local == null)
                files = (await mediaFolder.GetFilesAsync()).ToArray<StorageFile>();
            else
                files = local;

            bool isRenamedFiles = false;
            for(int i = 0; i < files.Length; i++)
            {
                if (files[i].Name.StartsWith("_")) { 
                    // leading _ says to ignore file
                    continue;
                }
                StorageFile nfcFile = await mediaFolder.GetFileAsync(files[i].Name.Normalize(NormalizationForm.FormC));
                if(local == null)
                {
                    if(!files[i].Name.Equals(nfcFile.Name, StringComparison.OrdinalIgnoreCase))
                        await files[i].DeleteAsync();
                    else
                        await files[i].RenameAsync(nfcFile.Name);
                    isRenamedFiles = true;
                    files[i] = nfcFile;
                }
                if (!allRefs.Contains(nfcFile.Name))
                    unUsed.Add(files[i].Name);
                else
                    allRefs.Remove(nfcFile.Name);
            }

            // if we renamed any files to nfc format, we must rerun the check
            // to make sure the renamed files are not marked as unused
            if (isRenamedFiles)
                return await Check(local);

            List<string> notHave = new List<string>();
            foreach (string x in allRefs)
                if (x.StartsWith("_"))
                    notHave.Add(x);

            List<List<string>> result = new List<List<string>>();
            result.Add(notHave);
            result.Add(unUsed);
            result.Add(inValid);
            return result;
        }

        private void NormalizeNoteRefs(long noteID)
        {
            Note note = collection.GetNote(noteID);
            string[] flds = note.Fields;
            for (int i = 0; i < flds.Length; i++)
            {
                string fld = flds[i];
                string nfc = fld.Normalize(NormalizationForm.FormC);
                if (!nfc.Equals(fld, StringComparison.OrdinalIgnoreCase))
                {
                    note.SetField(i, nfc);
                }
            }
            note.Flush();
        }

        public async Task<bool> IsHave(string fName)
        {
            StorageFile f = await mediaFolder.TryGetItemAsync(fName) as StorageFile;
            if (f == null)
                return false;
            return true;
        }

        public async Task findChanges(bool force = false)
        {
            if ((folder == null) || (await Changed() != null))
                await LogChanges();
        }

        public async Task<long?> Changed()
        {
            long mod = database.QueryScalar<long>("select dirMod from meta");
            long time = await GetTimeLastModified(mediaFolder);
            if (mod != 0 && mod == time)
                return null;

            return time;
        }

        /// <summary>
        /// Get the last modified time in seconds according to Unix time
        /// </summary>
        /// <param name="fileName">File name in sFolder</param>
        /// <param name="sFolder">Folder stores the file wanted to check</param>
        /// <returns></returns>
        private async Task<long> GetTimeLastModified(string fileName, StorageFolder sFolder)
        {
            StorageFile file = await sFolder.GetFileAsync(fileName);
            return (await file.GetBasicPropertiesAsync()).DateModified.ToUnixTimeSeconds();
        }

        /// <summary>
        /// Get the last modified time in seconds according to Unix time
        /// </summary>
        /// <param name="sFolder"></param>
        /// <returns></returns>
        private async Task<long> GetTimeLastModified(StorageFolder sFolder)
        {
            return (await sFolder.GetBasicPropertiesAsync()).DateModified.ToUnixTimeSeconds();
        }

        /// <summary>
        /// Get the last modified time in seconds according to Unix time
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        private async Task<long> GetTimeLastModified(StorageFile file)
        {
            return (await file.GetBasicPropertiesAsync()).DateModified.ToUnixTimeSeconds();
        }

        private async Task LogChanges()
        {
            List<List<string>> result = await Changes();
            List<string> added = result[0];
            List<string> removed = result[1];
            List<object[]> media = new List<object[]>();
            foreach(string f in added)
            {
                long modifiedTime = await GetTimeLastModified(f, mediaFolder);
                media.Add(new object[] { f, Checksum(f, mediaFolder), modifiedTime, 1 });
            }
            foreach (string f in removed)
                media.Add(new object[] { f, null, 0, 1 });

            database.ExecuteMany("insert or replace into media values (?,?,?,?)", media);
            database.Execute("update meta set dirMod = ?", new object[] { await GetTimeLastModified(mediaFolder)});
        }

        private async Task<List<List<string>>> Changes()
        {
            Dictionary<string, object[]> cache = new Dictionary<string, object[]>();
            var array = (from s in database.QueryColumn<MediaDB>("select fname, csum, mtime from media where csum is not null")
                        select new { s.FileName, s.CheckSum, s.ModifiedTime}
                        ).ToArray();
            for(int i = 0; i < array.Length; i++)
                cache.Add(array[i].FileName, new object[] { array[i].CheckSum, array[i].ModifiedTime, false });

            List<string> added = new List<string>();
            List<string> removed = new List<string>();
            foreach (StorageFile file in await mediaFolder.GetFilesAsync())
            {
                if (file.Name.Equals("thumbs.db", StringComparison.CurrentCultureIgnoreCase))
                    continue;
                if (HasIllegal(file.Name))
                    continue;

                //Delete file if empty
                ulong fsize = (await file.GetBasicPropertiesAsync()).Size;
                if (fsize == 0)
                {
                    await file.DeleteAsync();
                    continue;
                }

                if (fsize > 100 * 1024 * 1024)
                {
                    collection.Log("ignoring file over 100MB", file);
                    continue;
                }

                if(!file.Name.IsNormalized())
                    await file.RenameAsync(file.Name.Normalize(NormalizationForm.FormC), 
                                            NameCollisionOption.ReplaceExisting);

                //Newly added
                if (!cache.ContainsKey(file.Name))
                    added.Add(file.Name);
                else
                { //Modified since last time?
                    if ( (await GetTimeLastModified(file)) != (long)cache[file.Name][1])
                        if (!Checksum(file.Name, mediaFolder).Equals(cache[file.Name][0]))
                            added.Add(file.Name);

                    cache[file.Name][2] = true;
                }
            }
            foreach (string fname in cache.Keys)
                if (!(bool)cache[fname][2])
                    removed.Add(fname);

            List<List<string>> results = new List<List<string>>();
            results.Add(added);
            results.Add(removed);
            return results;
        }

        public bool HaveDirty()
        {
            return database.QueryScalar<int>("select 1 from media where dirty=1 limit 1") > 0;
        }

        public KeyValuePair<string, int> GetSyncInfo(string fName)
        {
            var array = (from s in database.QueryFirstRow<MediaDB>("select csum, dirty from media where fname=?", new string[] { fName })
                         select new { s.CheckSum, s.Dirty }).ToArray();
            if (array[0] == null)
                return new KeyValuePair<string, int>(null, 0);
            else
                return new KeyValuePair<string, int>(array[0].CheckSum, array[0].Dirty);
        }

        public void MarkClean(List<string> fNames)
        {
            foreach (string fName in fNames)
                database.Execute("update media set dirty=0 where fname=?", new object[] { fName });
        }

        public async Task SyncDelete(string fName)
        {
            StorageFile file = await mediaFolder.TryGetItemAsync(fName) as StorageFile;
            if (file != null)
                await file.DeleteAsync();
            database.Execute("delete from media where fname=?", new object[] { fName });
        }

        public int MediaCount()
        {
            return database.QueryScalar<int>("select count() from media where csum is not null");
        }

        public int DirtyCount()
        {
            return database.QueryScalar<int>("select count() from media where dirty=1");
        }

        public void ForceReSync()
        {
            database.Execute("delete from media");
            database.Execute("update meta set lastUsn=0,dirMod=0");
            database.Execute("vacuum analyze");
        }

        public async Task<KeyValuePair<StorageFile, List<string>>> MediaChangesZip()
        {
            string name = collection.GetPath().ReplaceFirst("collection\\.anki2$", "tmpSyncToServer.zip");
            var compressedFile = await folder.CreateFileAsync(name, CreationCollisionOption.ReplaceExisting);

            List<string> fNames = new List<string>();
            JsonArray meta = new JsonArray();
            ulong size = 0;
            var array = (from s in database.QueryColumn<MediaDB>("select fname, csum from media where dirty=1 limit " + Syncing.ZIP_COUNT)
                         select new { s.FileName, s.CheckSum }).ToArray();

            using (FileStream zipToOpen = new FileStream(compressedFile.Path, FileMode.Open))
            {
                using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Update))
                {
                    for (int i = 0; i < array.Length; i++)
                    {
                        fNames.Add(array[i].FileName);
                        string normName = array[i].FileName.Normalize(NormalizationForm.FormC);

                        if (!String.IsNullOrEmpty(array[i].CheckSum))
                        {
                            collection.Log("+media zip" + array[i].FileName);
                            StorageFile file = await mediaFolder.TryGetItemAsync(array[i].FileName) as StorageFile;
                            if (file != null)
                            {
                                archive.CreateEntryFromFile(file.Path, i.ToString());
                                meta.Add(new JsonArray {
                                            JsonValue.CreateStringValue(normName),
                                            JsonValue.CreateStringValue(i.ToString())
                                            });
                                size += (await file.GetBasicPropertiesAsync()).Size;
                            }
                            else
                                await RemoveFile(array[i].FileName);
                        }
                        else
                        {
                            collection.Log("-media zip " + array[i].FileName);
                            meta.Add(new JsonArray {
                                            JsonValue.CreateStringValue(normName),
                                            JsonValue.CreateStringValue("")
                                            });
                        }
                        if (size >= Syncing.ZIP_SIZE)
                            break;
                    }
                    ZipArchiveEntry metaEntry =  archive.CreateEntry("_meta");
                    using (StreamWriter writer = new StreamWriter(metaEntry.Open()))
                    {
                        writer.WriteLine(Utils.JsonToString(meta));
                    }
                    
                }
            }
            return new KeyValuePair<StorageFile, List<string>>(compressedFile, fNames);
        }

        /// <summary>
        /// Remove a file in media folder and database
        /// </summary>
        /// <param name="fName">Name of the file in media folder</param>
        /// <returns></returns>
        public async Task RemoveFile(string fName)
        {
            StorageFile file = await mediaFolder.TryGetItemAsync(fName) as StorageFile;
            if (file != null)
                await file.DeleteAsync();

            database.Execute("insert or replace into media values (?,?,?,?)",
                new object[] { fName, null, 0, 1 });
        }

        public async Task<int> addFilesFromZip(string pathToFile)
        {
            List<object[]> media = new List<object[]>();
            JsonObject meta;
            int count = 0;

            using (FileStream zipToOpen = new FileStream(pathToFile, FileMode.Open))
            {
                using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Read))
                {
                    ZipArchiveEntry metaEntry = archive.GetEntry("_meta");
                    using (StreamReader reader = new StreamReader(metaEntry.Open()))
                        meta = JsonObject.Parse(reader.ReadToEnd());
                    
                    foreach(ZipArchiveEntry entry in archive.Entries)
                    {
                        if (entry.Name.Equals("_meta"))
                            continue;
                            
                        string name = meta.GetNamedString(entry.Name).Normalize(NormalizationForm.FormC);
                        string destPath = mediaFolder.Path + "\\" + name;
                        entry.ExtractToFile(destPath);
                        StorageFile newFile = await mediaFolder.GetFileAsync(name);
                        string checkSum = await Utils.FileChecksum(newFile);
                        media.Add(new object[] { name, checkSum, GetTimeLastModified(newFile), 0});
                        count++;
                    }
                    if (media.Count > 0)
                        database.ExecuteMany("insert or replace into media values (?,?,?,?)", media);
                }
            }
            return count;
        }

        /// <summary>
        /// Temporary implement as the one in java code
        /// TODO: Fix this description after learning the use of this function
        /// </summary>
        /// <param name="reg"></param>
        /// <returns></returns>
        public static int IndexOfFname(Regex reg)
        {
            int fnameIdx = reg == fSoundRegExps ? 2 : reg == fImgRegExpUnQuote ? 2 : 3;
            return fnameIdx;
        }

        /// <summary>
        /// Scan the first dirMod from meta
        /// </summary>
        /// <returns>True if the media db has not been populated yet</returns>
        public bool NeedScan()
        {
            long dirMod = database.QueryScalar<long>("select dirMod from meta");
            if (dirMod == 0)
                return true;
            
            return false;
        }

        //TODO: Delete this attributes
        [SQLite.Net.Attributes.Table("media")]
        private class MediaDB
        {
            [SQLite.Net.Attributes.Column("fname")]
            public string FileName { get; set; }
            [SQLite.Net.Attributes.Column("csum")]
            public string CheckSum { get; set; }
            [SQLite.Net.Attributes.Column("mtime")]
            public long ModifiedTime { get; set; }
            [SQLite.Net.Attributes.Column("dirty")]
            public int Dirty { get; set; }
        }
    }
}
