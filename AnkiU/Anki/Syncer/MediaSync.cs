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

using AnkiU.AnkiCore;
using AnkiU.Models;
using AnkiU.UIUtilities;
using AnkiU.UserControls;
using AnkiU.ViewModels;
using Microsoft.OneDrive.Sdk;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.Xaml.Controls;

namespace AnkiU.Anki.Syncer
{
    class MediaSync
    {
        private const string MEDIA_DB_LOG_NAME = "mediaSyncLog.db";
        private const string SYNC_MEDIA_LABEL = "Syncing media files ({0}/{1})...";
        private const string UPLOADING_MEDIA_LABEL = "Uploading media files ({0}/{1})...";
        private const string MEDIA_COLLUMN_QUERY = "select * from media where fname = ?";

        private FullSync fullSync;
        private StorageFile remoteMediaDBFile;
        private DB mediaSyncLogDb;

        public MediaSync(FullSync fullSync)
        {
            this.fullSync = fullSync;
        }

        public async Task StartSyncMedia()
        {
            try
            {
                await GetLastSyncLog();

                remoteMediaDBFile = await TryGetUnCompressMediaDB();
                if (remoteMediaDBFile == null)
                    return;

                var deckMediaFolders = await fullSync.MainPage.Collection.Media.MapDeckIdToDeckIdFolder();

                using (var remoteMediaDB = new DB(remoteMediaDBFile.Path))
                {
                    string remoteMediaFolderPath = Constant.ANKIROOT_SYNC_FOLDER + "/" + fullSync.MainPage.Collection.Media.MediaFolder.Name + "/";
                    var remoteMeta = remoteMediaDB.GetTable<MetaTable>().First();
                    long localLastMediaSync = fullSync.MainPage.Collection.Media.GetLastUnixTimeSync();

                    if (remoteMeta.LastUnixTimeSync > localLastMediaSync)
                    {
                        await DownLoadAndUploadMediaChanges(remoteMediaDB, deckMediaFolders,
                                                    remoteMediaFolderPath, remoteMeta, localLastMediaSync);
                    }
                    else
                    {
                        await UploadMediaChanges(deckMediaFolders, remoteMediaFolderPath, remoteMeta);
                    }
                }
            }
            finally
            {
                if(mediaSyncLogDb != null)
                {
                    mediaSyncLogDb.Close();
                    mediaSyncLogDb = null;
                }
            }
        }

        private async Task GetLastSyncLog()
        {
            try
            {
                mediaSyncLogDb = new DB(Storage.AppLocalFolder.Path + "/" + MEDIA_DB_LOG_NAME);
                if (!mediaSyncLogDb.HasTable<MediaTable>("media"))                
                    mediaSyncLogDb.CreateTable<MediaTable>();                
            }
            catch //If any error happen then we try create a new file
            {
                if (mediaSyncLogDb != null)
                    mediaSyncLogDb.Close();

                var mediaSyncLogDbFile = await Storage.AppLocalFolder.TryGetItemAsync(MEDIA_DB_LOG_NAME);
                if (mediaSyncLogDbFile != null)                
                    await mediaSyncLogDbFile.DeleteAsync();

                mediaSyncLogDb = new DB(Storage.AppLocalFolder.Path + "/" + MEDIA_DB_LOG_NAME);
                mediaSyncLogDb.CreateTable<MediaTable>();                
            }
        }

        private async Task<StorageFile> TryGetUnCompressMediaDB()
        {
            StorageFile returnFile;
            var remoteMediaDBZipItem = await fullSync.TryGetItemInSyncFolderAsync(Constant.MEDIA_DB_NAME_ZIP);
            if (remoteMediaDBZipItem == null)
            {
                var remoteMediaDBItem = await fullSync.TryGetItemInSyncFolderAsync(Constant.MEDIA_DB_NAME);
                if (remoteMediaDBItem == null)
                {
                    await UploadAllMediaFiles();
                    return null;
                }

                returnFile = await fullSync.CreateTempFileAsync(Constant.MEDIA_DB_NAME);
                await fullSync.SyncInstance.DownloadItemWithPathAsync(Constant.MEDIA_DB_SYNC_PATH, returnFile);
            }
            else
            {
                var remoteMediaDBZip = await fullSync.CreateTempFileAsync(Constant.MEDIA_DB_NAME_ZIP);
                await fullSync.SyncInstance.DownloadItemWithPathAsync(Constant.ANKIROOT_SYNC_FOLDER + "/" + Constant.MEDIA_DB_NAME_ZIP,
                                                             remoteMediaDBZip);
                using (var fileStream = await remoteMediaDBZip.OpenStreamForReadAsync())
                using (var zip = new ZipArchive(fileStream, ZipArchiveMode.Read))
                {
                    zip.ExtractToDirectory(fullSync.TempSyncFolder.Path);
                }
                returnFile = await fullSync.TempSyncFolder.TryGetItemAsync(Constant.MEDIA_DB_NAME) as StorageFile;
                if (returnFile == null)
                {
                    bool isUploadAll = await UIHelper.AskUserConfirmation("Media database in server is corrupted."
                                                                         + " Do you want to re-upload all media files?");
                    if (isUploadAll)
                        await UploadAllMediaFiles();

                    return null;
                }
            }

            return returnFile;
        }

