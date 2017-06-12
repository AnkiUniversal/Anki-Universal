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

namespace AnkiU.AnkiCore.Sync
{
    public class Syncer
    {
        Collection collection;
        ISyncer server;
        protected long serverModifiedTime;
        protected int maxUsn;
        protected long clientModifiedTime;
        protected int minUsn;
        protected bool isClientNewer;
        protected string syncMsg;

        private LinkedList<string> tablesLeft;

        public string SyncMsg { get { return syncMsg; } }

        public Syncer(Collection col, ISyncer server = null)
        {
            collection = col;
            this.server = server;
        }

        public JsonObject Meta()
        {
            JsonObject j = new JsonObject();
            j.Add("mod", JsonValue.CreateNumberValue(collection.TimeModified));
            j.Add("scm", JsonValue.CreateNumberValue(collection.Scm));
            j.Add("usn", JsonValue.CreateNumberValue(collection.GetUsnForSync));
            j.Add("ts", JsonValue.CreateNumberValue(DateTimeOffset.Now.ToUnixTimeSeconds()));
            j.Add("musn", JsonValue.CreateNumberValue(0));
            j.Add("msg", JsonValue.CreateStringValue(""));
            j.Add("cont", JsonValue.CreateBooleanValue(true));
            return j;
        }

        /// <summary>
        /// Returns 'noChanges', 'fullSync', 'success', etc
        /// </summary>
        /// <returns></returns>
        public async Task<string[]> Sync()
        {
            string[] result = null;
            syncMsg = "";
            // if the deck has any pending changes, flush them first and bump mod time
            collection.Save();
            // step 1: login & metadata
            HttpResponseMessage ret = await server.Meta();
            if (ret == null)
            {
                return null;
            }
            HttpStatusCode returntype = ret.StatusCode;
            if (returntype == HttpStatusCode.Forbidden)
            {
                return new string[] { "badAuth" };
            }

            collection.Database.BeginTransaction();
            string transactionSave = collection.Database.SaveTransactionPoint();
            try
            {
                JsonObject serverMeta = JsonObject.Parse(await ret.Content.ReadAsStringAsync());
                collection.Log(args: new object[] { "rmeta", serverMeta });
                syncMsg = serverMeta.GetNamedString("msg");
                if (!serverMeta.GetNamedBoolean("cont"))
                {
                    // Don't add syncMsg; it can be fetched by UI code using the accessor
                    result = new string[] { "serverAbort" };
                    return result;
                }
                else
                {
                    // don't abort, but ui should show messages after sync finishes
                    // and require confirmation if it's non-empty
                }
                long serverScm = (long)serverMeta.GetNamedNumber("scm");
                int serverTs = (int)serverMeta.GetNamedNumber("ts");
                serverModifiedTime = (long)serverMeta.GetNamedNumber("mod");
                maxUsn = (int)serverMeta.GetNamedNumber("usn");
                // skip uname
                JsonObject clientMeta = Meta();
                collection.Log(args: new object[] { "lmeta", clientMeta });
                clientModifiedTime = (long)clientMeta.GetNamedNumber("mod");
                minUsn = (int)clientMeta.GetNamedNumber("usn");
                long clientScm = (long)clientMeta.GetNamedNumber("scm");
                int clientTs = (int)clientMeta.GetNamedNumber("ts");

                long diff = Math.Abs(serverTs - clientTs);
                if (diff > 300)
                {
                    collection.Log(args: "clock off");
                    result = new string[] { "clockOff", diff.ToString() };
                    return result;
                }
                if (clientModifiedTime == serverModifiedTime)
                {
                    collection.Log(args: "no changes");
                    result = new string[] { "noChanges" };
                    return result;
                }
                else if (clientScm != serverScm)
                {
                    collection.Log(args: "schema diff");
                    result = new string[] { "fullSync" };
                    return result;
                }
                isClientNewer = clientModifiedTime > serverModifiedTime;
                // step 1.5: check collection is valid
                if (!collection.BasicCheck())
                {
                    collection.Log(args: "basic check");
                    result = new string[] { "basicCheckFailed" };
                    return result;
                }

                // step 2: Deletions
                JsonObject clientRemove = Removed();
                JsonObject o = new JsonObject();
                o["minUsn"] = JsonValue.CreateNumberValue(minUsn);
                o["lnewer"] = JsonValue.CreateBooleanValue(isClientNewer);
                o["graves"] = clientRemove;
                //WARNING: java ver and python different witch each other
                //in how to implement server.Start
                JsonObject severRemove = await server.Start(o);
                Remove(severRemove);

                // ... and small objects
                JsonObject clientChanges = Changes();
                JsonObject sch = new JsonObject();
                sch.Add("changes", clientChanges);

                JsonObject severChanges = await server.ApplyChanges(sch);
                MergeChanges(clientChanges, severChanges);

                // step 3: stream large tables from server
                while (true)
                {
                    JsonObject chunk = await server.Chunk();
                    collection.Log(args: new object[] { "server chunk", chunk });
                    ApplyChunk(chunk);
                    if (chunk.GetNamedBoolean("done"))
                    {
                        break;
                    }
                }

                // step 4: stream to server
                while (true)
                {
                    JsonObject json = Chunk();
                    collection.Log(args: new object[] { "client chunk", json });
                    JsonObject chunk = new JsonObject();
                    chunk.Add("chunk", json);
                    await server.ApplyChunk(chunk);
                    if (json.GetNamedBoolean("done"))
                    {
                        break;
                    }
                }
                // step 5: sanity check
                JsonObject clientCheck = SanityCheck();
                JsonObject severCheck = await server.SanityCheck2(clientCheck);
                if (severCheck == null || !severCheck.GetNamedString("status", "bad").Equals("ok"))
                {
                    collection.Log(args: new object[] { "sanity check failed", clientCheck, severCheck });
                    result = new string[] { "sanityCheckError", null };
                    return result;
                }
                // finalize
                long timeModified = await server.Finish();
                if (timeModified == 0)
                {
                    result = new string[] { "finishError" };
                    return result;
                }
                Finish(timeModified);
                result = new string[] { "success" };
            }
            catch(Exception e)
            {
                collection.Database.RollbackTo(transactionSave);
                throw new Exception("Sync failed!", e);
            }
            finally
            {
                collection.Database.Commit();                
            }

            return result;
        }

