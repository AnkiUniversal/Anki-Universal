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
        private StorageFolder tempSyncFolder = null;
        private StorageFolder deckImageFolder = null;
        private StorageFolder deckImageCacheFolder = null;
        private ISyncInstance syncInstance = null;     
        private GeneralPreference remoteUserPref = null;
        private MainPage mainPage;
        private SyncDialog syncStateDialog;
        private bool isSyncStateDialogClose = false;      
        
        public ISyncInstance SyncInstance { get { return syncInstance; } }
        public SyncDialog SyncStateDialog { get { return syncStateDialog; } }
        public MainPage MainPage { get { return mainPage; } }
        public StorageFolder TempSyncFolder { get { return tempSyncFolder; } }

        public FullSync(MainPage mainPage, ISyncInstance syncInstance)
        {
            this.mainPage = mainPage;
            syncStateDialog = new SyncDialog(mainPage.CurrentDispatcher);
            syncStateDialog.Opened += SyncStateDialogOpened;
            syncStateDialog.Closed += SyncStateDialogClosed;
            this.syncInstance = syncInstance;            
        }

        /// <summary>
        /// Get a file from sync folder
        /// </summary>
        /// <param name="itemName">Name of the file</param>
        /// <returns>The requested file. Null if not found.</returns>
        public async Task<RemoteItem> TryGetItemInSyncFolderAsync(string itemName)
        {
            var items = await syncInstance.GetChildrenInRemoteFolder(Constant.ANKIROOT_SYNC_FOLDER);
            foreach (var item in items)
            {
                if (item.Name == itemName)
                    return item;
            }
            return null;
        }

        /// <summary>
        /// Create a file in the temp folder for syncing
        /// </summary>
        /// <param name="fileName"></param>
        /// <returns></returns>
        public async Task<StorageFile> CreateTempFileAsync(string fileName)
        {
            return await tempSyncFolder.CreateFileAsync(fileName, CreationCollisionOption.ReplaceExisting);
        }

        public async Task<StorageFile> CopyToTempFolderAsync(StorageFile file, string fileName = null)
        {
            if (fileName == null)
                fileName = file.Name;

            return await file.CopyAsync(tempSyncFolder, fileName, NameCollisionOption.ReplaceExisting);
        }

        public void UpdateProgessDialog(string label, long count, long total)
        {
            syncStateDialog.Label = String.Format(label, count, total);
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
                bool isSuccess = false;
                await PreparingFilesAsync();
                var remoteCollectionZipFile = await TryGetItemInSyncFolderAsync(Constant.COLLECTION_NAME_ZIP);
                if (remoteUserPref == null
                    || MainPage.UserPrefs.LastSyncTime >= remoteUserPref.LastSyncTime)
                {
                    isSuccess = await UploadToserverIfNeeded(isSuccess, remoteCollectionZipFile);
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
                        isSuccess = await DownloadFromServer();                    
                    else
                        isSuccess = await UploadToServer();
                }
                if (isSuccess)
                {
                    await SyncMediaIfNeeded();
                    syncStateDialog.Label = "Finished.";
                    await Task.Delay(500);
                }
                else
                {
                    syncStateDialog.Label = "Failed.";
                    await UIHelper.ShowMessageDialog("Failed to sync your data. Please check your internet connection.\n");
                }                
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

        private async Task<bool> UploadToserverIfNeeded(bool isSuccess, RemoteItem remoteCollectionZipFile)
        {
            if (GetLastModifiedTimeInSecond() > MainPage.UserPrefs.LastSyncTime
                                    || remoteCollectionZipFile == null)
            {
                isSuccess = await UploadToServer();
            }
            else //No need to upload -> sync is success by default
                isSuccess = true;
            return isSuccess;
        }

        private async Task PreparingFilesAsync()
        {
            syncStateDialog.Label = "Authenticating...";
            syncStateDialog.Show(MainPage.UserPrefs.IsReadNightMode);            
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
                            + "Choosing \"Upload\" will upload your current collection to the server.\n\n"
                            + "(If in doubt, choose \"Download\" as you can restore your data from the backup.)";
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

        private async Task<bool> UploadToServer()
        {
            try
            {
                syncStateDialog.Label = "Uploading database...";
                await UploadCollectionDatabase();
                await UploadPrefDatabase();
                await UploadDeckImages();

                return true;
            }
            catch
            {
                return false;
            }
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
            try
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
            catch //Errors in uploading deck images should not stop syncing
            { }
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

        private async Task<bool> DownloadFromServer()
        {            
            try
            {
                syncStateDialog.Label = "Donwloading database...";
                if (await DownloadCollectionDatabase())
                {
                    await DownloadDeckImages();
                    UpdatePrefDatabase();
                    return true;
                }
                return false;
            }
            catch
            {
                return false;
            }
        }

        private void UpdatePrefDatabase()
        {
            MainPage.UserPrefs.LastSyncTime = remoteUserPref.LastSyncTime;
            MainPage.UserPrefs.Helps = remoteUserPref.Helps;
            mainPage.UpdateUserPreference();
            if(mainPage.AllHelps != null)
                mainPage.AllHelps.SetupTutorialsVisibility();
        }

        private async Task<bool> DownloadCollectionDatabase()
        {
            StorageFile collectionFile = await GetUnCompressedDBFile();
            if (collectionFile == null)
            {
                await UIHelper.ShowMessageDialog("Couldn't download database file.");
                return false;
            }

            mainPage.Collection.Close();
            await collectionFile.CopyAsync(Storage.AppLocalFolder, Constant.COLLECTION_NAME, NameCollisionOption.ReplaceExisting);
            mainPage.Collection = await Storage.OpenOrCreateCollection(Storage.AppLocalFolder, Constant.COLLECTION_NAME);
            await mainPage.NavigateToDeckSelectPage();
            mainPage.ContentFrame.BackStack.RemoveAt(0);
            return true;
        }

        private async Task<StorageFile> GetUnCompressedDBFile()
        {            
            try
            {
                StorageFile collectionFile;
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
                return collectionFile;
            }
            catch
            {
                return null;
            }                                                                    
        }

        private async Task DownloadDeckImages()
        {
            try
            {
                var files = (await deckImageFolder.GetFilesAsync()).ToList();
                var remoteFolder = await TryGetItemInSyncFolderAsync(Constant.DEFAULT_DECK_IMAGE_FOLDER_NAME);
                if (remoteFolder != null)
                {
                    await DownloadChangedDeckImages(files);
                }
            }
            catch //error in download deck image should not stop syncing
            { }
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
            MediaSync mediaSync = new MediaSync(this);
            await mediaSync.StartSyncMedia();
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

    }
}
