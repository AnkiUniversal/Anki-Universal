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
using Windows.Data.Json;
using Windows.Storage;
using System.IO;
using System.Text.RegularExpressions;
using System.IO.Compression;
using System.Diagnostics;

namespace AnkiU.AnkiCore.Exporter
{
    public class AnkiPackageExporter : AnkiExporter
    {
        private const string MEDIA_BASE_FOLDER = "-1";

        public delegate void ExportFinishedHandler(string message);
        public event ExportFinishedHandler ExportFinishedEvent;

        public AnkiPackageExporter(Collection sourCol) : base(sourCol)
        {
        }

        public AnkiPackageExporter(Collection sourceCol, long did) : base(sourceCol, did)
        {
        }

        public async override Task ExportInto(StorageFolder destFolder, string fileName)
        {
            StorageFolder tempExportPackageFolder = 
                await Storage.AppLocalFolder.TryGetItemAsync("tempExportFolder") as StorageFolder;            
            try
            {
                //We have to create a temp folder in app folder so that SQLite can access it,
                //We will move the package file to destFolder after it has been created
                if(tempExportPackageFolder == null)
                    tempExportPackageFolder = await Storage.AppLocalFolder.CreateFolderAsync("tempExportFolder");

                string absolutePath = tempExportPackageFolder.Path + "\\" + fileName;
                
                // open a zip file
                using (FileStream fileStream = new FileStream(absolutePath, FileMode.Create))
                using (ZipArchive archive = new ZipArchive(fileStream, ZipArchiveMode.Update))
                {
                    // if all decks and scheduling included, full export
                    KeyValuePair<JsonObject, JsonObject> mediaPair;
                    JsonObject media;
                    JsonObject mediaAnkiU = null;
                    if (includeSched && deckId == null)
                    {
                        mediaPair = await ExportVerbatim(archive);                        
                    }
                    else
                    {
                        // otherwise, filter
                        mediaPair = await ExportFiltered(tempExportPackageFolder, archive, fileName);
                    }
                    media = mediaPair.Key;
                    mediaAnkiU = mediaPair.Value;
                    // media map
                    ZipArchiveEntry entry = archive.CreateEntry("media");
                    using (StreamWriter writer = new StreamWriter(entry.Open()))
                    {
                        writer.WriteLine(Utils.JsonToString(media));
                    }

                    //media map for AnkiU
                    if (mediaAnkiU != null)
                    {
                        entry = archive.CreateEntry("mediaAnkiU");
                        using (StreamWriter writer = new StreamWriter(entry.Open()))
                        {
                            writer.WriteLine(Utils.JsonToString(mediaAnkiU));
                        }
                    }
                }

                StorageFile package = await tempExportPackageFolder.GetFileAsync(fileName);
                await package.MoveAsync(destFolder, package.Name, NameCollisionOption.GenerateUniqueName);
                ExportFinishedEvent?.Invoke("Successed");
            }
            catch(Exception e)
            {
                ExportFinishedEvent?.Invoke(e.Message);
            }
            finally
            {
                tempExportPackageFolder = await sourceCol.Folder.TryGetItemAsync("tempExportFolder") as StorageFolder;
                if (tempExportPackageFolder != null)
                {
                    await tempExportPackageFolder.DeleteAsync();
                    tempExportPackageFolder = null;
                }
            }
        }

        private async Task<KeyValuePair<JsonObject, JsonObject>> ExportVerbatim(ZipArchive archive)
        {
            // close our deck & write it into the zip file, and reopen
            count = sourceCol.CardCount();
            string sourcePath = sourceCol.Folder.Path + "\\" + sourceCol.RelativePath;
            sourceCol.Close();
            try
            {
                archive.CreateEntryFromFile(sourcePath, "collection.anki2");
                return await PackageMediaFiles(archive);
            }
            catch (Exception)
            {
                throw new Exception("Unknown error.");
            }
            finally
            {
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                sourceCol.ReOpen();
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            }                        
        }

        private async Task<KeyValuePair<JsonObject, JsonObject>> PackageMediaFiles(ZipArchive archive)
        {            
            JsonObject mediaAnki = new JsonObject();
            JsonObject mediaAnkiU = new JsonObject();            

            if (sourceCol.Media.MediaFolder != null && includeMedia)
            {
                var folders = await sourceCol.Media.MediaFolder.GetFoldersAsync();
                int count = await MapAndAddMediafiles(archive, mediaAnki, mediaAnkiU, folders);
                await MapAndAddAllStaticMediafiles(archive, mediaAnki, mediaAnkiU, count);
            }
            return new KeyValuePair<JsonObject, JsonObject>(mediaAnki, mediaAnkiU);
        }

