/*
Copyright (C) 2016 Anki Universal Team <ankiuniversal@outlook.com>

This program is free software: you can redistribute it and/or modify
it under the terms of the GNU Affero General Public License as
published by the Free Software Foundation, either version 3 of the
License, or (at your option) any later version.

This program is distributed in the hope that it will be useful,
but WITHOUT ANY WARRANTY; without even the implied warranty of
MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
GNU Affero General Public License for more details.

You should have received a copy of the GNU Affero General Public License
along with this program.  If not, see <http://www.gnu.org/licenses/>.
*/

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
using AnkiU.AnkiCore.Templates;

namespace AnkiU.AnkiCore
{

    public class Media : IDisposable
    {
        public static readonly string[] ALLOWED_EXTENSION = { ".flac", ".mp3", ".wav", ".wma",
                                                              ".mkv", ".mp4", ".avi",
                                                              ".jpg", ".jpeg", ".png", ".bmp", ".gif"};

        public const string IMAGE_HTML = "<img src='{0}'>";
        public const string SOUND_HTML = "[sound:{0}]";
        public const char DECK_NAME_SEPARATOR = '/';
        private const int MAX_PATH_LENGTH = 250;
        private readonly static Regex illegalCharRegex;
        private readonly static Regex remoteRegex;
     
        //Group 1 = Contents of [sound:] tag <br>
        //Group 2 = "fname"
        private readonly static Regex soundRegExps;

        
        //Group 1 = Contents of<img> tag<br>
        //Group 2 = "str" <br>
        //Group 3 = "fname" <br>
        //Group 4 = Backreference to "str" (i.e., same type of quote character)
        private readonly static Regex imgRegExpQuote;

        //Group 1 = Contents of <img> tag <br>
        //Group 2 = "fname"
        private readonly static Regex imgRegExpUnQuote;

        public readonly static Regex mediaClozeRegex = new Regex(@"{{c(\d+)::.+?}}", RegexOptions.Compiled);

        private Collection collection;
        private string mediaFolderName;
        private string mediaDatabaseName;
        private StorageFolder mediaFolder;
        private StorageFolder appFolder;
        private DB database;

        public StorageFolder MediaFolder { get { return mediaFolder; } }
        public DB Database { get { return database; } }
        public string MediaDatabaseName { get { return mediaDatabaseName; } }
        public static System.Globalization.CultureInfo locale = new System.Globalization.CultureInfo("en-US");

        public static List<Regex> imgRegExps = new List<Regex>();
        public static List<Regex> RegExps = new List<Regex>();

        public long GetLastUnixTimeSync()
        {
             return database.QueryScalar<long>("select lastUsn from meta");
        }

        public void SetLastUnixTimeSync(long value)
        {
            database.Execute("update meta set lastUsn = ?", value);
        }

        public bool IsDatabaseModified()
        {
            int dirMod = database.QueryScalar<int>("select dirMod from meta");
            return dirMod == 1;
        }

        public void SetDatabaseModified()
        {
            database.Execute("update meta set dirMod = ?", 1);
        }

        public void MarkDatabaseClean()
        {
            database.Execute("update meta set dirMod = ?", 0);
        }

        static Media()
        {
            illegalCharRegex = new Regex( @"[][><:"" /? *^\\|\0\r\n]", RegexOptions.Compiled);
            remoteRegex = new Regex("(https?|ftp)://", RegexOptions.Compiled);
            soundRegExps = new Regex(@"(?i)(\[sound:(?<fname>[^]]+)\])", RegexOptions.Compiled);
            imgRegExpQuote = new Regex(@"(?i)(\<img[^>]* src=(?<str>[\""'])(?<fname>[^>]+?)(\k<str>)[^>]*\>)", RegexOptions.Compiled);
            imgRegExpUnQuote = new Regex(@"(?i)(\<img[^>]* src=(?!['\""])(?<fname>[^ >]+)[^>]*?\>)", RegexOptions.Compiled);

            imgRegExps.Add(imgRegExpQuote);
            imgRegExps.Add(imgRegExpUnQuote);

            RegExps.Add(soundRegExps);
            RegExps.Add(imgRegExpQuote);
            RegExps.Add(imgRegExpUnQuote);
        }