        public JsonObject Changes()
        {
            JsonObject o = new JsonObject();
            o["models"] = GetModels();
            o["decks"] = GetDecks();
            o["tags"] = GetTags();
            if (isClientNewer)
            {
                o["conf"] = GetConf();
                o["crt"] = JsonValue.CreateNumberValue(collection.Crt);
            }
            return o;
        }

        private JsonArray GetModels()
        {
            JsonArray result = new JsonArray();
            if (collection.IsServer)
            {
                foreach (JsonObject m in collection.Models.All())
                {
                    if (m.GetNamedNumber("usn") >= minUsn)
                    {
                        result.Add(m);
                    }
                }
            }
            else
            {
                foreach (JsonObject m in collection.Models.All())
                {
                    if (m.GetNamedNumber("usn") == -1)
                    {
                        m["usn"] = JsonValue.CreateNumberValue(maxUsn);
                        result.Add(m);
                    }
                }
                collection.Models.Save();
            }
            return result;
        }

        private JsonArray GetDecks()
        {
            JsonArray result = new JsonArray();
            if (collection.IsServer)
            {
                JsonArray decks = new JsonArray();
                foreach (JsonObject g in collection.Deck.All())
                {
                    if (g.GetNamedNumber("usn") >= minUsn)
                    {
                        decks.Add(g);
                    }
                }
                JsonArray dconfs = new JsonArray();
                foreach (JsonObject g in collection.Deck.AllConf())
                {
                    if (g.GetNamedNumber("usn") >= minUsn)
                    {
                        dconfs.Add(g);
                    }
                }
                result.Add(decks);
                result.Add(dconfs);
            }
            else
            {
                JsonArray decks = new JsonArray();
                foreach (JsonObject g in collection.Deck.All())
                {
                    if (g.GetNamedNumber("usn") == -1)
                    {
                        g["usn"] = JsonValue.CreateNumberValue(maxUsn);
                        decks.Add(g);
                    }
                }
                JsonArray dconfs = new JsonArray();
                foreach (JsonObject g in collection.Deck.AllConf())
                {
                    if (g.GetNamedNumber("usn") == -1)
                    {
                        g["usn"] = JsonValue.CreateNumberValue(maxUsn);
                        dconfs.Add(g);
                    }
                }
                collection.Deck.Save();
                result.Add(decks);
                result.Add(dconfs);
            }
            return result;
        }

