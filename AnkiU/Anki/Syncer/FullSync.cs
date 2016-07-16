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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

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
        private SyncDialog dialog;

        private StorageFile remoteMediaDBFile;

        public FullSync(MainPage mainPage, ISyncInstance syncInstance)
        {
            this.mainPage = mainPage;
            dialog = new SyncDialog(mainPage.CurrentDispatcher);
            this.syncInstance = syncInstance;            
        }

        public async Task StartSync()
        {                       
            try
            {
                dialog.Show(MainPage.UserPrefs.IsReadNightMode);
                dialog.Label = "Authenticating...";
                syncInstance.InitInstance();
                await syncInstance.AuthenciateAccount();
                
                dialog.Label = "Preparing files...";
                await syncInstance.InitSyncFolderIfNeeded();
                await PrepareForSyncing();

                if (remoteUserPref == null || MainPage.UserPrefs.LastSyncTime >= remoteUserPref.LastSyncTime)
                    await UploadToServer();
                else
                    await DownloadFromServer();

                await SyncMediaIfNeeded();
                dialog.Label = "Finished.";
                syncInstance.Close();
                await Task.Delay(500);
            }
            catch (Exception ex)
            {
                await syncInstance.ExceptionHandler(ex);
            }
            finally
            {
                if(tempSyncFolder != null)
                    await tempSyncFolder.DeleteAsync();
                dialog.Close();
            }
        }

        private async Task UploadToServer()
        {
            dialog.Label = "Uploading database...";
            await UploadPrefDatabase();
            await UploadCollectionDatabase();            
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
            mainPage.Collection.SaveAndCommit();
            var collectionFile = await CopyToTempFolderAsync(await Storage.AppLocalFolder.GetFileAsync(Constant.COLLECTION_NAME));
            await syncInstance.UploadItemWithPathAsync(collectionFile, Constant.ANKI_COL_SYNC_PATH);
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
            dialog.Label = "Donwloading database...";                   
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
            var collectionDBFile = await CreateTempFileAsync(Constant.COLLECTION_NAME);            
            await syncInstance.DownloadItemWithPathAsync(Constant.ANKI_COL_SYNC_PATH, collectionDBFile);            
            mainPage.Collection.Close();
            await collectionDBFile.CopyAsync(Storage.AppLocalFolder, collectionDBFile.Name, NameCollisionOption.ReplaceExisting);            
            mainPage.Collection = await Storage.OpenOrCreateCollection(Storage.AppLocalFolder, Constant.COLLECTION_NAME);            
            await mainPage.NavigateToDeckSelectPage();
            mainPage.ContentFrame.BackStack.RemoveAt(0);
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
            dialog.Label = "Start syncing media files...";
            var remoteMediaDBItem = await TryGetItemInSyncFolderAsync(Constant.MEDIA_DB_NAME);
            if (remoteMediaDBItem == null)
            {
                await UploadAllMediaFiles();
                return;
            }
            
            remoteMediaDBFile = await CreateTempFileAsync(Constant.MEDIA_DB_NAME);
            await syncInstance.DownloadItemWithPathAsync(Constant.MEDIA_DB_SYNC_PATH, remoteMediaDBFile);
            var deckMediaFolders = await mainPage.Collection.Media.MapDeckIdToDeckIdFolder();

            using (var remoteMediaDB = new DB(remoteMediaDBFile.Path))
            {                
                string remoteMediaFolderPath = Constant.ANKIROOT_SYNC_FOLDER + "/" + mainPage.Collection.Media.MediaFolder.Name + "/";
                var remoteMeta = remoteMediaDB.GetTable<MetaTable>().First();
                long localLastMediaSync = mainPage.Collection.Media.GetLastUnixTimeSync();     
                                   
                if (remoteMeta.LastUnixTimeSync > localLastMediaSync)
                {
                    await DownLoadMediaChanges(remoteMediaDB, deckMediaFolders, 
                                                remoteMediaFolderPath, remoteMeta, localLastMediaSync);
                }
                else
                {
                    await UploadMediaChanges(deckMediaFolders, remoteMediaFolderPath, remoteMeta);
                }
            }                         
        }      

        private async Task DownLoadMediaChanges(DB remoteMediaDB, Dictionary<long, StorageFolder> deckMediaFolders, string remoteMediaFolderPath, MetaTable remoteMeta, long localLastMediaSync)
        {
            var mediasModified = remoteMediaDB.QueryColumn<MediaTable>
                                ("Select * from media where mtime > ?", localLastMediaSync);
            long count = 0;
            long total = mediasModified.Count;
            foreach (var media in mediasModified)
            {
                UpdateProgessDialog(SYNC_MEDIA_LABEL, count, total);                
                count++;

                var splitString = media.RelativePathName.Split(new char[] { Media.DECK_NAME_SEPARATOR }, 2);
                var deckId = long.Parse(splitString[0]);
                var name = splitString[1];
                if (media.IsAdded)
                {
                    var localMediaInfor = mainPage.Collection.Media.Database
                                          .QueryFirstRow<MediaTable>("Select * from media where fname = ? ", media.RelativePathName);
                    if (localMediaInfor.Count == 0 || localMediaInfor[0].IsAdded == false
                        || localMediaInfor[0].ModifiedTime < media.ModifiedTime)
                    {
                        await DownloadMediaFilesFromSever(deckMediaFolders, remoteMediaFolderPath, media, deckId, name);
                    }
                }
                else
                {
                    await RemoveMediaFilesInLocal(deckMediaFolders, media, deckId, name);
                }                
            }
            await DeleteUnusedMediaDeckFolder(deckMediaFolders);
            await UpdateLocalMediaDatabase();
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
            mainPage.Collection.Media.ConnectDatabaseAsync();
        }

        private async Task UploadMediaChanges(Dictionary<long, StorageFolder> deckMediaFolders, string remoteMediaFolderPath, MetaTable remoteMeta)
        {
            var mediasModified = mainPage.Collection.Media.Database.QueryColumn<MediaTable>
                                                     ("Select * from media where mtime > ?", remoteMeta.LastUnixTimeSync);
            long count = 0;
            long total = mediasModified.Count;
            List<Item> deletedFolders = new List<Item>();
            foreach (var media in mediasModified)
            {
                UpdateProgessDialog(SYNC_MEDIA_LABEL, count, total);                
                count++;

                var splitString = media.RelativePathName.Split(new char[] { Media.DECK_NAME_SEPARATOR }, 2);
                var deckId = long.Parse(splitString[0]);
                var name = splitString[1];
                string remoteFilePath = remoteMediaFolderPath + media.RelativePathName;
                if (media.IsAdded)
                {
                    var localFile = await deckMediaFolders[deckId].TryGetItemAsync(name) as StorageFile;
                    if (localFile == null)
                        continue;

                    await syncInstance.UploadItemWithPathAsync(localFile, remoteFilePath);
                }
                else
                {
                    await DeleteRemoteMediaFile(remoteFilePath);
                }
            }

            await DeleteUnusedFolderInServer(deckMediaFolders, remoteMediaFolderPath);
            await UpdateSeverMediaDatabase();
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
            dialog.Label = String.Format(label, count, total);
        }

        private async Task UpdateSeverMediaDatabase()
        {
            mainPage.Collection.Media.SetLastUnixTimeSync(DateTimeOffset.Now.ToUnixTimeSeconds());
            var localFile = await Storage.AppLocalFolder.GetFileAsync(Constant.MEDIA_DB_NAME);
            var mediaDatabase = await localFile.CopyAsync(tempSyncFolder, localFile.Name + "_upload");            
            await syncInstance.UploadItemWithPathAsync(mediaDatabase, Constant.MEDIA_DB_SYNC_PATH);
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
                    await GetRemotePrefs(item);
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

        private async Task GetRemotePrefs(RemoteItem item)
        {
            var file = await CreateTempFileAsync(Constant.USER_PREF);
            await syncInstance.DownloadItemWithPathAsync(Constant.USER_PREF_SYNC_PATH, file);            
            using (var remotePrefDatabase = new DB(tempSyncFolder.Path + "\\" + Constant.USER_PREF))
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

        private async Task<StorageFile> CopyToTempFolderAsync(StorageFile file)
        {
            return await file.CopyAsync(tempSyncFolder, file.Name, NameCollisionOption.ReplaceExisting);
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
