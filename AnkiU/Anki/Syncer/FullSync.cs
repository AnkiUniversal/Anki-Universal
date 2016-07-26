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
    public class FullSync : ISync
    {
        private const string UPLOADING_MEDIA_LABEL = "Uploading media files ({0}/{1})...";
        private const string SYNC_MEDIA_LABEL = "Syncing media files ({0}/{1})...";

        private StorageFolder tempSyncFolder = null;
        private StorageFolder deckImageFolder = null;
        private StorageFolder deckImageCacheFolder = null;
        private ISyncInstance syncInstance = null;     
        private GeneralPreference remoteUserPref = null;
        private MainPage mainPage;
        private SyncDialog syncStateDialog;
        private bool isSyncStateDialogClose = false;

        private StorageFile remoteMediaDBFile;                       

        public FullSync(MainPage mainPage, ISyncInstance syncInstance)
        {
            this.mainPage = mainPage;
            syncStateDialog = new SyncDialog(mainPage.CurrentDispatcher);
            syncStateDialog.Opened += SyncStateDialogOpened;
            syncStateDialog.Closed += SyncStateDialogClosed;
            this.syncInstance = syncInstance;            
        }

        private void SyncStateDialogOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            isSyncStateDialogClose = false;
        }

        private void SyncStateDialogClosed(ContentDialog sender, Windows.UI.Xaml.Controls.ContentDialogClosedEventArgs args)
        {
            isSyncStateDialogClose = true;
        }

        public async Task StartSync()
        {                       
            try
            {
                await PreparingFilesAsync();
                var remoteCollectionZipFile = await TryGetItemInSyncFolderAsync(Constant.COLLECTION_NAME_ZIP);
                if (remoteUserPref == null
                    || MainPage.UserPrefs.LastSyncTime >= remoteUserPref.LastSyncTime)
                {
                    if (GetLastModifiedTimeInSecond() > MainPage.UserPrefs.LastSyncTime
                        || remoteCollectionZipFile == null)
                    {
                        await UploadToServer();
                    }
                }
                else
                {
                    bool? isDownload = true;
                    if (GetLastModifiedTimeInSecond() > MainPage.UserPrefs.LastSyncTime)
                    {                        
                        isDownload = await AskForceSyncInOneDirection();
                        if (isDownload == null)
                            return;
                        else if(isDownload == true)
                        {
                            syncStateDialog.Label = "Backing up your database...";
                            await MainPage.BackupDatabase();
                        }
                    }
                    
                    if (isDownload == true)                    
                        await DownloadFromServer();                    
                    else
                        await UploadToServer();
                }

                await SyncMediaIfNeeded();
                syncStateDialog.Label = "Finished.";
                await Task.Delay(500);
            }
            catch (Exception ex)
            {
                await syncInstance.ExceptionHandler(ex);
            }
            finally
            {
                syncInstance.Close();
                if (tempSyncFolder != null)
                    await tempSyncFolder.DeleteAsync();
                syncStateDialog.Close();
            }
        }

        private async Task PreparingFilesAsync()
        {
            syncStateDialog.Show(MainPage.UserPrefs.IsReadNightMode);
            syncStateDialog.Label = "Authenticating...";
            syncInstance.InitInstance();
            await syncInstance.AuthenciateAccount();

            syncStateDialog.Label = "Preparing files...";
            mainPage.Collection.SaveAndCommit();
            await syncInstance.InitSyncFolderIfNeeded();
            await PrepareForSyncing();
        }

        private long GetLastModifiedTimeInSecond()
        {
            return mainPage.Collection.TimeModified / 1000;
        }

        private async Task<bool?> AskForceSyncInOneDirection()
        {
            await CloseSyncStateDialog();

            ThreeOptionsDialog dialog = new ThreeOptionsDialog();
            dialog.Message = "Your current collection has been modified without syncing to the server first." +
                            " As a result, some of your changes will be lost.\n\n"
                            + "Choosing \"Download\" will replace your current collection with the one from the server."
                            + " (A backup will also be created.)\n\n"
                            + "Choosing \"Upload\" will upload your current collection to the server.";
            dialog.Title = "Out of Sync";
            dialog.LeftButton.Content = "Download";
            dialog.MiddleButton.Content = "Upload";
            dialog.RightButton.Content = "Cancel";
            await dialog.ShowAsync();
            await dialog.WaitForDialogClosed();
            syncStateDialog.Show(MainPage.UserPrefs.IsReadNightMode);
            return dialog.ThreeStateChoose;
        }

        private async Task CloseSyncStateDialog()
        {
            syncStateDialog.Close();
            while (!isSyncStateDialogClose)
                await Task.Delay(50);
        }

        private async Task UploadToServer()
        {
            syncStateDialog.Label = "Uploading database...";            
            await UploadCollectionDatabase();
            await UploadPrefDatabase();
            await UploadDeckImages();            
        }

        private async Task UploadPrefDatabase()
        {
            MainPage.UserPrefs.LastSyncTime = DateTimeOffset.Now.ToUnixTimeSeconds();
            mainPage.UpdateUserPreference();
            var userPrefFile = await CopyToTempFolderAsync(await Storage.AppLocalFolder.GetFileAsync(Constant.USER_PREF));
            await syncInstance.UploadItemWithPathAsync(userPrefFile, Constant.USER_PREF_SYNC_PATH);
        }

        private async Task UploadCollectionDatabase()
        {
            StorageFile collectionFile = await CompressedDatabase();
            await syncInstance.UploadItemWithPathAsync(collectionFile, Constant.ANKIROOT_SYNC_FOLDER + "/" + collectionFile.Name);
        }

        private async Task<StorageFile> CompressedDatabase()
        {
            var collectionFile = await CopyToTempFolderAsync(await Storage.AppLocalFolder.GetFileAsync(Constant.COLLECTION_NAME));
            var zipFile = await tempSyncFolder.CreateFileAsync(Constant.COLLECTION_NAME_ZIP);
            using (var fileStream = await zipFile.OpenStreamForWriteAsync())
            using (ZipArchive archive = new ZipArchive(fileStream, ZipArchiveMode.Create))
            {
                archive.CreateEntryFromFile(collectionFile.Path, collectionFile.Name);
            }

            return zipFile;
        }

        private async Task UploadDeckImages()
        {            
            var files = await deckImageFolder.GetFilesAsync();
            var remoteFolder = await TryGetItemInSyncFolderAsync(Constant.DEFAULT_DECK_IMAGE_FOLDER_NAME);
            if (remoteFolder == null)
            {
                await UploadAllDeckImages(files);
            }
            else
            {
                await UploadChangedDeckImages(files);
            }
        }
        private async Task UploadAllDeckImages(IReadOnlyList<StorageFile> files)
        {
            var localCacheItems = await deckImageCacheFolder.GetFilesAsync();

            foreach (var file in files)
            {
                if (file.Name == DeckInformation.DEFAULT_IMAGE_NAME)
                    continue;
                
                var cacheItem = DeckInformation.TryGetCacheItem(localCacheItems, file.Name);
                if (cacheItem == null)
                    continue;

                long localFileDateCreated = long.Parse(cacheItem.Name.Split(DeckInformation.IMAGE_NAME_SEPARATOR)[1]);

                await UploadDeckImageFile(file, localFileDateCreated);
            }
        }
        private async Task UploadChangedDeckImages(IReadOnlyList<StorageFile> files)
        {
            var remoteItems = await syncInstance.GetChildrenInRemoteFolder(Constant.DEFAULT_DECK_IMAGE_FOLDER_SYNC_PATH);
            var localCacheItems = await deckImageCacheFolder.GetFilesAsync();

            foreach (var file in files)
            {
                if (file.Name == DeckInformation.DEFAULT_IMAGE_NAME)
                    continue;

                var cacheItem = DeckInformation.TryGetCacheItem(localCacheItems, file.Name);
                long localFileDateCreated = 0;
                if (cacheItem != null)
                    localFileDateCreated = long.Parse(cacheItem.Name.Split(DeckInformation.IMAGE_NAME_SEPARATOR)[1]);

                bool isHasFile = false;
                foreach (var item in remoteItems)
                {
                    var splitString = item.Name.Split(DeckInformation.IMAGE_NAME_SEPARATOR);
                    if (splitString.Length != 2)
                        continue;

                    string name = splitString[0];
                    long itemDateCreated = long.Parse(splitString[1]);

                    if (name == file.Name)
                    {
                        if (itemDateCreated < localFileDateCreated)
                        {
                            await syncInstance.DeleteItemWithPathAsync(Constant.DEFAULT_DECK_IMAGE_FOLDER_SYNC_PATH + "/" + item.Name);
                            await UploadDeckImageFile(file, localFileDateCreated);
                        }

                        isHasFile = true;
                        remoteItems.Remove(item);
                        break;
                    }
                }

                if (!isHasFile)
                    await UploadDeckImageFile(file, localFileDateCreated);
            }

            await DeleteUnusedDeckImagesOnServer(remoteItems);
        }
        private async Task UploadDeckImageFile(StorageFile file, long localFileDateCreated)
        {
            await syncInstance.UploadItemWithPathAsync(file,
                                   Constant.DEFAULT_DECK_IMAGE_FOLDER_SYNC_PATH + "/" 
                                   + file.Name 
                                   + DeckInformation.IMAGE_NAME_SEPARATOR
                                   + localFileDateCreated);
        }
        private async Task DeleteUnusedDeckImagesOnServer(List<RemoteItem> remoteItems)
        {
            if (remoteItems.Count != 0)
            {
                foreach (var item in remoteItems)
                    await syncInstance.DeleteItemWithPathAsync(Constant.DEFAULT_DECK_IMAGE_FOLDER_SYNC_PATH + "/" + item.Name);                    
            }
        }

        private async Task DownloadFromServer()
        {
            syncStateDialog.Label = "Donwloading database...";                   
            await DownloadCollectionDatabase();
            await DownloadDeckImages();

            UpdatePrefDatabase();
        }

        private void UpdatePrefDatabase()
        {
            MainPage.UserPrefs.LastSyncTime = remoteUserPref.LastSyncTime;
            MainPage.UserPrefs.Helps = remoteUserPref.Helps;
            mainPage.UpdateUserPreference();
            if(mainPage.AllHelps != null)
                mainPage.AllHelps.SetupTutorialsVisibility();
        }

        private async Task DownloadCollectionDatabase()
        {
            StorageFile collectionFile = await GetUnCompressedDBFile();
            if (collectionFile == null)
            {
                await UIHelper.ShowMessageDialog("Couldn't find database file.");
                return;
            }

            mainPage.Collection.Close();
            await collectionFile.CopyAsync(Storage.AppLocalFolder, Constant.COLLECTION_NAME, NameCollisionOption.ReplaceExisting);
            mainPage.Collection = await Storage.OpenOrCreateCollection(Storage.AppLocalFolder, Constant.COLLECTION_NAME);
            await mainPage.NavigateToDeckSelectPage();
            mainPage.ContentFrame.BackStack.RemoveAt(0);
        }

        private async Task<StorageFile> GetUnCompressedDBFile()
        {
            StorageFile collectionFile;
            try
            {
                var compressedRemoteDB = await CreateTempFileAsync(Constant.COLLECTION_NAME_ZIP);
                await syncInstance.DownloadItemWithPathAsync(Constant.ANKIROOT_SYNC_FOLDER + "/" 
                                                           + Constant.COLLECTION_NAME_ZIP, 
                                                             compressedRemoteDB);
                using (var fileStream = await compressedRemoteDB.OpenStreamForReadAsync())
                using (ZipArchive archive = new ZipArchive(fileStream, ZipArchiveMode.Read))
                {
                    archive.ExtractToDirectory(tempSyncFolder.Path);
                }
                collectionFile = await tempSyncFolder.TryGetItemAsync(Constant.COLLECTION_NAME) as StorageFile;
            }
            catch
            {// NO zipFile
                await UIHelper.ShowMessageDialog("Please update all your devices to the newest version and re-sync your data.");
                var oldSyncFile = await syncInstance.TryGetItemInRemotePathAsync(Constant.COLLECTION_NAME, Constant.ANKIROOT_SYNC_FOLDER);
                if (oldSyncFile != null)
                    await syncInstance.DeleteItemWithPathAsync(Constant.ANKI_COL_SYNC_PATH);
                collectionFile = null;
            }                                                        
            return collectionFile;
        }

        private async Task DownloadDeckImages()
        {            
            var files = (await deckImageFolder.GetFilesAsync()).ToList();
            var remoteFolder = await TryGetItemInSyncFolderAsync(Constant.DEFAULT_DECK_IMAGE_FOLDER_NAME);
            if (remoteFolder != null)            
            {
                await DownloadChangedDeckImages(files);
            }
        }
        private async Task DownloadChangedDeckImages(List<StorageFile> files)
        {
            var remoteItems = await syncInstance.GetChildrenInRemoteFolder(Constant.DEFAULT_DECK_IMAGE_FOLDER_SYNC_PATH);            
            var localCacheItems = await deckImageCacheFolder.GetFilesAsync();

            foreach (var item in remoteItems)
            {
                var splitString = item.Name.Split(DeckInformation.IMAGE_NAME_SEPARATOR);
                if (splitString.Length != 2)
                    continue;

                string name = splitString[0];
                long dateCreated = long.Parse(splitString[1]);

                bool isHasFile = false;
                foreach(var file in files)
                {
                    if(name == file.Name)
                    {
                        var cacheFile = DeckInformation.TryGetCacheItem(localCacheItems, name);
                        long localDateCreated = 0;
                        if (cacheFile != null)
                            localDateCreated = long.Parse(cacheFile.Name.Split(DeckInformation.IMAGE_NAME_SEPARATOR)[1]);

                        if (dateCreated > localDateCreated)                        
                            await ChangeDeckImage(item, name, dateCreated);

                        isHasFile = true;
                        files.Remove(file);
                        break;
                    }
                }

                if (!isHasFile)
                    await ChangeDeckImage(item, name, dateCreated);
            }

            await DeleteLocalUnusedDeckImage(files);
        }
        private async Task ChangeDeckImage(RemoteItem item, string name, long dateCreated)
        {
            var deckId = long.Parse(name);            
            var fileToChange = await CreateTempFileAsync(item.Name);
            await syncInstance.DownloadItemWithPathAsync(Constant.DEFAULT_DECK_IMAGE_FOLDER_SYNC_PATH + "/" + item.Name, fileToChange);
            mainPage.DeckImageChangedEventFire(fileToChange, deckId, dateCreated);
        }
        private async Task DeleteLocalUnusedDeckImage(List<StorageFile> files)
        {
            if(files.Count > 0)
            {                
                foreach (var file in files)
                {
                    if (file.Name == DeckInformation.DEFAULT_IMAGE_NAME)
                        continue;

                    var deckInfor = new DeckInformation(null, 0, 0, long.Parse(file.Name), false);
                    await deckInfor.ChangeBackToDefaultImage();
                }
            }
        }

        private async Task SyncMediaIfNeeded()
        {
            if (!MainPage.UserPrefs.IsSyncMedia)            
                return;
            syncStateDialog.Label = "Start syncing media files...";

            remoteMediaDBFile = await TryGetUnCompressMediaDB();
            if (remoteMediaDBFile == null)
                return;

            var deckMediaFolders = await mainPage.Collection.Media.MapDeckIdToDeckIdFolder();

            using (var remoteMediaDB = new DB(remoteMediaDBFile.Path))
            {                
                string remoteMediaFolderPath = Constant.ANKIROOT_SYNC_FOLDER + "/" + mainPage.Collection.Media.MediaFolder.Name + "/";
                var remoteMeta = remoteMediaDB.GetTable<MetaTable>().First();
                long localLastMediaSync = mainPage.Collection.Media.GetLastUnixTimeSync();     
                                   
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

        private async Task<StorageFile> TryGetUnCompressMediaDB()
        {
            StorageFile returnFile;
            var remoteMediaDBZipItem = await TryGetItemInSyncFolderAsync(Constant.MEDIA_DB_NAME_ZIP);
            if (remoteMediaDBZipItem == null)
            {
                var remoteMediaDBItem = await TryGetItemInSyncFolderAsync(Constant.MEDIA_DB_NAME);
                if (remoteMediaDBItem == null)
                {
                    await UploadAllMediaFiles();
                    return null;
                }

                returnFile = await CreateTempFileAsync(Constant.MEDIA_DB_NAME);
                await syncInstance.DownloadItemWithPathAsync(Constant.MEDIA_DB_SYNC_PATH, returnFile);
            }
            else
            {
                var remoteMediaDBZip = await CreateTempFileAsync(Constant.MEDIA_DB_NAME_ZIP);
                await syncInstance.DownloadItemWithPathAsync(Constant.ANKIROOT_SYNC_FOLDER + "/" + Constant.MEDIA_DB_NAME_ZIP,
                                                             remoteMediaDBZip);
                using (var fileStream = await remoteMediaDBZip.OpenStreamForReadAsync())
                using (var zip = new ZipArchive(fileStream, ZipArchiveMode.Read))
                {
                    zip.ExtractToDirectory(tempSyncFolder.Path);
                }
                returnFile = await tempSyncFolder.TryGetItemAsync(Constant.MEDIA_DB_NAME) as StorageFile;
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

            var localMediasModified = mainPage.Collection.Media.Database.QueryColumn<MediaTable>
                                     ("Select * from media where mtime > ?", localLastMediaSync);            
            ResolveConflictIfHas(remoteMediasModified, localMediasModified);

            long total = remoteMediasModified.Count + localMediasModified.Count;
            var outOfSyncRemoteFiles = await UpdateMediaFilesInLocal(deckMediaFolders, remoteMediaFolderPath, remoteMediasModified, 0, total);
            await DeleteUnusedMediaDeckFolder(deckMediaFolders);

            var outOfSyncLocalFiles = await UpdateMediaFilesInRemoteSever(deckMediaFolders, remoteMediaFolderPath, 
                                                                     localMediasModified, total, remoteMediasModified.Count);
            UpdateDownloadedRemoteMediaDB(remoteMediaDB, localMediasModified, outOfSyncLocalFiles, outOfSyncRemoteFiles);
            
            await UpdateLocalMediaDatabase();

            if(localMediasModified.Count > 0 || outOfSyncRemoteFiles.Count > 0)
                await UpdateSeverMediaDatabase();
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

            foreach(var file in outOfSyncRemoteFiles)
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
                UpdateProgessDialog(SYNC_MEDIA_LABEL, count, total);
                count++;

                var splitString = media.RelativePathName.Split(new char[] { Media.DECK_NAME_SEPARATOR }, 2);
                var deckId = long.Parse(splitString[0]);
                if(!deckMediaFolders.ContainsKey(deckId))
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
                await syncInstance.DownloadItemWithPathAsync(remoteFilePath, newFile);
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
            var allFoldersInMedia = await mainPage.Collection.Media.MediaFolder.GetFoldersAsync();
            foreach (var folder in allFoldersInMedia)
            {
                var key = long.Parse(folder.Name);
                if (!deckMediaFolders.ContainsKey(key))
                    await folder.DeleteAsync();
            }
        }
        private async Task UpdateLocalMediaDatabase()
        {
            mainPage.Collection.Media.Database.Close();
            await remoteMediaDBFile.CopyAsync(Storage.AppLocalFolder, Constant.MEDIA_DB_NAME, NameCollisionOption.ReplaceExisting);
            await mainPage.Collection.Media.ConnectDatabaseAsync();
        }

        private async Task UploadMediaChanges(Dictionary<long, StorageFolder> deckMediaFolders, string remoteMediaFolderPath, MetaTable remoteMeta)
        {
            if (!mainPage.Collection.Media.IsDatabaseModified())
                return;

            var mediasModified = mainPage.Collection.Media.Database.QueryColumn<MediaTable>
                                                     ("Select * from media where mtime > ?", remoteMeta.LastUnixTimeSync);
            long total = mediasModified.Count;            

            await UpdateMediaFilesInRemoteSever(deckMediaFolders, remoteMediaFolderPath, mediasModified, total, 0);

            await DeleteUnusedFolderInServer(deckMediaFolders, remoteMediaFolderPath);
            await UpdateSeverMediaDatabase();
        }

        private async Task<List<MediaTable>> UpdateMediaFilesInRemoteSever(Dictionary<long, StorageFolder> deckMediaFolders, string remoteMediaFolderPath, List<MediaTable> mediasModified, long total, long count)
        {
            List<MediaTable> outOfSyncFiles = new List<MediaTable>();
            foreach (var media in mediasModified)
            {
                UpdateProgessDialog(SYNC_MEDIA_LABEL, count, total);
                count++;

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

                    await syncInstance.UploadItemWithPathAsync(localFile, remoteFilePath);
                }
                else
                {
                    await DeleteRemoteMediaFile(remoteFilePath);
                }
            }
            return outOfSyncFiles;
        }

        private async Task DeleteRemoteMediaFile(string remoteFilePath)
        {
            try
            {
                await syncInstance.DeleteItemWithPathAsync(remoteFilePath);
            }
            catch { }
        }
        private async Task DeleteUnusedFolderInServer(Dictionary<long, StorageFolder> deckMediaFolders, string remoteMediaFolderPath)
        {
            var remoteDeckMediaFolders = await syncInstance.GetChildrenInRemoteFolder
                                         (remoteMediaFolderPath.Substring(0, remoteMediaFolderPath.Length - 1));

            foreach (var folder in remoteDeckMediaFolders)
            {
                var key = long.Parse(folder.Name);
                if (!deckMediaFolders.ContainsKey(key))
                    await syncInstance.DeleteItemWithPathAsync(remoteMediaFolderPath + folder.Name);                    
            }
        }

        private async Task UploadAllMediaFiles()
        {
            var folders = await mainPage.Collection.Media.MediaFolder.GetFoldersAsync();
            long count = 0;
            long total = mainPage.Collection.Media.MediaCount();            
            foreach (var folder in folders)
            {                
                string remoteDeckFolderPath = Constant.ANKIROOT_SYNC_FOLDER + "/"
                                              + mainPage.Collection.Media.MediaFolder.Name + "/"
                                              + folder.Name;
                var files = await folder.GetFilesAsync();
                foreach (var file in files)
                {
                    UpdateProgessDialog(UPLOADING_MEDIA_LABEL, count, total);
                    await syncInstance.UploadItemWithPathAsync(file, remoteDeckFolderPath + "/" + file.Name);
                    count++;
                }
            }

            await UpdateSeverMediaDatabase();
        }

        private void UpdateProgessDialog(string label, long count, long total)
        {            
            syncStateDialog.Label = String.Format(label, count, total);
        }

        private async Task UpdateSeverMediaDatabase()
        {
            mainPage.Collection.Media.SetLastUnixTimeSync(DateTimeOffset.Now.ToUnixTimeSeconds());
            mainPage.Collection.Media.MarkDatabaseClean();
            var localFile = await Storage.AppLocalFolder.GetFileAsync(Constant.MEDIA_DB_NAME);
            var mediaDatabase = await localFile.CopyAsync(tempSyncFolder, localFile.Name + "_upload");

            var zipFile = await tempSyncFolder.CreateFileAsync(Constant.MEDIA_DB_NAME_ZIP + "_upload");
            using (var fileStream = await zipFile.OpenStreamForWriteAsync())
            using (var zip = new ZipArchive(fileStream, ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(mediaDatabase.Path, Constant.MEDIA_DB_NAME);
            }
            await syncInstance.UploadItemWithPathAsync(zipFile, Constant.ANKIROOT_SYNC_FOLDER + "/" + Constant.MEDIA_DB_NAME_ZIP);
        }       

        private async Task PrepareForSyncing()
        {
            await GetDeckImageFolders();

            tempSyncFolder = await Storage.AppLocalFolder.CreateFolderAsync("tempSync", CreationCollisionOption.ReplaceExisting);

            var items = await syncInstance.GetChildrenInRemoteFolder(Constant.ANKIROOT_SYNC_FOLDER);
            foreach (var item in items)
            {
                if (item.Name == Constant.USER_PREF)
                {
                    await GetRemotePrefsAsync(item);
                    return;
                }
            }
        }

        private async Task GetDeckImageFolders()
        {
            deckImageFolder = await Storage.AppLocalFolder.GetFolderAsync(Constant.DEFAULT_DECK_IMAGE_FOLDER_NAME);
            deckImageCacheFolder = await deckImageFolder.TryGetItemAsync(DeckInformation.DECK_IMAGE_SYNC_CACHE_FOLDER) as StorageFolder;
            if(deckImageCacheFolder == null)
                deckImageCacheFolder = await deckImageFolder.CreateFolderAsync(DeckInformation.DECK_IMAGE_SYNC_CACHE_FOLDER);
        }

        private async Task GetRemotePrefsAsync(RemoteItem item)
        {
            var file = await CreateTempFileAsync(Constant.USER_PREF);
            await syncInstance.DownloadItemWithPathAsync(Constant.USER_PREF_SYNC_PATH, file);            
            using (var remotePrefDatabase = new DB(file.Path))
            {
                remoteUserPref = remotePrefDatabase.GetTable<GeneralPreference>().First();
            }
        }

        /// <summary>
        /// Create a file in the temp folder for syncing
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        private async Task<StorageFile> CreateTempFileAsync(string fileName)
        {
            return await tempSyncFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
        }       

        private async Task<StorageFile> CopyToTempFolderAsync(StorageFile file, string fileName = null)
        {
            if (fileName == null)
                fileName = file.Name;

            return await file.CopyAsync(tempSyncFolder, fileName, NameCollisionOption.ReplaceExisting);
        }
      
        /// <summary>
        /// Get a file from sync folder
        /// </summary>
        /// <param name="itemName">Name of the file</param>
        /// <returns>The requested file. Null if not found.</returns>
        private async Task<RemoteItem> TryGetItemInSyncFolderAsync(string itemName)
        {
            var items = await syncInstance.GetChildrenInRemoteFolder(Constant.ANKIROOT_SYNC_FOLDER);
            foreach (var item in items)
            {
                if (item.Name == itemName)
                    return item;
            }
            return null;
        }
    }
}