        public Media(Collection colection, bool server, StorageFolder folder)
        {
            this.appFolder = folder;
            this.collection = colection;
            if (server)
            {
                mediaFolderName = null;
                return;
            }

            mediaFolderName = colection.RelativePath.ReplaceFirst(".anki2", ".media");            
            CreateOrOpenMediaFolderAsync();
            ConnectDatabaseInNewThread();
        }

        public async void CreateOrOpenMediaFolderAsync()
        {
            mediaFolder = await appFolder.TryGetItemAsync(mediaFolderName) as StorageFolder;
            if(mediaFolder == null)
                mediaFolder = await appFolder.CreateFolderAsync(mediaFolderName, CreationCollisionOption.OpenIfExists);
        }

        public async void ConnectDatabaseInNewThread()
        {
            await ConnectDatabaseAsync();
        }

        public async Task ConnectDatabaseAsync()
        {
            StorageFile store = null;
            try
            {
                if (collection.IsServer)
                    return;

                mediaDatabaseName = Constant.MEDIA_DB_NAME;

                bool create = false;
                store = (await appFolder.TryGetItemAsync(mediaDatabaseName)) as StorageFile;
                if (store == null)
                    create = true;

                database = new DB(appFolder.Path + "\\" + mediaDatabaseName);
                if (!database.HasTable<MediaTable>("media"))
                    create = true;

                if (create)
                    InitMediaDatabase();
            }
            catch //Media DB is corrupted -> create new DB
            {
                if (store != null)
                    await store.DeleteAsync();

                database = new DB(appFolder.Path + "\\" + mediaDatabaseName);
                InitMediaDatabase();
            }
        }

        public void InitMediaDatabase()
        {
            database.CreateTable<MediaTable>();
            string sql = "create index idx_media_dirty on media (dirty)";
            database.ExecuteScript(sql);

            database.CreateTable<MetaTable>();
            database.Insert(new MetaTable() { DirtyModified = 0, LastUnixTimeSync = 0 });            
        }

