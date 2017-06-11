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
using AnkiU.AnkiCore.Sync;
using Windows.UI.Xaml;

namespace AnkiU.AnkiCore.Importer
{

    public delegate void AnkiPackageImporterFinishedEventHandler(AnkiImportFinishCode code, string message);
    public class AnkiPackageImporter : Anki2Importer
    {
        private ZipArchive archive;
        private Dictionary<string, string> numToName;
        private StorageFile packageFile;
        private StorageFolder tempDir;        

        private const string tempDirName = "tmpDir";
        private Dictionary<long, Dictionary<string, string>> mediaToDeckIdMap;

        private long missingMediaCount;
        public long MissingMediaCount { get { return missingMediaCount; } }

        public event ImporterStateHandler PackageImportStateChangeEvent;
        public event AnkiPackageImporterFinishedEventHandler AnkiPackageImporterFinishedEvent;

        public AnkiPackageImporter(Collection collection, StorageFile packageFile)
            : base(collection, null, null)
        {
            this.packageFile = packageFile;
            base.ImporterStateChangeEvent += ImporterStateChangeEventHandler;
        }

        private void ImporterStateChangeEventHandler(string message)
        {
            PackageImportStateChangeEvent?.Invoke(message);
        }

        public async override Task Run()
        {                        
            tempDir = await destCol.Folder.CreateFolderAsync(tempDirName, CreationCollisionOption.ReplaceExisting);
            
            string collectionName = "collection.anki2";
            string mapFileName = "media";
            string mapAnkiU = "mediaAnkiU";
            AnkiImportFinishCode code = AnkiImportFinishCode.UnableToUnzip;
            try
            {
                packageFile = await packageFile.CopyAsync(tempDir, packageFile.Name, NameCollisionOption.ReplaceExisting);
                using(var fileStream = (await packageFile.OpenReadAsync()).AsStream())
                using (archive = new ZipArchive(fileStream, ZipArchiveMode.Read))
                {
                    // We extract the zip contents into a temporary directory and do a little more
                    // validation than the python client to ensure the extracted collection is an apkg.
                    try
                    {
                        PackageImportStateChangeEvent?.Invoke("Extracting package...");
                        Utils.UnZipNotFolderEntries(archive, tempDir.Path, new string[] { collectionName, mapFileName, mapAnkiU }, null);

                        //Point the sourcefolder and relative path to extracted files
                        sourceFolder = tempDir;
                        relativePathToFile = collectionName;
                    }
                    catch (IOException e)
                    {
                        code = AnkiImportFinishCode.UnableToUnzip;                        
                        ThrowDebugException(AnkiImportFinishCode.UnableToUnzip, e.Message);
                        return;
                    }
                    string colpath = "collection.anki2";
                    StorageFile colFile = await tempDir.TryGetItemAsync("collection.anki2") as StorageFile;
                    if (colFile == null)
                    {
                        code = AnkiImportFinishCode.NotFoundCollection;
                        ThrowDebugException(AnkiImportFinishCode.NotFoundCollection);
                        return;
                    }
                    // we need the media dict in advance, and we'll need a map of fname ->
                    // number to use during the import
                    PackageImportStateChangeEvent?.Invoke("Create mapping file...");
                    numToName = new Dictionary<string, string>();
                    using (Collection col = await Storage.OpenOrCreateCollection(tempDir, colpath))
                    {
                        try
                        {
                            GetAnkiMediaMapping(numToName);
                            await GetAnkiUMediaMappingIfNeeded();
                        }
                        catch (FileNotFoundException)
                        {
                            code = AnkiImportFinishCode.NotFoundMediaFile;
                            ThrowDebugException(AnkiImportFinishCode.NotFoundMediaFile);
                            return;
                        }
                        catch (IOException)
                        {
                            code = AnkiImportFinishCode.MediaFileIsCorrupted;
                            ThrowDebugException(AnkiImportFinishCode.MediaFileIsCorrupted);
                            return;
                        }
                        catch(Exception)
                        {
                            code = AnkiImportFinishCode.UnknownExpception;
                            return;
                        }
                    }
                    try
                    {
                        // run anki2 importer
                        await base.Run();
                        //WARNING: AnkiU does not support static media and Latex 
                        //import static media
                        //PackageImportStateChangeEvent?.Invoke("Importing static media...");
                        //foreach (var entry in numToName)
                        //{
                        //    string file = entry.Value;
                        //    string c = entry.Key;
                        //    if (!file.StartsWith("_") && !file.StartsWith("latex-"))
                        //    {
                        //        continue;
                        //    }
                        //    StorageFile path = await destCol.Media.MediaFolder
                        //                             .TryGetItemAsync(file) as StorageFile;
                        //    if (path == null)
                        //    {
                        //        try
                        //        {
                        //            Utils.UnZipNotFolderEntries(archive, destCol.Media.MediaFolder.Path, new string[] { c }, numToName);
                        //        }
                        //        catch (IOException)
                        //        {
                        //            Debug.WriteLine("Failed to extract static media file. Ignoring.");
                        //        }
                        //    }
                        //}
                        destCol.Database.Commit();
                        code = AnkiImportFinishCode.Success;

                        //Only in AnkiU we perform this step to move all mediafiles into DeckIdFolder 
                        //when importing the whole collection                  
                        PackageImportStateChangeEvent?.Invoke("Importing media...");
                        await ExtractSourceMediaFileToAllDeckIdFolderAsync();                        
                    }
                    catch(AnkiImportException e)
                    {
                        code = e.Error;
                    }
                    catch (Exception)
                    {
                        code = AnkiImportFinishCode.UnknownExpception;                      
                    }
                }
            }
            finally
            {
                if (tempDir == null)
                {
                    // Clean up our temporary files
                    tempDir = await destCol.Folder.TryGetItemAsync(tempDirName) as StorageFolder;
                }

                if (tempDir != null)
                {
                    if (sourceCol != null)
                    {
                        sourceCol.Close(false);
                        sourceCol = null;
                    }
                    sourceFolder = null;
                    await tempDir.DeleteAsync();
                    tempDir = null;
                }                
                AnkiPackageImporterFinishedEvent?.Invoke(code, ImportedNoteId.Count.ToString());
            }
        }