        private JsonArray GetTags()
        {
            JsonArray result = new JsonArray();
            JsonObject tags = collection.Tags.GetTags();
            if (collection.IsServer)
            {
                foreach (var t in tags)
                {
                    if (t.Value.GetNumber() >= minUsn)
                    {
                        result.Add(JsonValue.CreateStringValue(t.Key));
                    }
                }
            }
            else
            {
                JsonObject addTags = new JsonObject();
                foreach (var t in tags)
                {
                    if (t.Value.GetNumber() == -1)
                    {
                        addTags.Add(t.Key, JsonValue.CreateNumberValue(maxUsn));
                        result.Add(JsonValue.CreateStringValue(t.Key));
                    }
                }
                //Update tags in collection
                foreach(var t in addTags)
                {
                    tags[t.Key] = t.Value;
                }
                collection.Tags.Save();
            }
            return result;
        }

        private JsonObject GetConf()
        {
            return collection.Conf;
        }

        public virtual JsonObject ApplyChanges(JsonObject objectChanges)
        {
            JsonObject rightChanges = objectChanges;
            JsonObject lchg = Changes();
            // merge our side before returning
            MergeChanges(lchg, rightChanges);
            return lchg;
        }

        public void MergeChanges(JsonObject lchg, JsonObject rchg)
        {
            // then the other objects
            MergeModels(rchg.GetNamedArray("models"));
            MergeDecks(rchg.GetNamedArray("decks"));
            MergeTags(rchg.GetNamedArray("tags"));
            if (rchg.ContainsKey("conf"))
            {
                MergeConf(rchg.GetNamedObject("conf"));
            }
            // this was left out of earlier betas
            if (rchg.ContainsKey("crt"))
            {
                collection.Crt = (long)rchg.GetNamedNumber("crt");
            }
            PrepareToChunk();
        }

        private void MergeModels(JsonArray rchg)
        {
            for (uint i = 0; i < rchg.Count; i++)
            {
                JsonObject r = rchg.GetObjectAt(i);
                JsonObject l;
                l = collection.Models.Get((int)r.GetNamedNumber("id"));
                // if missing locally or server is newer, update
                if (l == null || r.GetNamedNumber("mod") > l.GetNamedNumber("mod"))
                {
                    collection.Models.Update(r);
                }
            }
        }

        private void MergeDecks(JsonArray rchg)
        {
            JsonArray decks = rchg.GetArrayAt(0);
            for (uint i = 0; i < decks.Count; i++)
            {
                JsonObject r = decks.GetObjectAt(i);
                JsonObject l = collection.Deck.Get((int)r.GetNamedNumber("id"), false);
                // if missing locally or server is newer, update
                if (l == null || r.GetNamedNumber("mod") > l.GetNamedNumber("mod"))
                {
                    collection.Deck.Update(r);
                }
            }
            JsonArray confs = rchg.GetArrayAt(1);
            for (uint i = 0; i < confs.Count; i++)
            {
                JsonObject r = confs.GetObjectAt(i);
                JsonObject l = collection.Deck.DeckConf[(int)r.GetNamedNumber("id")];
                // if missing locally or server is newer, update
                if (l == null || r.GetNamedNumber("mod") > l.GetNamedNumber("mod"))
                {
                    collection.Deck.UpdateConf(r);
                }
            }
        }

        private void MergeTags(JsonArray tags)
        {
            List<string> list = new List<string>();
            for (uint i = 0; i < tags.Count; i++)
            {
                list.Add(tags.GetStringAt(i));
            }
            collection.Tags.Register(list, maxUsn);
        }

        private void MergeConf(JsonObject conf)
        {
            collection.Conf = conf;
        }