        [Obsolete]
        public async void MaybeUpgrade()
        {
            string oldpath = mediaFolderName + ".db";
            StorageFile oldFile = (await appFolder.TryGetItemAsync(oldpath)) as StorageFile;
            if (oldFile != null)
            {
                database.Execute(String.Format(Media.locale, "attach \"{0}\" as old", oldpath));
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
                    collection.Log(args: "failed to import old media db:" + e.StackTrace.ToString());
                }

                database.Execute("detach old");
                StorageFile newDbFile = (await appFolder.TryGetItemAsync(oldpath + ".old")) as StorageFile;
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

        /// <summary>
        /// Add a file into media folder and mark it into database
        /// </summary>
        /// <param name="file"></param>
        /// <returns>Relative file name</returns>
        public async Task<string> AddFile(StorageFile file, long deckId = 0)
        {
            StorageFolder folder = null;
            if (deckId != 0)
            {
                folder = await GetDeckMediaFolder(deckId);
            }

            StorageFile sFile = await WriteData(file, folder);
            MarkFileAddIntoDatabase(sFile.Name, deckId);
            return sFile.Name;
        }

        public async Task<StorageFolder> GetDeckMediaFolder(long deckId)
        {
            var folder = await MediaFolder.TryGetItemAsync(deckId.ToString()) as StorageFolder;
            if (folder == null)
                folder = await MediaFolder.CreateFolderAsync(deckId.ToString());
            return folder;
        }

        private async Task<StorageFile> WriteData(StorageFile sourceFile, StorageFolder deckIdFolder)
        {
            StorageFolder mediaFolder;
            if (deckIdFolder != null)
                mediaFolder = deckIdFolder;
            else
                mediaFolder = this.mediaFolder;

            string fileName = sourceFile.Name;
            Debug.WriteLine(sourceFile.Path);
            string normalize = fileName.Normalize(NormalizationForm.FormC);

            string legalName = StripIllegal(normalize);
            string[] split = Utils.SplitNameAndExtension(legalName);
            string root = split[0];
            string ext = split[1];

            //WARNING: In python ver content of two files are compared and return if they are alike
            //However checksum is an expensive operation for large files so AnkiU does not check the whole file
            //Therefore we will only rename without checking content
            //string checkSum = await Utils.FileChecksum(sourceFile);
            try
            {
                var pathLength = (mediaFolder.Path + legalName).Length;
                if (pathLength > MAX_PATH_LENGTH)                
                    throw new Exception("File name is too long! Please rename the file first.");
                
                StorageFile newFile;
                while (true)
                {                                     
                    newFile = (await mediaFolder.TryGetItemAsync(legalName)) as StorageFile;

                    if (newFile == null)
                    {
                        newFile = await sourceFile.CopyAsync(mediaFolder, legalName);
                        return newFile;
                    }

                    //string newCheckSum = await Utils.FileChecksum(newFile);
                    //if (newCheckSum.Equals(checkSum, StringComparison.OrdinalIgnoreCase))
                    //{
                    //    return newFile;
                    //}

                    Regex reg = new Regex(@" \((\d+)\)$", RegexOptions.Compiled);
                    MatchCollection matches = reg.Matches(root);
                    if (matches.Count > 0)
                    {
                        Match match = matches[0];
                        int n = int.Parse(match.GetGroup(1));
                        n++;
                        root = reg.Replace(root, " (" + n + ")");
                        Debug.WriteLine(root);
                    }
                    else
                        root = root + " (1)";

                    legalName = root + ext;
                }
            }
            catch(Exception e)
            {
                throw new Exception("Media.WriteData Failed: " + e.Message, e);
            }
        }

        public void MarkFileAddIntoDatabase(string fileName, long deckId, long? modifiedTime = null)
        {
            var mediaInfo = new MediaTable();

            if (modifiedTime == null)
                mediaInfo.ModifiedTime = DateTimeOffset.Now.ToUnixTimeSeconds();
            else
                mediaInfo.ModifiedTime = (long)modifiedTime;

            mediaInfo.Dirty = 1;
            mediaInfo.DeckId = deckId;
            mediaInfo.IsAdded = true;
            mediaInfo.RelativePathName = deckId.ToString() + DECK_NAME_SEPARATOR + fileName;            

            database.InsertOrReplace(mediaInfo, typeof(MediaTable));
            SetDatabaseModified();
        }

        [Obsolete]
        public async Task MarkFileAdd(StorageFile file)
        {
            var mediaInfo = new MediaTable();

            mediaInfo.CheckSum = await Checksum(file);
            mediaInfo.ModifiedTime = await GetTimeLastModified(file);
            mediaInfo.Dirty = 1;
            mediaInfo.IsAdded = true;
            mediaInfo.RelativePathName = file.Name;

            database.InsertOrReplace(mediaInfo, typeof(MediaTable));
        }

        /// <summary>
        /// Return the checksum of a file
        /// </summary>
        /// <param name="fileName">Relative path</param>
        /// <param name="sFolder">Parent folder</param>
        /// <returns></returns>
        private async Task<string> Checksum(string fileName, StorageFolder sFolder = null)
        {
            StorageFile file;
            if (sFolder == null)
                file = await appFolder.GetFileAsync(fileName);
            else
                file = await sFolder.GetFileAsync(fileName);

            return await Utils.FileChecksum(file);
        }

        private async Task<string> Checksum(StorageFile file)
        {
            return await Utils.FileChecksum(file);
        }

        public string StripIllegal(string str)
        {
            return illegalCharRegex.Replace(str, "");
        }

        public bool HasIllegal(string str)
        {
            return illegalCharRegex.IsMatch(str);
        }

        public List<string> FileNameInMediaFolder(long mid, string strIn, bool includeRemote = false)
        {
            List<string> returnList = new List<string>();
            List<string> stringList = new List<string>();
            JsonObject model = collection.Models.Get(mid);

            int type = (int)JsonHelper.GetNameNumber(model,"type");
            if ((type == (int)ModelType.CLOZE) && strIn.Contains("{{c"))
                stringList = ExpandClozes(strIn);
            else
                stringList.Add(strIn);

            string str;
            foreach (string s in stringList)
            {
                str = LaTeX.MungeQA(s, collection);
                MatchCollection matches;
                foreach (Regex p in RegExps)
                {
                    matches = p.Matches(str);
                    foreach (Match m in matches)
                    {
                        string fName = m.Groups["fname"].Value;
                        bool isLocal = !remoteRegex.IsMatch(fName.ToLowerInvariant());
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
            string clozeReg = Template.clozeReg;
            
            MatchCollection matches = mediaClozeRegex.Matches(str);
            if (matches.Count > 0)
                foreach (Match m in matches)
                    ords.Add(m.GetGroup(1));

            string replacePattern = String.Format(locale, clozeReg, ".+?");
            foreach (string ord in ords)
            {
                StringBuilder sBuild = new StringBuilder();
                string pattern = String.Format(locale, clozeReg, ord);
                matches = (new Regex(pattern)).Matches(str);
                foreach (Match m in matches)
                {
                    if (String.IsNullOrEmpty(m.GetGroup(3)))
                    {
                        if (!sBuild.AppendAndReplace("[$3]", str, m))
                            break;
                    }
                    else
                    {
                        if (!sBuild.AppendAndReplace("[...]", str, m))
                            break;
                    }
                }
                string s;
                if (sBuild.Length != 0)
                    s = sBuild.ToString().Replace(replacePattern, "$1");
                else
                    s = str;
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
            foreach (Regex pattern in imgRegExps)
            {
                MatchCollection matches = pattern.Matches(str);
                foreach (Match m in matches)
                {
                    string tag = m.GetGroup(0);
                    string fName = m.Groups["fname"].Value;
                    if (!remoteRegex.IsMatch(fName))
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

        //WARNING: we used struct instead of List<List<string>
        //as in java and python ver to enforce type-safe and provide a clearer
        //meaning of return values
        public struct CheckResults
        {
            public List<KeyValuePair<string, long>> UnusedFiles { get; set; }
            public List<KeyValuePair<string, long>> MisingFiles { get; set; }
        }
        /// <summary>
        /// Return missing files and unused files
        /// </summary>
        /// <param name="local"></param>
        /// <returns></returns>
        public async Task<CheckResults> CheckMissingAndUnusedFiles(Dictionary<StorageFile, long> local = null)
        {
            //WARNING: This function is totally diffent with python and java ver
            //because we don't store all media files in one folder but in deckIdFolder          
            var listNote = collection.Database.QueryColumn<NoteTable>(
                           "select distinct f.id, f.mid, f.flds from notes f, cards c where c.nid = f.id");
            var listCard = collection.Database.QueryColumn<CardTable>(
               "select distinct c.did, c.nid from cards c, notes f where c.nid = f.id");

            var noteToDeckId = MapNoteToDeckId(listNote, listCard);
            var mediaToDeckId = MapMediaNameToDeckId(noteToDeckId);

            var unUsed = new List<KeyValuePair<string, long>>();
            //Warning: Invalid is kept in java ver for compatible with the source code in python
            //Since window and C# is unicode base, we omit this instead
            //List<string> inValid = new List<string>();
            Dictionary<StorageFile, long> filesToDeckId;
            if (local != null)
                filesToDeckId = new Dictionary<StorageFile, long>(local);
            else
            {
                filesToDeckId = new Dictionary<StorageFile, long>();
                foreach (var folder in await mediaFolder.GetFoldersAsync())                             
                    foreach(var f in await folder.GetFilesAsync())
                        filesToDeckId[f] =  Convert.ToInt64(folder.Name);
            }

            bool isRenamedFiles = false;
            foreach(var file in filesToDeckId)
            {
                if (file.Key.Name.StartsWith("_")) { 
                    // leading _ says to ignore file
                    continue;
                }

                var key = new KeyValuePair<string, long>(file.Key.Name, file.Value);
                if (!mediaToDeckId.Keys.Contains(key))
                    unUsed.Add(key);
                else
                    mediaToDeckId.Remove(key);
            }

            // if we renamed any files to nfc format, we must rerun the check
            // to make sure the renamed files are not marked as unused
            if (isRenamedFiles)
                return await CheckMissingAndUnusedFiles(local);

            var noHave = new List<KeyValuePair<string, long>>();
            foreach (var x in mediaToDeckId.Keys)
                if (!x.Key.StartsWith("_"))
                    noHave.Add( new KeyValuePair<string, long>(x.Key, x.Value));

            CheckResults result = new CheckResults();
            result.MisingFiles = noHave;
            result.UnusedFiles = unUsed;            
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
            note.SaveChangesToDatabase();
        }

        public async Task<bool> HaveInMainFolder(string fName)
        {
            StorageFile f = await mediaFolder.TryGetItemAsync(fName) as StorageFile;
            if (f == null)
                return false;
            return true;
        }

        /// <summary>
        /// Scan for changes in media folder and note any changes.
        /// </summary>
        /// <param name="force">Unconditionally scan the media folder for changes 
        /// (i.e., ignore differences in recorded and current
        /// directory mod times). 
        /// Use this when rebuilding the media database.</param>
        /// <returns></returns>
        [Obsolete]
        public async Task ScanForChangesAsync(bool force = false)
        {
            if (force || (await Changed() != null))
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
            var modified = (await file.GetBasicPropertiesAsync())
                       .DateModified.ToUnixTimeSeconds();
            return modified;
        }

        /// <summary>
        /// Get the last modified time in seconds according to Unix time
        /// </summary>
        /// <param name="sFolder"></param>
        /// <returns></returns>
        private async Task<long> GetTimeLastModified(StorageFolder sFolder)
        {
            var modified = (await sFolder.GetBasicPropertiesAsync())
                            .DateModified.ToUnixTimeSeconds();
            return modified;
        }

        /// <summary>
        /// Get the last modified time in seconds according to Unix time
        /// </summary>
        /// <param name="file"></param>
        /// <returns></returns>
        private async Task<long> GetTimeLastModified(StorageFile file)
        {
            var modified = (await file.GetBasicPropertiesAsync())
                                   .DateModified.ToUnixTimeSeconds();
            return modified;
        }

        [Obsolete]
        private async Task LogChanges()
        {
            List<List<string>> result = await Changes();
            List<string> added = result[0];
            List<string> removed = result[1];
            List<object[]> media = new List<object[]>();
            foreach(string f in added)
            {
                long modifiedTime = await GetTimeLastModified(f, mediaFolder);
                media.Add(new object[] { f, await Checksum(f, mediaFolder), modifiedTime, 1 });
            }
            foreach (string f in removed)
                media.Add(new object[] { f, null, 0, 1 });

            database.ExecuteMany("insert or replace into media values (?,?,?,?)", media);
            database.Execute("update meta set dirMod = ?", new object[] { await GetTimeLastModified(mediaFolder)});
        }

        [Obsolete]
        private async Task<List<List<string>>> Changes()
        {
            Dictionary<string, object[]> cache = new Dictionary<string, object[]>();
            var array = (from s in database.QueryColumn<MediaTable>("select fname, csum, mtime from media where csum is not null")
                        select new { s.RelativePathName, s.CheckSum, s.ModifiedTime}
                        ).ToArray();
            for(int i = 0; i < array.Length; i++)
                cache.Add(array[i].RelativePathName, new object[] { array[i].CheckSum, array[i].ModifiedTime, false });

            List<string> added = new List<string>();
            List<string> removed = new List<string>();

            var items = await mediaFolder.GetItemsAsync();

            foreach (var item in items)
            {
                var file = item as StorageFile;
                if (file == null)
                    continue;

                if (file.Name.Equals("thumbs.db", StringComparison.OrdinalIgnoreCase))
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
                    collection.Log(args: new object[] { "ignoring file over 100MB", file });
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
                        if (! (await Checksum(file.Name, mediaFolder))
                              .Equals(cache[file.Name][0]))
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

        [Obsolete]
        public KeyValuePair<string, int> GetSyncInfo(string fName)
        {
            var array = (from s in database.QueryFirstRow<MediaTable>("select csum, dirty from media where fname=?", new string[] { fName })
                         select new { s.CheckSum, s.Dirty }).ToArray();
            if (array[0] == null)
                return new KeyValuePair<string, int>(null, 0);
            else
                return new KeyValuePair<string, int>(array[0].CheckSum, array[0].Dirty);
        }

        [Obsolete]
        public void MarkClean(List<string> fNames)
        {
            foreach (string fName in fNames)
                database.Execute("update media set dirty=0 where fname=?", new object[] { fName });
        }
      
        [Obsolete]
        public async Task SyncDelete(string mediaNameInDatabase)
        {
            var splitString = mediaNameInDatabase.Split(new char[] { DECK_NAME_SEPARATOR }, 2);
            if (splitString.Length != 2)
                return;

            var deckId = splitString[0];            
            var deckFolder = await mediaFolder.TryGetItemAsync(deckId) as StorageFolder;
            if (deckFolder == null)
                return;

            var fileName = splitString[1];
            StorageFile file = await deckFolder.TryGetItemAsync(fileName) as StorageFile;
            if (file != null)
                await file.DeleteAsync();

            //TODO: See if we need to change this position
            database.Execute("delete from media where fname=?", fileName);
        }

        public int MediaCount()
        {
            return database.QueryScalar<int>("select count() from media where isadded is not 0");
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

        [Obsolete]
        public async Task<KeyValuePair<StorageFile, List<string>>> MediaChangesZip()
        {
            string name = collection.RelativePath.ReplaceFirst("collection.anki2", "tmpSyncToServer.zip");
            var compressedFile = await appFolder.CreateFileAsync(name, CreationCollisionOption.ReplaceExisting);

            List<string> fNames = new List<string>();
            JsonArray meta = new JsonArray();
            ulong size = 0;
            var array = (from s in database.QueryColumn<MediaTable>("select fname, csum from media where dirty=1 limit " + Syncing.ZIP_COUNT)
                         select new { s.RelativePathName, s.CheckSum }).ToArray();

            using (FileStream zipToOpen = new FileStream(compressedFile.Path, FileMode.Open))
            {
                using (ZipArchive archive = new ZipArchive(zipToOpen, ZipArchiveMode.Update))
                {
                    for (int i = 0; i < array.Length; i++)
                    {
                        fNames.Add(array[i].RelativePathName);
                        string normName = array[i].RelativePathName.Normalize(NormalizationForm.FormC);

                        if (!String.IsNullOrEmpty(array[i].CheckSum))
                        {
                            collection.Log(args: "+media zip" + array[i].RelativePathName);
                            StorageFile file = await mediaFolder.TryGetItemAsync(array[i].RelativePathName) as StorageFile;
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
                                RemoveFileFromMediaDatabase(array[i].RelativePathName);
                        }
                        else
                        {
                            collection.Log(args: "-media zip " + array[i].RelativePathName);
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

        [Obsolete]
        public void RemoveFileFromMediaDatabase(string fName)
        {
            var mediaInfor = new MediaTable();

            database.Execute("insert or replace into media values (?,?,?,?)",
                new object[] { fName, null, 0, 1 });
        }

        public void MarkFileRemoveIntoDatabase(string fName, long deckId, long? modifiedTime = null)
        {
            var mediaInfor = new MediaTable();
            mediaInfor.RelativePathName = deckId.ToString() + DECK_NAME_SEPARATOR + fName;
            mediaInfor.IsAdded = false;
            mediaInfor.DeckId = deckId;
            mediaInfor.Dirty = 1;
            if (modifiedTime == null)
                mediaInfor.ModifiedTime = DateTimeOffset.Now.ToUnixTimeSeconds();
            else
                mediaInfor.ModifiedTime = (long)modifiedTime;
            database.InsertOrReplace(mediaInfor);
            SetDatabaseModified();
        }

        [Obsolete]
        public async Task<int> AddFilesFromZip(ZipArchive archive)
        {
            List<object[]> media = new List<object[]>();
            JsonObject meta;
            int count = 0;

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
         
            return count;
        }

        /// <summary>
        /// Scan the first dirMod from meta
        /// </summary>
        /// <returns>True if the media db has not been populated yet</returns>
        [Obsolete]
        public bool NeedScan()
        {
            long dirMod = database.QueryScalar<long>("select dirMod from meta");
            if (dirMod == 0)
                return true;
            
            return false;
        }

        public void Dispose()
        {
            Close();
        }

        #region Not in java and python ver        
        public Dictionary<NoteTable, long> MapNoteToDeckId(IEnumerable<NoteTable> arrayNote, IEnumerable<CardTable> arrayCard)
        {
            Dictionary<NoteTable, long> array = new Dictionary<NoteTable, long>();
            foreach (var note in arrayNote)
            {
                //We assume that card and note will always belong to only one deck
                //so only need to retrieve DeckId from one card
                var card = arrayCard.First((x) => { return x.Nid == note.Id; });
                array.Add(note, card.Did);
            }

            return array;
        }

        public Dictionary<KeyValuePair<string, long>, bool> MapMediaNameToDeckId(Dictionary<NoteTable, long> array)
        {
            var mediaNameMapDeckId = new Dictionary<KeyValuePair<string, long>, bool>();
            foreach (var note in array)
            {
                List<string> noteRefs = FileNameInMediaFolder(note.Key.Mid, note.Key.Fields);
                foreach (string f in noteRefs)
                {
                    if (!f.IsNormalized(NormalizationForm.FormC))
                    {
                        NormalizeNoteRefs(note.Key.Id);
                        noteRefs = FileNameInMediaFolder(note.Key.Mid, note.Key.Fields);
                        break;
                    }
                }
                foreach (var s in noteRefs)
                {
                    var key = new KeyValuePair<string, long>(s, note.Value);
                    mediaNameMapDeckId[key] = true;
                }
            }

            return mediaNameMapDeckId;
        }

        public async Task<Dictionary<long, StorageFolder>> MapDeckIdToDeckIdFolder()
        {
            long[] deckIdArray = collection.Deck.AllIds();
            Dictionary<long, StorageFolder> folderList = new Dictionary<long, StorageFolder>();
            foreach (var id in deckIdArray)
            {
                StorageFolder folder = await MediaFolder.TryGetItemAsync(id.ToString()) as StorageFolder;
                if (folder == null)
                    folder = await MediaFolder.CreateFolderAsync(id.ToString());
                folderList.Add(id, folder);
            }

            return folderList;
        }

        public async Task RemoveDeckMediaFolderAsync(long deckId)
        {
            RemoveDeckMediaFromtDatabase(deckId);

            var deckFolder = await mediaFolder.TryGetItemAsync(deckId.ToString()) as StorageFolder;
            if (deckFolder == null)
                return;
                                    
            await deckFolder.DeleteAsync();
        }

        public void RemoveDeckMediaFromtDatabase(long deckId)
        {            
            database.Execute("delete from media where deckid = ?", deckId);
            SetDatabaseModified();
        }

        public async Task DeleteMediaFiles(List<KeyValuePair<string, long>> mediaFiles)
        {
            var folders = await MapDeckIdToDeckIdFolder();
            foreach(var file in mediaFiles)
            {
                StorageFolder folder;
                if (folders.Keys.Contains(file.Value))
                    folder = folders[file.Value];
                else
                    folder = await MediaFolder.TryGetItemAsync(file.Value.ToString()) as StorageFolder;
                if (folder == null)
                    continue;

                var storageFile = await folder.TryGetItemAsync(file.Key) as StorageFile;
                if (storageFile != null)
                {
                    await storageFile.DeleteAsync();
                    MarkFileRemoveIntoDatabase(storageFile.Name, file.Value);
                }
            }            
        }
        #endregion
    }

    /// <summary>
    /// Class used to access the media tables of media
    /// database
    /// </summary>
    [SQLite.Net.Attributes.Table("media")]
    public class MediaTable
    {
        [SQLite.Net.Attributes.PrimaryKey, SQLite.Net.Attributes.Column("fname")]
        public string RelativePathName { get; set; }
        [SQLite.Net.Attributes.Column("isadded")]
        public bool IsAdded { get; set; }  
        [SQLite.Net.Attributes.Column("mtime")]
        public long ModifiedTime { get; set; }
        [SQLite.Net.Attributes.Column("deckid")]
        public long DeckId { get; set; }
        [SQLite.Net.Attributes.Column("dirty")]
        public int Dirty { get; set; }

        #region Currently not used
        [SQLite.Net.Attributes.Column("csum")]
        public string CheckSum { get; set; }
        #endregion
    }

    /// <summary>
    /// Class used to access the meta table of media
    /// database
    /// </summary>
    [SQLite.Net.Attributes.Table("meta")]
    public class MetaTable
    {
        [SQLite.Net.Attributes.Column("dirMod")]
        public int DirtyModified { get; set; }
        [SQLite.Net.Attributes.Column("lastUsn")]
        public long LastUnixTimeSync { get; set; }
    }

}
