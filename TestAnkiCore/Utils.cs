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
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
using AnkiU.AnkiCore;
using Windows.Storage;
using System.Threading.Tasks;
using SQLite.Net;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Windows.Storage.Pickers;
using System.Text;

namespace TestAnkiCore
{
    static class Utils
    {
        public static readonly StorageFolder localFolder = ApplicationData.Current.LocalFolder;
        private static List<StorageFile> files;
        public const string collectionName = "test.anki2";

        public async static Task<Collection> GetEmptyCollection(StorageFolder folder, bool server = false)
        {
            return await Storage.OpenOrCreateCollection(folder, collectionName, server: server);
        }

        public async static Task<Collection> GetExistCollection(StorageFolder folder)
        {
            files = new List<StorageFile>();
            //Copy collection database file to test folder 
            string assetFileName = @"ms-appx:///TestAssets/collection.anki2";
            StorageFile assetFile = await StorageFile.GetFileFromApplicationUriAsync(
                                        new Uri(assetFileName));
            files.Add(await assetFile.CopyAsync(folder, collectionName));

            //Copy media database file to test folder 
            string mediaName = "test.media.au.db2";
            assetFileName = @"ms-appx:///TestAssets/collection.media.db2";
            assetFile = await StorageFile.GetFileFromApplicationUriAsync(
                                        new Uri(assetFileName));
            files.Add(await assetFile.CopyAsync(folder, mediaName));

            Collection col = await Storage.OpenOrCreateCollection(folder, collectionName);
            return col;
        }

        public async static Task DeleteTestFile()
        {
            if (files != null)
                foreach (StorageFile f in files)
                {
                    await f.DeleteAsync();
                }
            files = null;
        }

        public static void WriteToFile(string pathToFile, string text)
        {
            using (FileStream file = new FileStream(pathToFile, FileMode.Open))
            {
                byte[] data = Encoding.UTF8.GetBytes(text);
                file.Write(data, 0, data.Length);
            }
        }

        public static bool CompareLists<T>(List<T> first, List<T> second)
        {
            if (first.Count != second.Count)
                return false;

            for (int i = 0; i < first.Count; i++)
            {
                if (!first[i].Equals(second[i]))
                    return false;
            }

            return true;
        }

        public static bool CompareArray<T>(T[] first, T[] second)
        {
            if (first.Length != second.Length)
                return false;

            for (int i = 0; i < first.Length; i++)
            {
                if (!first[i].Equals(second[i]))
                    return false;
            }

            return true;
        }

    }
}