        public JsonObject SanityCheck()
        {
            JsonObject result = new JsonObject();

            if (collection.Database.QueryScalar<int>("SELECT count() FROM cards WHERE nid NOT IN (SELECT id FROM notes)") != 0)
            {
                result.Add("client", JsonValue.CreateStringValue("missing notes"));
                return result;
            }
            if (collection.Database.QueryScalar<int>("SELECT count() FROM notes WHERE id NOT IN (SELECT DISTINCT nid FROM cards)") != 0)
            {
                result.Add("client", JsonValue.CreateStringValue("missing cards"));
                return result;
            }
            if (collection.Database.QueryScalar<int>("SELECT count() FROM cards WHERE usn = -1") != 0)
            {
                result.Add("client", JsonValue.CreateStringValue("cards had usn = -1"));
                return result;
            }
            if (collection.Database.QueryScalar<int>("SELECT count() FROM notes WHERE usn = -1") != 0)
            {
                result.Add("client", JsonValue.CreateStringValue("notes had usn = -1"));
                return result;
            }
            if (collection.Database.QueryScalar<int>("SELECT count() FROM revlog WHERE usn = -1") != 0)
            {
                result.Add("client", JsonValue.CreateStringValue("revlog had usn = -1"));
                return result;
            }
            if (collection.Database.QueryScalar<int>("SELECT count() FROM graves WHERE usn = -1") != 0)
            {
                result.Add("client", JsonValue.CreateStringValue("graves had usn = -1"));
                return result;
            }
            foreach (JsonObject g in collection.Deck.All())
            {
                if (g.GetNamedNumber("usn") == -1)
                {
                    result.Add("client", JsonValue.CreateStringValue("deck had usn = -1"));
                    return result;
                }
            }
            foreach (var tag in collection.Tags.GetTags())
            {
                if (tag.Value.GetNumber() == -1)
                {
                    result.Add("client", JsonValue.CreateStringValue("tag had usn = -1"));
                    return result;
                }
            }
            bool found = false;
            foreach (JsonObject m in collection.Models.All())
            {
                if (collection.IsServer)
                {
                    // the web upgrade was mistakenly setting usn
                    if (m.GetNamedNumber("usn") < 0)
                    {
                        m["usn"] = JsonValue.CreateNumberValue(0);
                        found = true;
                    }
                }
                else
                {
                    if (m.GetNamedNumber("usn") == -1)
                    {
                        result.Add("client", JsonValue.CreateStringValue("model had usn = -1"));
                        return result;
                    }
                }
            }
            if (found)
            {
                collection.Models.Save();
            }
            collection.Sched.Reset();
            // check for missing parent decks
            collection.Sched.DeckDueList();
            // return summary of deck
            JsonArray ja = new JsonArray();
            JsonArray sa = new JsonArray();
            foreach (int c in collection.Sched.Counts())
            {
                sa.Add(JsonValue.CreateNumberValue(c));
            }
            ja.Add(sa);
            ja.Add(JsonValue.CreateNumberValue(
                    collection.Database.QueryScalar<int>("SELECT count() FROM cards")));
            ja.Add(JsonValue.CreateNumberValue(
                    collection.Database.QueryScalar<int>("SELECT count() FROM notes")));
            ja.Add(JsonValue.CreateNumberValue(
                collection.Database.QueryScalar<int>("SELECT count() FROM revlog")));
            ja.Add(JsonValue.CreateNumberValue(
                collection.Database.QueryScalar<int>("SELECT count() FROM graves")));
            ja.Add(JsonValue.CreateNumberValue(
                collection.Models.All().Count));
            ja.Add(JsonValue.CreateNumberValue(
                collection.Deck.All().Count));
            ja.Add(JsonValue.CreateNumberValue
                (collection.Deck.AllConf().Count));
            result.Add("client", ja);
            return result;
        }

        private string UsnLim()
        {
            if (collection.IsServer)
            {
                return "usn >= " + minUsn;
            }
            else
            {
                return "usn = -1";
            }
        }

        public long Finish()
        {
            return Finish(0);
        }

        private long Finish(long mod)
        {
            if (mod == 0)
            {
                // server side; we decide new mod time
                mod = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            }
            collection.Ls = mod;
            collection.SetUsnAfterSync = maxUsn + 1;
            // ensure we save the mod time even if no changes made
            collection.Database.IsModified = true;
            collection.Save(mod, null);
            return mod;
        }

        private void PrepareToChunk()
        {
            tablesLeft = new LinkedList<string>();
            tablesLeft.AddLast("revlog");
            tablesLeft.AddLast("cards");
            tablesLeft.AddLast("notes");
        }

