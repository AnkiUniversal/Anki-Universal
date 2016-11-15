using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Storage;
using Windows.Storage.Streams;

namespace AnkiU.Anki.Syncer
{
    public class SyncInstanceMock : ISyncInstance
    {
        private StorageFolder syncFolder;

        public SyncInstanceMock(StorageFolder SyncFolder)
        {
            syncFolder = SyncFolder;
        }

        public async Task AuthenciateAccount()
        {
            return;
        }

        public void Close()
        {
            return;
        }

        public async Task DeleteItemWithPathAsync(string remoteFilePath)
        {
            var file = await syncFolder.TryGetItemAsync(remoteFilePath) as StorageFile;
            if(file != null)
                await file.DeleteAsync();
        }

        public async Task DownloadItemWithPathAsync(string remoteFilePath, StorageFile writeToFile)
        {
            var file = await syncFolder.TryGetItemAsync(remoteFilePath) as StorageFile;
            if (file == null)
                return;

            await CopyFileStream(writeToFile, file);
        }

        private static async Task CopyFileStream(StorageFile writeToFile, StorageFile sourceFile)
        {
            using (var readStream = await sourceFile.OpenAsync(FileAccessMode.Read))
            using (var writeStream = await writeToFile.OpenAsync(FileAccessMode.ReadWrite))
            {
                Windows.Storage.Streams.Buffer buffer = new Windows.Storage.Streams.Buffer(1024);
                for (ulong i = 0; i < readStream.Size; i += 1024)
                {
                    var readData = await readStream.GetInputStreamAt(i).ReadAsync(buffer, 1024, InputStreamOptions.None);
                    await writeStream.GetOutputStreamAt(i).WriteAsync(readData);
                }
            }
        }

        public Task ExceptionHandler(Exception ex)
        {
            throw ex;
        }

        public async Task<List<RemoteItem>> GetChildrenInRemoteFolder(string remoteFolderPath)
        {
            var folder = await syncFolder.GetFolderAsync(remoteFolderPath);            

            var items = await folder.GetItemsAsync();
            List<RemoteItem> remoteItems = new List<RemoteItem>();
            foreach (var item in items)                            
                remoteItems.Add(new RemoteItem(item.Name, await GetLastDateModified(item)));

            return remoteItems;
        }

        public async Task<RemoteItem> GetRemoteItemWithPathAsync(string remoteFilePath)
        {
            var item = await syncFolder.GetItemAsync(remoteFilePath);
            return new RemoteItem(item.Name, await GetLastDateModified(item));
        }

        public void InitInstance()
        {
            return;
        }

        public async Task InitSyncFolderIfNeeded()
        {
            return;
        }

        public async Task<RemoteItem> TryGetItemInRemotePathAsync(string itemName, string folderPathToSearch)
        {
            var folder = await syncFolder.GetFolderAsync(folderPathToSearch);
            var item = await folder.TryGetItemAsync(itemName);
            if (item == null)
                return null;
            else
                return new RemoteItem(item.Name, await GetLastDateModified(item));
        }

        public async Task UploadItemWithPathAsync(StorageFile fileToUpload, string remoteFilePath)
        {
            var splitString = remoteFilePath.Split('\\', '/');
            var folder = syncFolder;
            for(int i = 0; i < splitString.Length - 1; i++)
            {
                folder = await folder.TryGetItemAsync(splitString[i]) as StorageFolder;
                if (folder == null)
                    folder = await folder.CreateFolderAsync(splitString[i]);
            }
            await fileToUpload.CopyAsync(folder, splitString[splitString.Length - 1], NameCollisionOption.ReplaceExisting);
        }

        private async Task<long> GetLastDateModified(IStorageItem item)
        {
            return (await item.GetBasicPropertiesAsync()).DateModified.ToUnixTimeSeconds();            
        }
    }
}
