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
using AnkiU.UIUtilities;
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
    public class OneDriveSync : ISyncInstance
    {
        private IOneDriveClient oneDriveClient = null;
        private bool isInitRemoteSyncFolder = false;        
  
        public void InitInstance()
        {
            oneDriveClient = OneDriveClientExtensions
                            .GetClientUsingOnlineIdAuthenticator(new string[] { "onedrive.appfolder", "offline_access" });
        }

        public async Task AuthenciateAccount()
        {
            await oneDriveClient.AuthenticateAsync();
        }

        public async Task InitSyncFolderIfNeeded()
        {
            if (isInitRemoteSyncFolder)
                return;
            
            var items = await oneDriveClient.Drive.Special.AppRoot.Children.Request().GetAsync();
            foreach (var item in items)
            {
                if (item.Name == Constant.ANKIROOT_SYNC_FOLDER)
                {
                    isInitRemoteSyncFolder = true;
                    return;
                }
            }

            var folderToCreate = new Item { Folder = new Folder(), Name = Constant.ANKIROOT_SYNC_FOLDER };
            await oneDriveClient.Drive.Special.AppRoot.Children.Request().AddAsync(folderToCreate);
            isInitRemoteSyncFolder = true;
        }

        public async Task<List<RemoteItem>> GetChildrenInRemoteFolder(string folderPath)
        {
            var items = await oneDriveClient.Drive.Special.AppRoot.ItemWithPath(folderPath).Children.Request().GetAsync();
            var remoteItems = new List<RemoteItem>(items.Count);
            foreach(var item in items)            
                remoteItems.Add(new RemoteItem(item.Name));

            return remoteItems;
        }

        public async Task<RemoteItem> TryGetItemInRemotePathAsync(string itemName, string folderPathToSearch)
        {
            var items = await oneDriveClient.Drive.Special.AppRoot.ItemWithPath(folderPathToSearch).Children.Request().GetAsync();
            foreach (var item in items)
            {
                if (item.Name == itemName)
                    return new RemoteItem(itemName);
            }
            return null;
        }

        /// <summary>
        /// Get a file with relative path starts from the root folder
        /// </summary>
        /// <param name="remoteFilePath">Relative path of the file</param>
        /// <returns>The requested file. Throw exception if not found</returns>
        public async Task<RemoteItem> GetRemoteItemWithPathAsync(string remoteFilePath)
        {
            var item = await oneDriveClient.Drive.Special.AppRoot.ItemWithPath(remoteFilePath).Request().GetAsync();
            return new RemoteItem(item.Name);
        }

        public async Task DownloadItemWithPathAsync(string remoteFilePath, StorageFile writeToFile)
        {
            await DownloadWithPathAsync(remoteFilePath, writeToFile, false);
        }

        private async Task DownloadWithPathAsync(string remoteFilePath, StorageFile writeToFile, bool isRetry)
        {
            try
            {
                using (var contentStream = await oneDriveClient.Drive.Special.AppRoot.ItemWithPath(remoteFilePath).Content.Request().GetAsync())
                using (var stream = await writeToFile.OpenStreamForWriteAsync())
                {
                    await contentStream.CopyToAsync(stream);
                }
            }
            catch(OneDriveException ex)
            {
                if(isRetry)
                {
                    throw ex;
                }
                else
                {
                    await RefreshAccessToken();
                    await DownloadWithPathAsync(remoteFilePath, writeToFile, true);
                }
            }
        }

        public async Task UploadItemWithPathAsync(StorageFile fileToUpload, string remoteFilePath)
        {
            await UploadItemAsync(fileToUpload, remoteFilePath, false);
        }

        private async Task UploadItemAsync(StorageFile fileToUpload, string remoteFilePath, bool isRetry)
        {
            try
            {
                using (var contentStream = (await fileToUpload.OpenReadAsync()).AsStreamForRead())
                {
                    await oneDriveClient.Drive.Special.AppRoot.ItemWithPath(remoteFilePath).Content
                                            .Request().PutAsync<Item>(contentStream);                                   
                }
            }
            catch(OneDriveException ex)
            {                
                if(isRetry)
                {
                    throw ex;
                }
                {
                    await RefreshAccessToken();
                    await UploadItemAsync(fileToUpload, remoteFilePath, true);
                }
            }
        }

        private async Task RefreshAccessToken()
        {
            InitInstance();
            await AuthenciateAccount();
        }

        public async Task DeleteItemWithPathAsync(string remoteFilePath)
        {
            try
            {
                await oneDriveClient.Drive.Special.AppRoot.ItemWithPath(remoteFilePath).Request().DeleteAsync();
            }
            catch(OneDriveException)
            {
                await RefreshAccessToken();
                await oneDriveClient.Drive.Special.AppRoot.ItemWithPath(remoteFilePath).Request().DeleteAsync();
            }
        }

        public void Close()
        {
            oneDriveClient = null;
            GC.Collect();            
        }

        public async Task ExceptionHandler(Exception ex)
        {
            var oneDriveException = ex as OneDriveException;
            if(oneDriveException == null)
            {
                await UIHelper.ShowMessageDialog("Unexpected error.");
                return;
            }

            if (oneDriveException.IsMatch(OneDriveErrorCode.AccessDenied.ToString()))
                await UIHelper.ShowMessageDialog("Access to your OneDrive folder is denied.");
            else if (oneDriveException.IsMatch(OneDriveErrorCode.AuthenticationFailure.ToString()))
                await UIHelper.ShowMessageDialog("Authentication Failed.");
            else if (oneDriveException.IsMatch(OneDriveErrorCode.ItemNotFound.ToString()))
                await UIHelper.ShowMessageDialog("File not found.");
            else if (oneDriveException.IsMatch(OneDriveErrorCode.QuotaLimitReached.ToString()))
                await UIHelper.ShowMessageDialog("Quota limit reached");
            else
                await UIHelper.ShowMessageDialog(oneDriveException.Message);
        }
    }
}