        /// <summary>
        /// In java and python ver, cursorForTable uses a string-type argument table 
        /// to retrieve either cards, notes, or revlog from database
        /// </summary>
        /// <param name="table"></param>
        private List<object[]> CursorForTable(string tableType)
        {
            string lim = UsnLim();

            switch (tableType)
            {
                case "revlog":
                    var listLog = collection.Database.QueryColumn<revlog>(String.Format(Media.locale,
                                        "SELECT id, cid, ease, ivl, lastIvl, factor, time, type FROM revlog WHERE {0}", lim));
                    return (from s in listLog select new object[] { s.Id, s.Cid, maxUsn, s.Ease, s.Interval, s.LastInterval, s.Factor, s.Time, s.Type })
                           .ToList();

                case "cards":
                    var listCard = collection.Database.QueryColumn<CardTable>(String.Format(Media.locale,
                                        "SELECT id, nid, did, ord, mod, type, queue, due, ivl, factor, reps, lapses, left, odue, odid, flags, data FROM cards WHERE {0}",
                                         lim));
                    return (from s in listCard select new object[] { s.Id, s.Nid, s.Did, s.Ord, s.Mod, maxUsn, s.Type,
                                                                     s.Queue, s.Due, s.Interval, s.Factor, s.Reps, s.Lapses, s.Left,
                                                                    s.ODue, s.ODid, s.Flags, s.Data })
                           .ToList();

                default:
                    var listNote = collection.Database.QueryColumn<NoteTable>(String.Format(Media.locale,
                                        "SELECT id, guid, mid, mod, tags, flds, flags, data FROM notes WHERE {0}",
                                        lim));
                    return (from s in listNote select new object[] { s.Id, s.GuId, s.Mid, s.TimeModified, maxUsn, s.Tags, s.Fields, "", "", s.Flags, s.Data})
                           .ToList();
            }
        }

        public JsonObject Chunk()
        {
            JsonObject buf = new JsonObject();
            buf.Add("done", JsonValue.CreateBooleanValue(false));
            int lim = 250;
            while (!(tablesLeft.Count == 0) && lim > 0)
            {
                string curTable = tablesLeft.First();
                var listObject = CursorForTable(curTable);
                JsonArray rows = new JsonArray();
                int fetched = 0;
                foreach (object[] objArray in listObject)
                {
                    JsonArray r = new JsonArray();
                    foreach (object obj in objArray)
                    {
                        if (obj is String)
                            r.Add(JsonValue.CreateStringValue(Convert.ToString(obj)));
                        else
                            r.Add(JsonValue.CreateNumberValue(Convert.ToDouble(obj)));
                    }
                    rows.Add(r);
                    if (++fetched == lim)
                        break;
                }
                if (fetched != lim)
                {
                    // table is empty
                    tablesLeft.RemoveFirst();

                    // if we're the client, mark the objects as having been sent
                    if (!collection.IsServer)
                    {
                        collection.Database.Execute("UPDATE " + curTable + " SET usn=" + maxUsn + " WHERE usn=-1");
                    }
                }
                buf.Add(curTable, rows);
                lim -= fetched;
            }
            if (tablesLeft.Count == 0)
            {
                buf["done"] = JsonValue.CreateBooleanValue(true);
            }
            return buf;
        }

        public void ApplyChunk(JsonObject chunk)
        {
            if (chunk.ContainsKey("revlog"))
            {
                MergeRevlog(chunk.GetNamedArray("revlog"));
            }
            if (chunk.ContainsKey("cards"))
            {
                MergeCards(chunk.GetNamedArray("cards"));
            }
            if (chunk.ContainsKey("notes"))
            {
                MergeNotes(chunk.GetNamedArray("notes"));
            }
        }

        [SQLite.Net.Attributes.Table("graves")]
        private class graves
        {
            [SQLite.Net.Attributes.Column("oid")]
            public long Oid { get; set; }

            [SQLite.Net.Attributes.Column("type")]
            public int Type { get; set; }
        }