        private async Task DownLoadAndUploadMediaChanges(DB remoteMediaDB, Dictionary<long, StorageFolder> deckMediaFolders, string remoteMediaFolderPath, MetaTable remoteMeta, long localLastMediaSync)
        {
            var remoteMediasModified = remoteMediaDB.QueryColumn<MediaTable>
                                ("Select * from media where mtime > ?", localLastMediaSync);

            var localMediasModified = fullSync.MainPage.Collection.Media.Database.QueryColumn<MediaTable>
                                     ("Select * from media where mtime > ?", localLastMediaSync);
            ResolveConflictIfHas(remoteMediasModified, localMediasModified);

            long total = remoteMediasModified.Count + localMediasModified.Count;
            var outOfSyncRemoteFiles = await UpdateMediaFilesInLocal(deckMediaFolders, remoteMediaFolderPath, remoteMediasModified, 0, total);
            await DeleteUnusedMediaDeckFolder(deckMediaFolders);

            var outOfSyncLocalFiles = await UpdateMediaFilesInRemoteSever(deckMediaFolders, remoteMediaFolderPath,
                                                                     localMediasModified, total, remoteMediasModified.Count);
            UpdateDownloadedRemoteMediaDB(remoteMediaDB, localMediasModified, outOfSyncLocalFiles, outOfSyncRemoteFiles);

            await UpdateLocalMediaDatabase();

            if (localMediasModified.Count > 0 || outOfSyncRemoteFiles.Count > 0)
                await UpdateSeverMediaDatabase();

            await DeleteMediaSyncLogFile();
        }

        private static void UpdateDownloadedRemoteMediaDB(DB remoteMediaDB, List<MediaTable> localMediasModified,
                                                        List<MediaTable> outOfSyncLocalFiles, List<MediaTable> outOfSyncRemoteFiles)
        {
            foreach (var file in outOfSyncLocalFiles)
            {
                localMediasModified.Remove(file);
            }

            foreach (var media in localMediasModified)
            {
                remoteMediaDB.InsertOrReplace(media);
            }

            foreach (var file in outOfSyncRemoteFiles)
            {
                remoteMediaDB.Delete<MediaTable>(file.RelativePathName);
            }
        }

        private static void ResolveConflictIfHas(List<MediaTable> remoteMediasModified, List<MediaTable> localMediasModified)
        {
            for (int i = 0; i < localMediasModified.Count; i++)
            {
                var conflictIndex = remoteMediasModified.FindIndex(
                                    (x) =>
                                    {
                                        return x.RelativePathName == localMediasModified[i].RelativePathName;
                                    });
                if (conflictIndex == -1)
                    continue;

                if (localMediasModified[i].IsAdded == remoteMediasModified[conflictIndex].IsAdded)
                {
                    remoteMediasModified.RemoveAt(conflictIndex);
                    localMediasModified.RemoveAt(i);
                    i--;
                }
                else
                {
                    localMediasModified.RemoveAt(i);
                    i--;
                }

            }
        }

        private async Task<List<MediaTable>> UpdateMediaFilesInLocal(Dictionary<long, StorageFolder> deckMediaFolders, string remoteMediaFolderPath, List<MediaTable> remoteMediasModified, long count, long total)
        {
            List<MediaTable> outOfSync = new List<MediaTable>();
            foreach (var media in remoteMediasModified)
            {
                fullSync.UpdateProgessDialog(SYNC_MEDIA_LABEL, count, total);
                count++;
                if (IsAlreadySynced(media))
                    continue;

                var splitString = media.RelativePathName.Split(new char[] { Media.DECK_NAME_SEPARATOR }, 2);
                var deckId = long.Parse(splitString[0]);
                if (!deckMediaFolders.ContainsKey(deckId))
                {// If no deckIdFolder exists -> Deck in collection is removed and user did not sync media properly
                    outOfSync.Add(media);
                    continue;
                }

                var name = splitString[1];
                if (media.IsAdded)
                {
                    await DownloadMediaFilesFromSever(deckMediaFolders, remoteMediaFolderPath, media, deckId, name);                    
                }
                else
                {
                    await RemoveMediaFilesInLocal(deckMediaFolders, media, deckId, name);
                }
                UpdateMediaSyncLog(media);
            }

            return outOfSync;
        }

