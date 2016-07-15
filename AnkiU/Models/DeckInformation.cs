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
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System;
using System.Threading.Tasks;
using Windows.Storage;
using System.Collections.Generic;

namespace AnkiU.Models
{
    public class DeckInformation : INotifyPropertyChanged
    {
        public const char IMAGE_NAME_SEPARATOR = '_';
        public const string DEFAULT_IMAGE_NAME = "Default.png";
        public const string DECK_IMAGE_SYNC_CACHE_FOLDER = "SyncCache";
        public static readonly string DEFAULT_FOLDER_PATH = Storage.AppLocalFolder.Path + "/" + Constant.DEFAULT_DECK_IMAGE_FOLDER_NAME + "/";

        private string name;
        public string Name
        {
            get
            {
                return name;
            }
            set
            {
                name = value;
                RaisePropertyChanged("Name");
            }
        }

        private string imagePath;
        public string ImagePath
        {
            get
            {
                return imagePath;
            }
            set
            {
                imagePath = value;
                RaisePropertyChanged("ImagePath");
            }
        }

        private int newCards;
        public int NewCards
        {
            get
            {
                return newCards;
            }
            set
            {
                newCards = value;
                RaisePropertyChanged("NewCards");
            }
        }

        private int dueCards;
        public int DueCards
        {
            get
            {
                return dueCards;
            }
            set
            {
                dueCards = value;
                RaisePropertyChanged("DueCards");
            }
        }

        private long id;
        public long Id
        {
            get
            {
                return id;
            }
            set
            {
                id = value;
            }
        }

        private bool isDynamic;
        public bool IsDynamic
        {
            get
            {
                return isDynamic;
            }
            set
            {
                isDynamic = value;
            }
        }

        public DeckInformation(string Name, int NewCards, int DueCards, long Id, bool IsDynamic)
        {
            this.name = Name;
            this.imagePath = ImagePath;
            this.newCards = NewCards;
            this.dueCards = DueCards;
            this.id = Id;
            this.isDynamic = IsDynamic;
            imagePath = DEFAULT_FOLDER_PATH + Id;
                        
            if (!File.Exists(imagePath))
                imagePath = DEFAULT_FOLDER_PATH + DEFAULT_IMAGE_NAME;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public async Task ChangeBackToDefaultImage()
        {
            string oldImage = ImagePath;            
            ImagePath = DEFAULT_FOLDER_PATH + DEFAULT_IMAGE_NAME;
            if (oldImage == ImagePath)
                return;

            StorageFolder folder = await GetDeckImageFolder();
            var file = await folder.TryGetItemAsync(id.ToString()) as StorageFile;
            if (file != null)
            {
                await file.DeleteAsync();
                await DeleteCacheFile(folder);
            }
        }

        private async Task DeleteCacheFile(StorageFolder folder)
        {
            var cacheFolder = await GetDeckImageSyncCacheFolder(folder);
            var cacheFile = TryGetCacheItem(await cacheFolder.GetFilesAsync(), id.ToString());
            if (cacheFile != null)
                await cacheFile.DeleteAsync();
        }

        public async Task ChangeImage(StorageFile file, long? dateCreated = null)
        {
            //First force reload new image and remove old one in cache
            ImagePath = file.Path;
            GC.Collect();

            StorageFolder folder = await GetDeckImageFolder();
            var oldFile = await folder.TryGetItemAsync(id.ToString()) as StorageFile;
            if (oldFile != null)
                await oldFile.DeleteAsync();

            file = await file.CopyAsync(folder, Id.ToString(), NameCollisionOption.ReplaceExisting);
            ImagePath = DEFAULT_FOLDER_PATH + Id;

            await cacheFileForSyncing(file, dateCreated, folder);
        }

        private async Task<long?> cacheFileForSyncing(StorageFile file, long? dateCreated, StorageFolder folder)
        {
            if (dateCreated == null)
                dateCreated = DateTimeOffset.Now.ToUnixTimeSeconds();
            var cacheFolder = await GetDeckImageSyncCacheFolder(folder);
            var cacheFile = TryGetCacheItem(await cacheFolder.GetFilesAsync(), id.ToString());
            if (cacheFile == null)
            {
                await cacheFolder.CreateFileAsync(file.Name + IMAGE_NAME_SEPARATOR + dateCreated, CreationCollisionOption.ReplaceExisting);
            }
            else
            {
                string newName = file.Name + IMAGE_NAME_SEPARATOR + dateCreated;
                if(newName != cacheFile.Name) //Make sure new name is different or else we get exception
                    await cacheFile.RenameAsync(newName, NameCollisionOption.ReplaceExisting);
            }

            return dateCreated;
        }

        private static async Task<StorageFolder> GetDeckImageFolder()
        {
            StorageFolder folder = await Storage.AppLocalFolder.TryGetItemAsync(Constant.DEFAULT_DECK_IMAGE_FOLDER_NAME) as StorageFolder;
            if (folder == null)
                folder = await Storage.AppLocalFolder.CreateFolderAsync(Constant.DEFAULT_DECK_IMAGE_FOLDER_NAME);
            return folder;
        }

        private static async Task<StorageFolder> GetDeckImageSyncCacheFolder(StorageFolder deckImagefolder)
        {
            StorageFolder folder = await deckImagefolder.TryGetItemAsync(DECK_IMAGE_SYNC_CACHE_FOLDER) as StorageFolder;
            if (folder == null)
                folder = await deckImagefolder.CreateFolderAsync(DECK_IMAGE_SYNC_CACHE_FOLDER);
            return folder;
        }

        public static StorageFile TryGetCacheItem(IEnumerable<StorageFile> files, string fileIdToGet)
        {
            foreach(var file in files)
            {
                string id = file.Name.Split(IMAGE_NAME_SEPARATOR)[0];
                if (id == fileIdToGet)
                    return file;
            }

            return null;
        }

        public override string ToString()
        {
            return this.Name;
        }
    }
}