        private JsonObject Removed()
        {
            JsonArray cards = new JsonArray();
            JsonArray notes = new JsonArray();
            JsonArray decks = new JsonArray();
            var list = collection.Database.QueryColumn<graves>( "SELECT oid, type FROM graves WHERE usn"
                                    + (collection.IsServer ? (" >= " + minUsn) : (" = -1")));
            foreach (var g in list)
            {
                RemovalType type = (RemovalType)g.Type;
                switch (type)
                {
                    case RemovalType.CARD:
                        cards.Add(JsonValue.CreateNumberValue(g.Oid));
                        break;
                    case RemovalType.NOTE:
                        notes.Add(JsonValue.CreateNumberValue(g.Oid));
                        break;
                    case RemovalType.DECK:
                        decks.Add(JsonValue.CreateNumberValue(g.Oid));
                        break;
                }
            }
            if (!collection.IsServer)
            {
                collection.Database.Execute("UPDATE graves SET usn=" + maxUsn + " WHERE usn=-1");
            }
            JsonObject o = new JsonObject();
            o.Add("cards", cards);
            o.Add("notes", notes);
            o.Add("decks", decks);
            return o;
        }

        public JsonObject Start(int minUsn, bool leftNewer, JsonObject graves)
        {
            maxUsn = collection.GetUsnForSync;
            this.minUsn = minUsn;
            this.isClientNewer = !leftNewer;
            JsonObject lgraves = Removed();
            Remove(graves);
            return lgraves;
        }

        private void Remove(JsonObject graves)
        {
            // pretend to be the server so we don't set usn = -1
            bool wasServer = collection.IsServer;
            collection.IsServer = true;
            // notes first, so we don't end up with duplicate graves
            collection.RemoveNotesOnly(Utils.JsonArrayToLongArray(graves.GetNamedArray("notes", new JsonArray())));
            // then cards
            collection.RemoveCardsAndNoteIfNoCardsLeft(Utils.JsonArrayToLongArray(graves.GetNamedArray("cards", new JsonArray())), false);
            // and decks
            JsonArray decks = graves.GetNamedArray("decks", new JsonArray());
            for (uint i = 0; i < decks.Count; i++)
            {
                collection.Deck.Remove((long)decks.GetNumberAt(i), false, false);
            }
            collection.IsServer = wasServer;
        }

        private void MergeRevlog(JsonArray logs)
        {
            for (uint i = 0; i < logs.Count; i++)
            {
                collection.Database.Execute("INSERT OR IGNORE INTO revlog VALUES (?,?,?,?,?,?,?,?,?)",
                        Utils.JsonArray2Objects(logs.GetArrayAt(i)));
            }
        }

        private class UnknownTable
        {
            [SQLite.Net.Attributes.Column("id")]
            public long Id { get; set; }
            [SQLite.Net.Attributes.Column("mod")]
            public long Mod { get; set; }
        }
        private List<object[]> NewerRows(JsonArray data, string table, int modIdx)
        {
            long[] ids = new long[data.Count];
            for (uint i = 0; i < data.Count; i++)
            {
                ids[i] = (long)data.GetArrayAt(i).GetNumberAt(0);
            }
            Dictionary<long, long> lmods = new Dictionary<long, long>();
            var list = collection.Database.QueryColumn<UnknownTable>(
                            "SELECT id, mod FROM " + table + " WHERE id IN " + Utils.Ids2str(ids) + " AND "
                                    + UsnLim());
            foreach (var c in list)
            {
                lmods.Add(c.Id, c.Mod);
            }

            List<object[]> update = new List<object[]>();
            for (uint i = 0; i < data.Count; i++)
            {
                JsonArray r = data.GetArrayAt(i);
                long num = (long)r.GetNumberAt(0);
                if (!lmods.ContainsKey(num) 
                    || lmods[num] < r.GetNumberAt((uint)modIdx))
                {
                    update.Add(Utils.JsonArray2Objects(r));
                }
            }
            collection.Log(args: new object[] { table, data });
            return update;
        }

        private void MergeCards(JsonArray cards)
        {
            foreach (object[] r in NewerRows(cards, "cards", 4))
            {
                collection.Database.Execute("INSERT OR REPLACE INTO cards VALUES (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)", r);
            }
        }

        private void MergeNotes(JsonArray notes)
        {
            //WARNING: Python ver uses 3 why java ver uses 4 here
            //We suspect it's an error in java code
            foreach (object[] n in NewerRows(notes, "notes", 3))
            {
                collection.Database.Execute("INSERT OR REPLACE INTO notes VALUES (?,?,?,?,?,?,?,?,?,?,?)", n);
                collection.UpdateFieldCache(new long[] {Convert.ToInt64(n[0]) });
            }
        }
    }


}

