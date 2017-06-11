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
using System.Diagnostics;
using Windows.Storage;
using Windows.Data.Json;
using System.Text.RegularExpressions;
using Shared.ViewModels;

namespace Shared.AnkiCore
{
    public class Storage
    {
        public static StorageFolder AppLocalFolder { get { return ApplicationData.Current.LocalFolder; } }

        /// <summary>
        /// Open a new or existing collection. Path must be unicode
        /// </summary>
        /// <param name="relativePath"></param>
        /// <param name="server"></param>
        /// <param name="log"></param>
        /// <returns></returns>
        public async static Task<Collection> OpenCollection(StorageFolder folder, string relativePath, bool server = false, bool log = false)
        {
            DB collectionDatabase = null;
            try
            {
                StorageFile file = await folder.TryGetItemAsync(relativePath) as StorageFile;
                bool create = file == null;
                collectionDatabase = new DB(folder.Path + "\\" + relativePath);               
                Collection col = new Collection(collectionDatabase, relativePath, server, log, folder);              
                return col;
            }
            catch(Exception)
            {
                if(collectionDatabase != null)
                    collectionDatabase.Close();                
                return null;                
            }
        }
    }
}