        private async Task DownloadMediaFilesFromSever(Dictionary<long, StorageFolder> deckMediaFolders, string remoteMediaFolderPath, MediaTable media, long deckId, string name)
        {
            try
            {
                var oldFile = await deckMediaFolders[deckId].TryGetItemAsync(name) as StorageFile;
                if (oldFile != null)
                    await oldFile.DeleteAsync();

                var newFile = await deckMediaFolders[deckId].CreateFileAsync(name,
                                                    CreationCollisionOption.ReplaceExisting);
                var remoteFilePath = remoteMediaFolderPath + media.RelativePathName;
                await fullSync.SyncInstance.DownloadItemWithPathAsync(remoteFilePath, newFile);
            }
            catch //Syncing shouldn't stop if some files are not available
            { }
        }
        private async Task RemoveMediaFilesInLocal(Dictionary<long, StorageFolder> deckMediaFolders, MediaTable media, long deckId, string name)
        {
            if (deckMediaFolders.ContainsKey(deckId))
            {
                var file = await deckMediaFolders[deckId].TryGetItemAsync(name) as StorageFile;
                if (file != null)
                    await file.DeleteAsync();
            }
        }
        private async Task DeleteUnusedMediaDeckFolder(Dictionary<long, StorageFolder> deckMediaFolders)
        {
            var allFoldersInMedia = await fullSync.MainPage.Collection.Media.MediaFolder.GetFoldersAsync();
            foreach (var folder in allFoldersInMedia)
            {
                var key = long.Parse(folder.Name);
                if (!deckMediaFolders.ContainsKey(key))
                    await folder.DeleteAsync();
            }
        }
        private async Task UpdateLocalMediaDatabase()
        {
            fullSync.MainPage.Collection.Media.Database.Close();
            await remoteMediaDBFile.CopyAsync(Storage.AppLocalFolder, Constant.MEDIA_DB_NAME, NameCollisionOption.ReplaceExisting);
            await fullSync.MainPage.Collection.Media.ConnectDatabaseAsync();
        }

        private async Task UploadMediaChanges(Dictionary<long, StorageFolder> deckMediaFolders, string remoteMediaFolderPath, MetaTable remoteMeta)
        {
            if (!fullSync.MainPage.Collection.Media.IsDatabaseModified())
                return;

            var mediasModified = fullSync.MainPage.Collection.Media.Database.QueryColumn<MediaTable>
                                                     ("Select * from media where mtime > ?", remoteMeta.LastUnixTimeSync);
            long total = mediasModified.Count;

            await UpdateMediaFilesInRemoteSever(deckMediaFolders, remoteMediaFolderPath, mediasModified, total, 0);

            await DeleteUnusedFolderInServer(deckMediaFolders, remoteMediaFolderPath);
            await UpdateSeverMediaDatabase();

            await DeleteMediaSyncLogFile();
        }

        private async Task<List<MediaTable>> UpdateMediaFilesInRemoteSever(Dictionary<long, StorageFolder> deckMediaFolders, string remoteMediaFolderPath, List<MediaTable> mediasModified, long total, long count)
        {
            List<MediaTable> outOfSyncFiles = new List<MediaTable>();
            foreach (var media in mediasModified)
            {
                fullSync.UpdateProgessDialog(SYNC_MEDIA_LABEL, count, total);
                count++;
                if (IsAlreadySynced(media))
                    continue;

                var splitString = media.RelativePathName.Split(new char[] { Media.DECK_NAME_SEPARATOR }, 2);

                var deckId = long.Parse(splitString[0]);
                if (!deckMediaFolders.ContainsKey(deckId))
                {
                    outOfSyncFiles.Add(media);
                    continue;
                }

                var name = splitString[1];
                string remoteFilePath = remoteMediaFolderPath + media.RelativePathName;
                if (media.IsAdded)
                {
                    var localFile = await deckMediaFolders[deckId].TryGetItemAsync(name) as StorageFile;
                    if (localFile == null)
                    {
                        outOfSyncFiles.Add(media);
                        continue;
                    }
                    
                    await fullSync.SyncInstance.UploadItemWithPathAsync(localFile, remoteFilePath);
                    UpdateMediaSyncLog(media);
                }
                else
                {
                    await DeleteRemoteMediaFile(remoteFilePath);
                    UpdateMediaSyncLog(media);
                }
            }
            return outOfSyncFiles;
        }

