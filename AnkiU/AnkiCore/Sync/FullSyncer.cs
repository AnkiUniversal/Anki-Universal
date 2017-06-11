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
using Windows.Web.Http;
using System.IO;
using Windows.Storage;

namespace AnkiU.AnkiCore.Sync
{
    public class FullSyncer : HttpSyncer
    {
        Collection collection;

        public FullSyncer(Collection collection, string hkey) : base(hkey)
        {
            postVars = new Dictionary<string, object>();
            postVars.Add("k", hkey);
            postVars.Add("v", String.Format("anki,{0},{1}", Utils.APP_VERSION, Utils.platDesc()));
            this.collection = collection;
        }

        public override string SyncURL()
        {
            // Allow user to specify custom sync server
            //TODO: User custom sync server:
            //Get user preferences
            //If verify valid custom sync preference is true
            //  return path + "sync/"
            //else
            // Usual case
            return Syncing.BASE + "sync/";
        }

        public async override Task<object[]> Download()
        {
            HttpResponseMessage ret = await Request("download");
            if (ret == null)
                return null;

            var content = ret.Content;
            string relativePath;
            if (collection != null)
            {
                // Usual case where collection is non-null
                relativePath = collection.RelativePath;
                collection.Close();
                collection = null;
            }
            else
            {
                // Different with java ver we thrwo exception here
                // instead of trying to access it
                throw new Exception("Can not open collection!");
            }

            string tempRelativePath = relativePath + ".tmp";
            WriteToFile((await content.ReadAsInputStreamAsync()).AsStreamForRead(), tempRelativePath);
            using (FileStream fis = new FileStream(tempRelativePath, FileMode.Open, FileAccess.Read))
            {
                if (Stream2String(fis, 15).Equals("upgradeRequired"))
                {
                    return new object[] { "upgradeRequired" };
                }
            }

            try
            {
                // check the received file is ok
                using (DB tempDb = new DB(collection.Folder.Path + "\\" + tempRelativePath))
                {
                    if (!tempDb.QueryScalar<string>("PRAGMA integrity_check").Equals("ok", StringComparison.OrdinalIgnoreCase))
                        return new object[] { "remoteDbError" };
                }
            }
            catch(SQLite.Net.SQLiteException)
            {
                return new object[] { "remoteDbError" };
            }

            try
            {
                StorageFile newFile = await Storage.AppLocalFolder.GetFileAsync(tempRelativePath);
                await newFile.RenameAsync(relativePath, NameCollisionOption.ReplaceExisting);
                return new object[] { "success" };
            }
            catch
            {
                return new object[] { "overwriteError" };
            }
            
        }

        public override async Task<object[]> Upload()
        {
            // make sure it's ok before we try to uploa            
            if (!collection.Database.QueryScalar<string>("PRAGMA integrity_check")
                    .Equals("ok", StringComparison.OrdinalIgnoreCase))
            {
                return new object[] { "dbError" };
            }
            if (!collection.BasicCheck())
            {
                return new object[] { "dbError" };
            }
            // apply some adjustments, then upload
            collection.BeforeUpload();
            string filePath = collection.RelativePath;
            HttpResponseMessage ret;
            using (FileStream stream = new FileStream(filePath, FileMode.Open))
            {
                ret = await Request("upload", stream);
                if (ret == null)
                {
                    return null;
                }
                HttpStatusCode status = ret.StatusCode;
                if (status != HttpStatusCode.Ok)
                {
                    return new object[] { "error", status, ret.ReasonPhrase };
                }
                else
                {
                    return new object[] { await ret.Content.ReadAsStringAsync() };
                }
            }

        }

    }
}
