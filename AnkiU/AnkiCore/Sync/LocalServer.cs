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
using Windows.Web.Http;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace AnkiU.AnkiCore.Sync
{
    /// <summary>
    /// Local syncing for unit tests
    /// </summary>
    public class LocalServer : Syncer, ISyncer
    {
        public LocalServer(Collection col) : base(col)
        {
        }

        public override JsonObject ApplyChanges(JsonObject objectChanges)
        {
            // serialize/deserialize payload, 
            // so we don't end up sharing objects between cols
            string str = Utils.JsonToString(objectChanges);
            JsonObject rightChanges = JsonObject.Parse(str);
            str = Utils.JsonToString(base.ApplyChanges(rightChanges));
            return JsonObject.Parse(str);
        }

        Task<JsonObject> ISyncer.ApplyChanges(JsonObject data)
        {
            //WARNING: java ver wrap all changes in jsonObject
            //with key "changes" while python ver does not
            JsonObject changes = data.GetNamedObject("changes");

            // serialize/deserialize payload, 
            // so we don't end up sharing objects between cols
            string str = Utils.JsonToString(changes);
            JsonObject json = JsonObject.Parse(str);

            Task<JsonObject> task = Task<JsonObject>.Factory.StartNew(() =>
            {
               str = Utils.JsonToString(base.ApplyChanges(json));
               return JsonObject.Parse(str);
            });
            return task;
        }

        Task ISyncer.ApplyChunk(JsonObject sech)
        {
            //WARNING: java ver wrap all changes in jsonObject
            //with key "chunk" while python ver does not
            JsonObject chunk = sech.GetNamedObject("chunk");

            Task task = Task.Factory.StartNew(() =>
            {
                //WARNING: java ver wrap all changes i jsonObject
                //with key "changes" while python ver does not
                base.ApplyChunk(chunk);
            });
            return task;
        }

         Task<JsonObject> ISyncer.Chunk(JsonObject kw)
        {
            Task<JsonObject> task = Task<JsonObject>.Factory.StartNew(() =>
            {
                return base.Chunk();
            });
            return task;
        }

        Task<long> ISyncer.Finish()
        {
            Task<long> task = Task<long>.Factory.StartNew(() =>
            {
                return base.Finish();
            });
            return task;
        }

        Task<HttpResponseMessage> ISyncer.Meta()
        {
            Task<HttpResponseMessage> task = Task<HttpResponseMessage>.Factory.StartNew(() =>
            {
                HttpResponseMessage httpResponse = new HttpResponseMessage();
                JsonObject json = base.Meta();
                httpResponse.StatusCode = HttpStatusCode.Ok;
                httpResponse.Content = new HttpStringContent(Utils.JsonToString(json));                
                return httpResponse;
            });
            return task;
        }

        Task<JsonObject> ISyncer.SanityCheck2(JsonObject client)
        {
            Task<JsonObject> task = Task<JsonObject>.Factory.StartNew(() =>
            {
                JsonObject result = new JsonObject();
                //We will always return ok here 
                result.Add("status", JsonValue.CreateStringValue("ok"));
                return result;
            });
            return task;
        }

        Task<JsonObject> ISyncer.Start(JsonObject data)
        {
            Task<JsonObject> task = Task<JsonObject>.Factory.StartNew(() =>
            {
                return base.Start((int)data.GetNamedNumber("minUsn"), 
                                       data.GetNamedBoolean("lnewer"), 
                                       data.GetNamedObject("graves"));
            });
            return task;
        }
    }
}
