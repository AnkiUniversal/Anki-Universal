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
                        mediaPair = await exportVerbatim(archive);
                        media = mediaPair.Key;
                        mediaAnkiU = mediaPair.Value;
                    }
                    else
                    {
                        // otherwise, filter
                        media = await exportFiltered(tempExportPackageFolder, archive, fileName, (long)deckId);
                    }
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
                ExportFinishedEvent("Successed");
            }
            catch(Exception e)
            {
                ExportFinishedEvent(e.Message);
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

        private async Task<KeyValuePair<JsonObject, JsonObject>> exportVerbatim(ZipArchive archive)
        {
            // close our deck & write it into the zip file, and reopen
            count = sourceCol.CardCount();
            string sourcePath = sourceCol.Folder.Path + "\\" + sourceCol.RelativePath;
            sourceCol.Close();
            try
            {
                archive.CreateEntryFromFile(sourcePath, "collection.anki2");
                // copy all media
                JsonObject media = new JsonObject();
                JsonObject mediaAnkiU = new JsonObject();
                if (!includeMedia)
                {
                    return new KeyValuePair<JsonObject,JsonObject>(media, mediaAnkiU);
                }

                if (sourceCol.Media.MediaFolder != null)
                {
                    int c = 0;
                    
                    foreach (var folder in await sourceCol.Media.MediaFolder.GetFoldersAsync())
                    {
                        JsonObject mediaFolderJson = new JsonObject();
                        var mediaFiles = await folder.GetFilesAsync();
                        if (mediaFiles != null && mediaFiles.Count != 0)
                        {
                            foreach (var f in mediaFiles)
                            {
                                string index = c.ToString();                                
                                archive.CreateEntryFromFile(f.Path, index);                                
                                media[index] = JsonValue.CreateStringValue(f.Name);
                                mediaFolderJson[index] = JsonValue.CreateStringValue(f.Name);
                                c++;
                            }
                            mediaAnkiU[folder.Name] = mediaFolderJson;
                        }
                    }
                }
                return new KeyValuePair<JsonObject, JsonObject>(media, mediaAnkiU);
            }
            catch (Exception)
            {
                throw new Exception("Unknown error.");
            }
            finally
            {
                sourceCol.ReOpen();
            }                        
        }

        private async Task<JsonObject> exportFiltered(StorageFolder folder, ZipArchive archive, string fileName, long deckId)
        {
            // export into the anki2 file
            string colfile = fileName.Replace(".apkg", ".anki2");
            await base.ExportInto(folder, colfile);

            string sourcePath = folder.Path + "\\" + colfile;
            archive.CreateEntryFromFile(sourcePath, "collection.anki2");

            // and media
            prepareMedia();
            JsonObject media = new JsonObject();
            StorageFolder sourMediaFolder = sourceCol.Media.MediaFolder;
            if (sourMediaFolder != null)
            {
                var deckIdFolder = await sourMediaFolder.TryGetItemAsync(deckId.ToString()) as StorageFolder;
                if (deckIdFolder != null)
                {
                    int c = 0;                    
                    foreach (string f in mediaFiles)
                    {
                        StorageFile sf = await deckIdFolder.TryGetItemAsync(f) as StorageFile;
                        if (sf != null)
                        {
                            archive.CreateEntryFromFile(sf.Path, c.ToString());
                            media[c.ToString()] = JsonValue.CreateStringValue(f);
                            c++;
                        }
                    }
                }                
            }
            return media;
        }

        protected void prepareMedia()
        {
            // chance to move each file in self.mediaFiles into place before media
            // is zipped up
        }

    }
}