        private void GetAnkiMediaMapping(Dictionary<string, string> numToName)
        {
            using (FileStream mediaMapFile = new FileStream(tempDir.Path + "\\" + "media", FileMode.Open))
            {
                //WARNING: Java ver needs opposite mapping since their extraction method requires it.
                JsonObject json = JsonObject.Parse(HttpSyncer.Stream2String(mediaMapFile));
                string name;
                string num;
                foreach (var jr in json)
                {
                    num = jr.Key;
                    name = jr.Value.GetString();
                    numToName.Add(num, name);
                }
            }
        }

        private async Task GetAnkiUMediaMappingIfNeeded()
        {
            StorageFile mediaAnkiU = await tempDir.TryGetItemAsync("mediaAnkiU") as StorageFile;
            mediaToDeckIdMap = null;
            if (mediaAnkiU != null)
            {
                mediaToDeckIdMap = new Dictionary<long, Dictionary<string, string>>();
                using (FileStream mediaMapFile = new FileStream(mediaAnkiU.Path, FileMode.Open))
                {
                    JsonObject json = JsonObject.Parse(HttpSyncer.Stream2String(mediaMapFile));
                    foreach (var jr in json)
                    {
                        long deckID = long.Parse(jr.Key);

                        Dictionary<string, string> dict = new Dictionary<string, string>();
                        JsonObject listMediaOfDeck = jr.Value.GetObject();
                        foreach (var j in listMediaOfDeck)
                        {
                            dict.Add(j.Value.GetString(), j.Key);
                        }
                        mediaToDeckIdMap[deckID] = dict;
                    }
                }
            }
        }

        [Conditional("DEBUG")]
        private void ThrowDebugException(AnkiImportFinishCode code)
        {
            throw new AnkiImportException(code);
        }

        [Conditional("DEBUG")]
        private void ThrowDebugException(AnkiImportFinishCode code, string message)
        {
            throw new AnkiImportException(code, message);
        }

        #region


        private async Task ExtractSourceMediaFileToAllDeckIdFolderAsync()
        {
            string snoteId = Utils.Ids2str(ImportedNoteId);
            var arrayNote = destCol.Database.QueryColumn<NoteTable>("select id, mid, flds from notes where id in" + snoteId);
            var arrayCard = destCol.Database.QueryColumn<CardTable>("select nid, did from cards where nid in " + snoteId);

            var array = destCol.Media.MapNoteToDeckId(arrayNote, arrayCard);
            var mediaNameMapDeckId = destCol.Media.MapMediaNameToDeckId(array);
            var deckIdFolderMap = await destCol.Media.MapDeckIdToDeckIdFolder();

            if (mediaToDeckIdMap != null)
                ExtractMediafliesToItsDeckFolderWithAnkiUMappingFile(mediaNameMapDeckId, deckIdFolderMap);
            else
                ExtractMediafliesToItsDeckFolder(mediaNameMapDeckId, deckIdFolderMap);
        }

        private void ExtractMediafliesToItsDeckFolderWithAnkiUMappingFile(Dictionary<KeyValuePair<string, long>, bool> mediaMapToFolder, 
                                                                            Dictionary<long, StorageFolder> deckIdFolderMap)
        {
            string fileName;
            long deckIdInDest;
            long deckIdInSource;
            string indexInArchive;
            StorageFolder folder;
            try
            {
                destCol.Media.Database.RunInTransaction(() =>
                {
                    foreach (var media in mediaMapToFolder)
                    {
                        fileName = media.Key.Key;
                        deckIdInDest = media.Key.Value;
                        deckIdInSource = ImportedDeckIdMap[deckIdInDest];

                        bool isHave = mediaToDeckIdMap[deckIdInSource].TryGetValue(fileName, out indexInArchive);

                        if (isHave)
                        {
                            folder = deckIdFolderMap[deckIdInDest];

                            archive.GetEntry(indexInArchive)
                                .ExtractToFile(folder.Path + "\\" + fileName, true);

                            destCol.Media.MarkFileAddIntoDatabase(fileName, deckIdInDest);
                        }
                        else
                        {
                            missingMediaCount++;
                        }
                    }
                });
            }
            catch(KeyNotFoundException)
            {
                throw new AnkiImportException(AnkiImportFinishCode.MediaFileIsCorrupted);
            }
        }

        private void ExtractMediafliesToItsDeckFolder(Dictionary<KeyValuePair<string, long>, bool> mediaMapToFolder,
                                                        Dictionary<long, StorageFolder> deckIdFolderMap)
        {        
            try
            {
                destCol.Media.Database.RunInTransaction(() =>
                {
                    var mediaToDeckId = mediaMapToFolder.Keys.ToList();
                    foreach (var fname in numToName)
                    {
                        var deckIdList = mediaToDeckId.FindAll((x) => (x.Key == fname.Value));
                        foreach (var did in deckIdList)
                        {
                            archive.GetEntry(fname.Key)
                               .ExtractToFile(deckIdFolderMap[did.Value].Path + "\\" + fname.Value, true);

                            destCol.Media.MarkFileAddIntoDatabase(fname.Value, did.Value);
                        }
                    }
                });
            }
            catch
            {
                throw new AnkiImportException(AnkiImportFinishCode.MediaFileIsCorrupted);
            }
        }
        #endregion       

    }

}
