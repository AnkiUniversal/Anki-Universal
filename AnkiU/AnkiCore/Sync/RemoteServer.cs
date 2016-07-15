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
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;
using System.IO;

namespace AnkiU.AnkiCore.Sync
{
    public class RemoteServer : HttpSyncer
    {
        public RemoteServer(string hkey) : base(hkey) { }

        public async override Task<JsonObject> ApplyChanges(JsonObject kw)
        {
            return await Run("applyChanges", kw);
        }

        public async override Task ApplyChunk(JsonObject sech)
        {
            await Run("applyChunk", sech);
        }

        public async override Task<JsonObject> Chunk(JsonObject kw = null)
        {
            JsonObject co = new JsonObject();
            return await Run("chunk", co);
        }

        public async override Task<long> Finish()
        {
            try
            {
                using (MemoryStream stream = GetInputStream("{}"))
                {
                    HttpResponseMessage ret = await Request("finish", stream);
                    string s = await ret.Content.ReadAsStringAsync();
                    return long.Parse(s);
                }
            }
            catch (FormatException)
            {
                return 0;
            }
        }

        /// <summary>
        /// Returns hkey or none if user/pw incorrect
        /// </summary>
        /// <param name="user"></param>
        /// <param name="pw"></param>
        /// <returns></returns>
        public async override Task<HttpResponseMessage> HostKey(string user, string pw)
        {
            postVars = new Dictionary<string, object>();
            JsonObject jo = new JsonObject();
            jo.Add("u", JsonValue.CreateStringValue(user));
            jo.Add("p", JsonValue.CreateStringValue(pw));
            using (MemoryStream stream = GetInputStream(Utils.JsonToString(jo)))
            {
                return await Request("hostKey", stream);
            }
        }

        public async override Task<HttpResponseMessage> Meta()
        {
            postVars = new Dictionary<string, object>();
            postVars.Add("k", hKey);
            postVars.Add("s", sKey);
            JsonObject jo = new JsonObject();
            jo.Add("v", JsonValue.CreateNumberValue(Syncing.VERSION));
            jo.Add("cv", JsonValue.CreateStringValue(
                         String.Format(Media.locale, 
                         "ankiU,{0},{1}", Utils.APP_VERSION, Utils.platDesc())));
            using (MemoryStream stream = GetInputStream(Utils.JsonToString(jo)))
            {
                return await Request("meta", stream);
            }
        }

        public async override Task<JsonObject> SanityCheck2(JsonObject client)
        {
            return await Run("sanityCheck2", client);
        }

        public async override Task<JsonObject> Start(JsonObject data)
        {
            return await Run("start", data);
        }

        private async Task<JsonObject> Run(string cmd, JsonObject data)
        {
            using (MemoryStream stream = GetInputStream(Utils.JsonToString(data)))
            {
                HttpResponseMessage ret = await Request(cmd, stream);
                string s = await ret.Content.ReadAsStringAsync();
                if (!s.Equals("null", StringComparison.OrdinalIgnoreCase) && s.Length != 0)
                {
                    return JsonObject.Parse(s);
                }
                else
                {
                    return new JsonObject();
                }
            }
        }
    }
}
