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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;

namespace AnkiU.Anki.Syncer
{
    public interface ISyncInstance
    {
        /// <summary>
        /// Init an instance for syncing. This is usually an object from the syncing SDK (ex: OneDrive, DropBox, etc.)
        /// </summary>
        void InitInstance();
        
        /// <summary>
        /// Release all unmanaged resource if used
        /// </summary>
        void Close();

        /// <summary>
        /// Authenciate user's account
        /// </summary>
        /// <returns></returns>
        Task AuthenciateAccount();

        /// <summary>
        /// Init the root sync folder in the remote server
        /// </summary>
        /// <returns></returns>
        Task InitSyncFolderIfNeeded();

        /// <summary>
        /// Get all children of a folder in remote server
        /// </summary>
        /// <param name="remoteFolderPath"></param>
        /// <returns></returns>
        Task<List<RemoteItem>> GetChildrenInRemoteFolder(string remoteFolderPath);        

        /// <summary>
        /// Try get an item in the remote server
        /// </summary>
        /// <param name="itemName">Name of the item</param>
        /// <param name="folderPathToSearch">Relative path from the root sync folder. Ex: "Anki Universal"</param>
        /// <returns>Null if not found. Otherwise, the requested item</returns>
        Task<RemoteItem> TryGetItemInRemotePathAsync(string itemName, string folderPathToSearch);

        /// <summary>
        /// Get an item the remote server
        /// </summary>
        /// <param name="remoteFilePath">Relative path from the root sync folder. Ex: "Anki Universal/RequestedItemName"</param>
        /// <returns>The requested item or an exception if not found</returns>
        Task<RemoteItem> GetRemoteItemWithPathAsync(string remoteFilePath);

        /// <summary>
        /// Download an item from the remote sever to local app folder
        /// </summary>
        /// <param name="remoteFilePath">Relative path from the root sync folder. Ex: "Anki Universal/RequestedItemName"</param>
        /// <param name="writeToFile">File in the local app folder to be written into</param>
        /// <returns></returns>
        Task DownloadItemWithPathAsync(string remoteFilePath, StorageFile writeToFile);

        /// <summary>
        /// Upload an item from local app folder to the remote sever 
        /// </summary>
        /// <param name="fileToUpload">File in the local app folder to be uploaded</param>
        /// <param name="remoteFilePath">Relative path from the root sync folder. Ex: "Anki Universal/RequestedItemName"</param>
        /// <returns></returns>
        Task UploadItemWithPathAsync(StorageFile fileToUpload, string remoteFilePath);

        /// <summary>
        /// Delete an item from the remote sever
        /// </summary>
        /// <param name="remoteFilePath">Relative path from the root sync folder. Ex: "Anki Universal/RequestedItemName"</param>
        /// <returns></returns>
        Task DeleteItemWithPathAsync(string remoteFilePath);

        /// <summary>
        /// Handle exception
        /// </summary>
        /// <param name="ex"></param>
        /// <returns></returns>
        Task ExceptionHandler(Exception ex);
    }
}