        private async Task<int> MapAndAddMediafiles(ZipArchive archive, JsonObject mediaAnki, 
                                               JsonObject mediaAnkiU, IReadOnlyList<StorageFolder> deckIdFolders)
        {
            int count = 0;
            foreach (var folder in deckIdFolders)
            {
                JsonObject mediaFolderJson = new JsonObject();
                var mediaFiles = await folder.GetFilesAsync();
                if (mediaFiles != null && mediaFiles.Count != 0)
                {
                    count = MapMediaFiles(archive, mediaAnki, count, mediaFolderJson, mediaFiles);
                    mediaAnkiU[folder.Name] = mediaFolderJson;
                }
            }
            return count;
        }

        private async Task MapAndAddAllStaticMediafiles(ZipArchive archive, JsonObject mediaAnki, JsonObject mediaAnkiU, int count)
        {
            JsonObject mediaFolderJson = new JsonObject();
            var mediaFiles = await sourceCol.Media.MediaFolder.GetFilesAsync();
            if (mediaFiles != null && mediaFiles.Count != 0)
            {
                count = MapMediaFiles(archive, mediaAnki, count, mediaFolderJson, mediaFiles);
                mediaAnkiU[MEDIA_BASE_FOLDER] = mediaFolderJson;
            }
        }

        private static int MapMediaFiles(ZipArchive archive, JsonObject mediaAnki, int count, JsonObject mediaFolderJson, IReadOnlyList<StorageFile> mediaFiles)
        {
            foreach (var f in mediaFiles)
            {
                string index = count.ToString();
                archive.CreateEntryFromFile(f.Path, index);
                mediaAnki[index] = JsonValue.CreateStringValue(f.Name);
                mediaFolderJson[index] = JsonValue.CreateStringValue(f.Name);
                count++;
            }

            return count;
        }

        private async Task<KeyValuePair<JsonObject, JsonObject>> ExportFiltered(StorageFolder folder, ZipArchive archive, string fileName)
        {
            // export into the anki2 file
            string colfile = fileName.Replace(".apkg", ".anki2");
            await base.ExportInto(folder, colfile);

            string sourcePath = folder.Path + "\\" + colfile;
            archive.CreateEntryFromFile(sourcePath, "collection.anki2");

            // and media
            prepareMedia();

            JsonObject mediaAnki = new JsonObject();
            JsonObject mediaAnkiU = new JsonObject();
            StorageFolder sourMediaFolder = sourceCol.Media.MediaFolder;

            if (sourMediaFolder != null && includeMedia)
            {
                var folders = await GetDeckMediaFolders(deckId, sourMediaFolder);
                int count = await MapAndAddMediafiles(archive, mediaAnki, mediaAnkiU, folders);
                await MapAndAddFilteredStaticMediafiles(archive, mediaAnki, mediaAnkiU, count);
            }

            return new KeyValuePair<JsonObject, JsonObject>(mediaAnki, mediaAnkiU);
        }

        private async Task MapAndAddFilteredStaticMediafiles(ZipArchive archive, JsonObject mediaAnki, JsonObject mediaAnkiU, int count)
        {
            JsonObject mediaFolderJson = new JsonObject();
            var staticMediaFiles = (await sourceCol.Media.MediaFolder.GetFilesAsync()).ToList();
            if (staticMediaFiles.Count == 0 || mediaFiles.Count == 0)
                return;

            for(int i = 0; i < staticMediaFiles.Count;)
            {
                if(mediaFiles.Contains(staticMediaFiles[i].Name))                
                    i++;
                else                
                    staticMediaFiles.RemoveAt(i);                
            }

            if (staticMediaFiles.Count != 0)
            {
                count = MapMediaFiles(archive, mediaAnki, count, mediaFolderJson, staticMediaFiles);
                mediaAnkiU[MEDIA_BASE_FOLDER] = mediaFolderJson;
            }
        }

        private async Task<IReadOnlyList<StorageFolder>> GetDeckMediaFolders(long? deckId, StorageFolder sourMediaFolder)
        {            
            if (deckId != null)
            {
                List<StorageFolder> folders = new List<StorageFolder>();
                var deckIdFolder = await sourMediaFolder.TryGetItemAsync(deckId.ToString()) as StorageFolder;
                if (deckIdFolder != null)
                    folders.Add(deckIdFolder);

                var children = sourceCol.Deck.Children((long)deckId);
                foreach (var deck in children)
                {
                    deckIdFolder = await sourMediaFolder.TryGetItemAsync(deck.Value.ToString()) as StorageFolder;
                    if (deckIdFolder != null)
                        folders.Add(deckIdFolder);
                }
                return folders;
            }
            else
            {
                return await sourMediaFolder.GetFoldersAsync();
            }            
        }

        protected void prepareMedia()
        {
            // chance to move each file in self.mediaFiles into place before media
            // is zipped up
        }

    }
}
