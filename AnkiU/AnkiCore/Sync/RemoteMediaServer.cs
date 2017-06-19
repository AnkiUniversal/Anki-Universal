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
using Windows.Web.Http;
using System.IO;
using System.IO.Compression;

namespace AnkiU.AnkiCore.Sync
{    
    public class RemoteMediaServer : HttpSyncer
    {
        private Collection collection;

        public RemoteMediaServer(Collection collection, string hkey) : base(hkey)
        {
            this.collection = collection;
        }

        public override string SyncURL()
        {
            //TODO: User custom sync server:
            //Get user preferences
            //If verify useCustomSyncServer sync preference is true
            //  return path + "sync/"
            //else
            // Usual case
            return Syncing.MEDIA_BASE;
        }

        public async Task<JsonArray> MediaChanges(long lastUsn)
        {
            postVars = new Dictionary<string, object>();
            postVars["sk"] = JsonValue.CreateStringValue(sKey);
            JsonObject json = new JsonObject();
            json.Add("lastUsn", JsonValue.CreateNumberValue(lastUsn));
            using (MemoryStream stream = GetInputStream(Utils.JsonToString(json)))
            {
                HttpResponseMessage resp = await Request("mediaChanges", stream);
                JsonObject jresp = JsonObject.Parse(await resp.Content.ReadAsStringAsync());
                return DataOnly(jresp, new JsonArray());
            }
        }

        /// <summary>
        /// This method returns a ZipFile with the OPEN_DELETE flag, 
        /// ensuring that the file on disk will be automatically deleted when the stream is 
        /// </summary>
        /// <param name="top"></param>
        /// <returns></returns>
        public async Task<ZipArchive> DownloadFiles(List<string> top)
        {
            JsonObject json = new JsonObject();
            json.Add("files", JsonArray.Parse(String.Join(" ", top)));
            using (MemoryStream stream = GetInputStream(Utils.JsonToString(json)))
            {
                HttpResponseMessage resp;
                resp = await Request("downloadFiles", stream);
                String zipPath = collection.RelativePath.ReplaceFirst("collection.anki2", "tmpSyncFromServer.zip");
                // retrieve contents and save to file on disk:
                WriteToFile((await resp.Content.ReadAsInputStreamAsync()).AsStreamForRead(), zipPath);
                return new ZipArchive(new FileStream(zipPath, FileMode.Open), ZipArchiveMode.Update);
            }
        }

        public async Task<JsonArray> UploadChanges(Windows.Storage.StorageFile zip)
        {
            using (FileStream file = new FileStream(zip.Path, FileMode.Open, FileAccess.Read))
            {
                // no compression, as we compress the zip file instead
                HttpResponseMessage resp = await Request("uploadChanges", file, 0);
                JsonObject jresp = JsonObject.Parse(await resp.Content.ReadAsStringAsync());
                return DataOnly(jresp, new JsonArray());
            }
        }

        public async Task<string> MediaSanity(int lcnt)
        {
            JsonObject json = new JsonObject();
            json.Add("local", JsonValue.CreateNumberValue(lcnt));
            using (MemoryStream stream = GetInputStream(Utils.JsonToString(json)))
            {

                HttpResponseMessage resp = await Request("mediaSanity", stream);
                JsonObject jresp = JsonObject.Parse(await resp.Content.ReadAsStringAsync());
                return DataOnly(jresp, "");
            }
        }

        public async Task<JsonObject> Begin()
        {
            postVars = new Dictionary<string, object>();
            postVars["k"] = hKey;
            postVars["v"] = String.Format(Media.locale, "ankiU,{0},{1}", Utils.APP_VERSION, Utils.GetPlatDesc());

            using (MemoryStream stream = GetInputStream(Utils.JsonToString(new JsonObject())))
            {
                HttpResponseMessage resp = await Request("begin", stream);
                JsonObject jresp = JsonObject.Parse(await resp.Content.ReadAsStringAsync());
                JsonObject ret = DataOnly(jresp, new JsonObject());
                sKey = ret.GetNamedString("sk");
                return ret;
            }
        }

        /// <summary>
        /// Returns the "data" element from the JSON response from the server, or throws an exception if there is a value in
        /// the "err" element.
        /// <p>
        /// The python counterpart to this method is flexible with type coercion; the type of object returned is decided by
        /// the content of the "data" element, and there are several such types in the various server responses. 
        /// Java ver requires us to specifically choose a type to convert to, so we need an additional parameter (returnType) to
        /// specify the type we expect.
        /// In C# we can use dynamic (with a hit in performance) however the JsonObject still requires us to use the 
        /// right get method for each type.
        /// So just use OVERLOAD instead
        /// </summary>
        /// <param name="resp"></param>
        /// <param name="notUsed">Just used to choose the right overload method. Not used in function.</param>
        /// <returns></returns>
        private string DataOnly(JsonObject resp, string notUsed)
        {
            if (!String.IsNullOrEmpty(resp.GetNamedString("err", null)))
            {
                string err = resp.GetNamedString("err");
                collection.Log(args: ("error returned: " + err));
                throw new MediaSyncException("SyncError:" + err);
            }

            return resp.GetNamedString("data");
        }

        private JsonObject DataOnly(JsonObject resp, JsonObject notUsed)
        {
            if (!String.IsNullOrEmpty(resp.GetNamedString("err", null)))
            {
                string err = resp.GetNamedString("err");
                collection.Log(args: ("error returned: " + err));
                throw new MediaSyncException("SyncError:" + err);
            }

            return resp.GetNamedObject("data");
        }

        private JsonArray DataOnly(JsonObject resp, JsonArray notUsed)
        {
            if (!String.IsNullOrEmpty(resp.GetNamedString("err", null)))
            {
                string err = resp.GetNamedString("err");
                collection.Log(args: ("error returned: " + err));
                throw new MediaSyncException("SyncError:" + err);
            }

            return resp.GetNamedArray("data");
        }
    }

    public class MediaSyncException : Exception
    {
        public MediaSyncException() : base() { }
        public MediaSyncException(string message) : base(message) { }
    }
}
