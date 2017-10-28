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
using Microsoft.OneDrive.Sdk.Authentication;
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
        OnlineIdAuthenticationProvider msaAuthenticationProvider;

        public void InitInstance()
        {
            msaAuthenticationProvider = new OnlineIdAuthenticationProvider(new string[] { "onedrive.appfolder", "offline_access" });
            oneDriveClient = new OneDriveClient(msaAuthenticationProvider);        
        }

        public async Task AuthenciateAccount()
        {
            await msaAuthenticationProvider.AuthenticateUserAsync();
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
            foreach (var item in items)
            {
                long lastModified = GetLastDateModified(item);
                remoteItems.Add(new RemoteItem(item.Name, lastModified));
            }
            return remoteItems;
        }

        public async Task<RemoteItem> TryGetItemInRemotePathAsync(string itemName, string folderPathToSearch)
        {
            var items = await oneDriveClient.Drive.Special.AppRoot.ItemWithPath(folderPathToSearch).Children.Request().GetAsync();
            foreach (var item in items)
            {
                if (item.Name == itemName)
                {
                    long lastModified = GetLastDateModified(item);
                    return new RemoteItem(itemName, lastModified);
                }
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
            long lastModified = GetLastDateModified(item);
            return new RemoteItem(item.Name, lastModified);
        }

        public async Task DownloadItemWithPathAsync(string remoteFilePath, StorageFile writeToFile, bool isSkipNotFoundItem = false)
        {
            await DownloadWithPathAsync(remoteFilePath, writeToFile, isSkipNotFoundItem, false);
        }

        private async Task DownloadWithPathAsync(string remoteFilePath, StorageFile writeToFile, bool isSkipNotFoundItem, bool isRetry)
        {
            try
            {
                using (var contentStream = await oneDriveClient.Drive.Special.AppRoot.ItemWithPath(remoteFilePath).Content.Request().GetAsync())
                using (var stream = await writeToFile.OpenStreamForWriteAsync())
                {
                    await contentStream.CopyToAsync(stream);
                }
            }
            catch(Microsoft.Graph.ServiceException ex)
            {
                if(isRetry)
                {
                    if (ex.IsMatch(OneDriveErrorCode.ItemNotFound.ToString()) && isSkipNotFoundItem)                    
                        return;
                    else
                        throw ex;
                }
                else
                {
                    await RefreshAccessToken();
                    await DownloadWithPathAsync(remoteFilePath, writeToFile, isSkipNotFoundItem, true);
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
            catch(Microsoft.Graph.ServiceException ex)
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
            catch
            {
                await RefreshAccessToken();
                await oneDriveClient.Drive.Special.AppRoot.ItemWithPath(remoteFilePath).Request().DeleteAsync();
            }
        }

        public void Close()
        {
            oneDriveClient = null;         
        }

        public async Task ExceptionHandler(Exception ex)
        {
            var oneDriveException = ex as Microsoft.Graph.ServiceException;
            if(oneDriveException == null)
            {
                await UIHelper.ShowMessageDialog(ex.Message + "\n" + ex.StackTrace);
                return;
            }

            if (oneDriveException.IsMatch(OneDriveErrorCode.AccessDenied.ToString()))
                await UIHelper.ShowMessageDialog("Access to your OneDrive folder is denied.");
            else if (oneDriveException.IsMatch("authenticationFailure"))
                await UIHelper.ShowMessageDialog("Authentication Failed.");            
            else if (oneDriveException.IsMatch(OneDriveErrorCode.ItemNotFound.ToString()))
                await UIHelper.ShowMessageDialog("File not found.");
            else if (oneDriveException.IsMatch(OneDriveErrorCode.QuotaLimitReached.ToString()))
                await UIHelper.ShowMessageDialog("Quota limit reached");
            else
                await UIHelper.ShowMessageDialog(oneDriveException.Error.Message);
        }

        private static long GetLastDateModified(Item item)
        {
            long lastModified;
            if (item.LastModifiedDateTime != null)
                lastModified = item.LastModifiedDateTime.Value.ToUnixTimeSeconds();
            else
                lastModified = 0;
            return lastModified;
        }
    }
}