        private async Task DeleteRemoteMediaFile(string remoteFilePath)
        {
            try
            {
                await fullSync.SyncInstance.DeleteItemWithPathAsync(remoteFilePath);
            }
            catch { }
        }
        private async Task DeleteUnusedFolderInServer(Dictionary<long, StorageFolder> deckMediaFolders, string remoteMediaFolderPath)
        {
            var remoteDeckMediaFolders = await fullSync.SyncInstance.GetChildrenInRemoteFolder
                                         (remoteMediaFolderPath.Substring(0, remoteMediaFolderPath.Length - 1));

            foreach (var folder in remoteDeckMediaFolders)
            {
                var key = long.Parse(folder.Name);
                if (!deckMediaFolders.ContainsKey(key))
                    await fullSync.SyncInstance.DeleteItemWithPathAsync(remoteMediaFolderPath + folder.Name);
            }
        }

        private async Task UploadAllMediaFiles()
        {
            var folders = await fullSync.MainPage.Collection.Media.MediaFolder.GetFoldersAsync();
            long count = 0;
            long total = fullSync.MainPage.Collection.Media.MediaCount();
            foreach (var folder in folders)
            {
                string remoteDeckFolderPath = Constant.ANKIROOT_SYNC_FOLDER + "/"
                                              + fullSync.MainPage.Collection.Media.MediaFolder.Name + "/"
                                              + folder.Name;
                var files = await folder.GetFilesAsync();
                foreach (var file in files)
                {
                    List<MediaTable> mediaList = GetMediaRowInLocalDB(folder, file);
                    if (mediaList.Count == 0)
                        continue;

                    fullSync.UpdateProgessDialog(UPLOADING_MEDIA_LABEL, count, total);
                    string absolutePath = remoteDeckFolderPath + "/" + file.Name;
                    if (!IsAlreadySynced(mediaList[0]))
                    {
                        await fullSync.SyncInstance.UploadItemWithPathAsync(file, absolutePath);
                        UpdateMediaSyncLog(mediaList[0]);
                    }

                    count++;
                }
            }

            await UpdateSeverMediaDatabase();
            await DeleteMediaSyncLogFile();
        }

        private async Task DeleteMediaSyncLogFile()
        {
            if (mediaSyncLogDb != null)
            {
                mediaSyncLogDb.Close();
                mediaSyncLogDb = null;
            }

            var file = await Storage.AppLocalFolder.TryGetItemAsync(MEDIA_DB_LOG_NAME);
            if (file != null)
                await file.DeleteAsync();
        }

        private List<MediaTable> GetMediaRowInLocalDB(StorageFolder folder, StorageFile file)
        {
            var fileNameInDb = folder.Name + "/" + file.Name;
            var mediaList = fullSync.MainPage.Collection.Media.Database.QueryFirstRow<MediaTable>
                            (MEDIA_COLLUMN_QUERY, fileNameInDb);
            return mediaList;
        }

        private bool IsAlreadySynced(MediaTable media)
        {
            var mediaList = mediaSyncLogDb.QueryFirstRow<MediaTable>(MEDIA_COLLUMN_QUERY, media.RelativePathName);
            if (mediaList.Count == 0)
                return false;
            
            if (mediaList[0].IsAdded == media.IsAdded && mediaList[0].ModifiedTime == media.ModifiedTime)
                return true;
            else
                return false;            
        }
        
        private void UpdateMediaSyncLog(MediaTable media)
        {
            var task = Task.Run(() =>
            {
                UpdateMediaSyncLogDatabase(media);
            });            
        }

        private static readonly object threadLock = new object();
        private void UpdateMediaSyncLogDatabase(MediaTable media)
        {
            lock (threadLock)
            {
                media.Dirty = 0;
                mediaSyncLogDb.InsertOrReplace(media);
            }
        }

        private async Task UpdateSeverMediaDatabase()
        {
            fullSync.MainPage.Collection.Media.SetLastUnixTimeSync(DateTimeOffset.Now.ToUnixTimeSeconds());
            fullSync.MainPage.Collection.Media.MarkDatabaseClean();
            var localFile = await Storage.AppLocalFolder.GetFileAsync(Constant.MEDIA_DB_NAME);
            var mediaDatabase = await localFile.CopyAsync(fullSync.TempSyncFolder, localFile.Name + "_upload");

            var zipFile = await fullSync.TempSyncFolder.CreateFileAsync(Constant.MEDIA_DB_NAME_ZIP + "_upload");
            using (var fileStream = await zipFile.OpenStreamForWriteAsync())
            using (var zip = new ZipArchive(fileStream, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(mediaDatabase.Path, Constant.MEDIA_DB_NAME);
            }
            await fullSync.SyncInstance.UploadItemWithPathAsync(zipFile, Constant.ANKIROOT_SYNC_FOLDER + "/" + Constant.MEDIA_DB_NAME_ZIP);
        }
    }
}
