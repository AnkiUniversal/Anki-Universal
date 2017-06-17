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
using AnkiU.ViewModels;
using AnkiU.UIUtilities;

namespace AnkiU.AnkiCore
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
        public async static Task<Collection> OpenOrCreateCollection(StorageFolder folder, string relativePath, bool server = false, bool log = false)
        {
            DB collectionDatabase = null;
            try
            {
                Debug.Assert(relativePath.EndsWith(".anki2"));
                Debug.WriteLine("Folder path: {0}", folder.Path);
                Debug.WriteLine("File relative path: {0}", relativePath);

                Hooks.Hooks.GetInstance();
                StorageFile file = await folder.TryGetItemAsync(relativePath) as StorageFile;
                bool create = file == null;
                collectionDatabase = new DB(folder.Path + "\\" + relativePath);
                // initialize
                int ver;
                if (create)
                {
                    ver = CreateDB(collectionDatabase);                    
                }
                else
                {
                    ver = UpgradeSchema(collectionDatabase);
                }
                collectionDatabase.Execute("PRAGMA temp_store = memory");
                // add db to col and do any remaining upgrades
                Collection col = new Collection(collectionDatabase, relativePath, server, log, folder);
                if (ver < Constant.SCHEMA_VERSION)
                {
                    Upgrade(col, ver);
                }
                else if (create)
                {
                    try
                    {
                        // add in reverse order so basic is default
                        Models.AddClozeModel(col);
                        Models.AddForwardOptionalReverse(col);
                        Models.AddForwardReverse(col);
                        Models.AddBasicModel(col);

                        //Only in AnkiU we add 4 more conf presets
                        AddConfigPresets(col);

                    }
                    catch (ConfirmModSchemaException e)
                    {
                        throw new Exception("This should never reached as we've just created a new database", e);
                    }
                    col.Save();
                }
                return col;
            }
            catch(Exception)
            {
                if(collectionDatabase != null)
                    collectionDatabase.Close();                
                return null;                
            }
        }

        private static void AddConfigPresets(Collection col)
        {
            int id = (int)ConfigPresets.TagOnLeech;
            string name = UIConst.TAG_ONLY_LEECH_NAME;
            JsonObject config = JsonObject.Parse(Deck.defaultConf);
            config.GetNamedObject("lapse")["leechAction"] = JsonValue.CreateNumberValue(1);
            AddConfig(col, id, name, config);

            id = (int)ConfigPresets.ShortInterval;
            name = UIConst.SHORT_INTERVAL_NAME;
            config = JsonObject.Parse(Deck.defaultConf);
            config.GetNamedObject("rev")["ivlFct"] = JsonValue.CreateNumberValue(0.8F);
            AddConfig(col, id, name, config);

            id = (int)ConfigPresets.LongInterval;
            name = UIConst.LONG_INTERVAL_NAME;
            config = JsonObject.Parse(Deck.defaultConf);
            config.GetNamedObject("rev")["ivlFct"] = JsonValue.CreateNumberValue(1.2F);
            AddConfig(col, id, name, config);

            id = (int)ConfigPresets.DueOnly;
            name = UIConst.DUE_ONLY_NAME;
            config = JsonObject.Parse(Deck.defaultConf);
            config.GetNamedObject("new")["perDay"] = JsonValue.CreateNumberValue(0);
            AddConfig(col, id, name, config);

            col.Deck.Save();
            col.SaveAndCommit();
        }

        private static void AddConfig(Collection col, int id, string name, JsonObject config)
        {
            config["id"] = JsonValue.CreateNumberValue(id);
            config["name"] = JsonValue.CreateStringValue(name);
            col.Deck.DeckConf.Add(id, config);            
        }

        private static int UpgradeSchema(DB db)
        {
            int ver = db.QueryScalar<int>("SELECT ver FROM col");
            if (ver == Constant.SCHEMA_VERSION)
            {
                return ver;
            }
            // add odid to cards, edue->odue
            if (db.QueryScalar<int>("SELECT ver FROM col") == 1)
            {
                db.Execute("ALTER TABLE cards RENAME TO cards2");
                AddSchema(db, false);
                db.Execute("insert into cards select id, nid, did, ord, mod, usn, type, queue, due, ivl, factor, reps, lapses, left, edue, 0, flags, data from cards2");
                db.Execute("DROP TABLE cards2");
                db.Execute("UPDATE col SET ver = 2");
                UpdateIndices(db);
            }
            // remove did from notes
            if (db.QueryScalar<int>("SELECT ver FROM col") == 2)
            {
                db.Execute("ALTER TABLE notes RENAME TO notes2");
                AddSchema(db, false);
                db.Execute("insert into notes select id, guid, mid, mod, usn, tags, flds, sfld, csum, flags, data from notes2");
                db.Execute("DROP TABLE notes2");
                db.Execute("UPDATE col SET ver = 3");
                UpdateIndices(db);
            }
            return ver;
        }

        private static void Upgrade(Collection col, int ver)
        {
                if (ver < 3)
                {
                    // new deck properties
                    foreach (JsonObject d in col.Deck.All())
                    {
                        d["dyn"] = JsonValue.CreateNumberValue(0);
                        d["collapsed"] = JsonValue.CreateBooleanValue(false);
                        col.Deck.Save(d);
                    }
                }
                if (ver < 4)
                {
                    col.ModSchemaNoCheck();
                    List<JsonObject> clozes = new List<JsonObject>();
                    foreach (JsonObject m in col.Models.All())
                    {
                        if (!m.GetNamedArray("tmpls").GetObjectAt(0).GetNamedString("qfmt").Contains("{{cloze:"))
                        {
                            m["type"] = JsonValue.CreateNumberValue((int)ModelType.STD);
                        }
                        else
                        {
                            clozes.Add(m);
                        }
                    }
                    foreach (JsonObject m in clozes)
                    {
                        UpgradeClozeModel(col, m);
                    }
                    col.Database.Execute("UPDATE col SET ver = 4");
                }
                if (ver < 5)
                {
                    col.Database.Execute("UPDATE cards SET odue = 0 WHERE queue = 2");
                    col.Database.Execute("UPDATE col SET ver = 5");
                }
                if (ver < 6)
                {
                    col.ModSchemaNoCheck();
                    foreach (JsonObject m in col.Models.All())
                    {
                        m["css"] = JsonObject.Parse(Models.defaultModel).GetNamedValue("css");
                        JsonArray ar = m.GetNamedArray("tmpls");
                        for (uint i = 0; i < ar.Count; i++)
                        {
                            JsonObject t = ar.GetObjectAt(i);
                            if (!t.ContainsKey("css"))
                            {
                                continue;
                            }
                            m["css"] = JsonValue.CreateStringValue(
                                    m.GetNamedString("css") + "\n"
                                            + t.GetNamedString("css").Replace(".card ", ".card" + t.GetNamedString("ord") + 1));
                            t.Remove("css");
                        }
                        col.Models.Save(m);
                    }
                    col.Database.Execute("UPDATE col SET ver = 6");
                }
                if (ver < 7)
                {
                    col.ModSchemaNoCheck();
                    col.Database.Execute("UPDATE cards SET odue = 0 WHERE (type = 1 OR queue = 2) AND NOT odid");
                    col.Database.Execute("UPDATE col SET ver = 7");
                }
                if (ver < 8)
                {
                    col.ModSchemaNoCheck();
                    col.Database.Execute("UPDATE cards SET due = due / 1000 WHERE due > 4294967296");
                    col.Database.Execute("UPDATE col SET ver = 8");
                }
                if (ver < 9)
                {
                    col.Database.Execute("UPDATE col SET ver = 9");
                }
                if (ver < 10)
                {
                    col.Database.Execute("UPDATE cards SET left = left + left * 1000 WHERE queue = 1");
                    col.Database.Execute("UPDATE col SET ver = 10");
                }
            if (ver < 11)
            {
                col.ModSchemaNoCheck();
                foreach (JsonObject d in col.Deck.All())
                {
                    if (JsonHelper.GetNameNumber(d,"dyn") != 0)
                    {
                        int order = (int)JsonHelper.GetNameNumber(d,"order");
                        // failed order was removed
                        if (order >= 5)
                        {
                            order -= 1;
                        }
                        JsonArray ja = new JsonArray();
                        ja.Add(d.GetNamedValue("search"));
                        ja.Add(d.GetNamedValue("limit"));
                        ja.Add(JsonValue.CreateNumberValue(order));
                        d.Add("terms", new JsonArray());
                        d.GetNamedArray("terms").Insert(0, ja);
                        d.Remove("search");
                        d.Remove("limit");
                        d.Remove("order");
                        d["resched"] = JsonValue.CreateBooleanValue(true);
                        d["return"] = JsonValue.CreateBooleanValue(true);
                    }
                    else
                    {
                        if (!d.ContainsKey("extendNew"))
                        {
                            d["extendNew"] = JsonValue.CreateNumberValue(10);
                            d["extendRev"] = JsonValue.CreateNumberValue(50);
                        }
                    }
                    col.Deck.Save(d);
                }
                foreach (JsonObject c in col.Deck.AllConf())
                {
                    JsonObject r = c.GetNamedObject("rev");
                    r["ivlFct"] = JsonValue.CreateNumberValue(JsonHelper.GetNameNumber(r,"ivlFct", 1));
                    if (r.ContainsKey("ivlfct"))
                    {
                        r.Remove("ivlfct");
                    }
                    r["maxIvl"] = JsonValue.CreateNumberValue(36500);
                    col.Deck.Save(c);
                }
                foreach (JsonObject m in col.Models.All())
                {
                    JsonArray tmpls = m.GetNamedArray("tmpls");
                    for (uint ti = 0; ti < tmpls.Count; ++ti)
                    {
                        JsonObject t = tmpls.GetObjectAt(ti);
                        t["bqfmt"] = JsonValue.CreateStringValue("");
                        t["bafmt"] = JsonValue.CreateStringValue("");
                    }
                    col.Models.Save(m);
                }
                col.Database.Execute("update col set ver = 11");
            }
        }

        private static int CreateDB(DB db)
        {
            db.Execute("PRAGMA page_size = 4096");
            db.Execute("PRAGMA legacy_file_format = 0");
            db.Execute("VACUUM");
            AddSchema(db);
            UpdateIndices(db);
            db.Execute("ANALYZE");
            return Constant.SCHEMA_VERSION;
        }

        private static void AddSchema(DB db, bool setColConf = true)
        {
            db.Execute("create table if not exists col ( " + "id              integer primary key, "
                    + "crt             integer not null," + "mod             integer not null,"
                    + "scm             integer not null," + "ver             integer not null,"
                    + "dty             integer not null," + "usn             integer not null,"
                    + "ls              integer not null," + "conf            text not null,"
                    + "models          text not null," + "decks           text not null,"
                    + "dconf           text not null," + "tags            text not null" + ");");
            db.Execute("create table if not exists notes (" + "   id              integer primary key,   /* 0 */"
                    + "  guid            text not null,   /* 1 */" + " mid             integer not null,   /* 2 */"
                    + " mod             integer not null,   /* 3 */" + " usn             integer not null,   /* 4 */"
                    + " tags            text not null,   /* 5 */" + " flds            text not null,   /* 6 */"
                    + " sfld            integer not null,   /* 7 */" + " csum            integer not null,   /* 8 */"
                    + " flags           integer not null,   /* 9 */" + " data            text not null   /* 10 */" + ");");
            db.Execute("create table if not exists cards (" + "   id              integer primary key,   /* 0 */"
                    + "  nid             integer not null,   /* 1 */" + "  did             integer not null,   /* 2 */"
                    + "  ord             integer not null,   /* 3 */" + "  mod             integer not null,   /* 4 */"
                    + " usn             integer not null,   /* 5 */" + " type            integer not null,   /* 6 */"
                    + " queue           integer not null,   /* 7 */" + "    due             integer not null,   /* 8 */"
                    + "   ivl             integer not null,   /* 9 */" + "  factor          integer not null,   /* 10 */"
                    + " reps            integer not null,   /* 11 */" + "   lapses          integer not null,   /* 12 */"
                    + "   left            integer not null,   /* 13 */" + "   odue            integer not null,   /* 14 */"
                    + "   odid            integer not null,   /* 15 */" + "   flags           integer not null,   /* 16 */"
                    + "   data            text not null   /* 17 */" + ");");
            db.Execute("create table if not exists revlog (" + "   id              integer primary key,"
                    + "   cid             integer not null," + "   usn             integer not null,"
                    + "   ease            integer not null," + "   ivl             integer not null,"
                    + "   lastIvl         integer not null," + "   factor          integer not null,"
                    + "   time            integer not null," + "   type            integer not null" + ");");
            db.Execute("create table if not exists graves (" + "    usn             integer not null,"
                    + "    oid             integer not null," + "    type            integer not null" + ")");
            db.Execute("INSERT OR IGNORE INTO col VALUES(1,0,0," +
                    DateTimeOffset.Now.ToUnixTimeMilliseconds() + "," + Constant.SCHEMA_VERSION +
                    ",0,0,0,'','{}','','','{}')");
            if (setColConf)
            {
                SetColConfigs(db);
            }
        }

        private static void SetColConfigs(DB db)
        {
            JsonObject g = JsonObject.Parse(Deck.defaultDeck);
            g["id"] =  JsonValue.CreateNumberValue(1);
            g["name"] = JsonValue.CreateStringValue("Default");
            g["conf"] = JsonValue.CreateNumberValue(1);
            g["mod"] = JsonValue.CreateNumberValue(DateTimeOffset.Now.ToUnixTimeSeconds());

            JsonObject gc = JsonObject.Parse(Deck.defaultConf);
            gc["id"] = JsonValue.CreateNumberValue(1);
            JsonObject ag = new JsonObject();
            ag["1"] =  g;
            JsonObject agc = new JsonObject();
            agc["1"] = gc;

            db.Execute("update col set conf =?, decks =?, dconf =?",
                               AnkiCore.Collection.defaultConf,
                               Utils.JsonToString(ag),
                               Utils.JsonToString(agc));
        }

        private static void UpdateIndices(DB db)
        {
            db.Execute("create index if not exists ix_notes_usn on notes (usn);");
            db.Execute("create index if not exists ix_cards_usn on cards (usn);");
            db.Execute("create index if not exists ix_revlog_usn on revlog (usn);");
            db.Execute("create index if not exists ix_cards_nid on cards (nid);");
            db.Execute("create index if not exists ix_cards_sched on cards (did, queue, due);");
            db.Execute("create index if not exists ix_revlog_cid on revlog (cid);");
            db.Execute("create index if not exists ix_notes_csum on notes (csum);)");
        }

        private static void UpgradeClozeModel(Collection col, JsonObject m)
        {
            m["type"] = JsonValue.CreateNumberValue((int)ModelType.CLOZE);
            // convert first template
            JsonObject t = m.GetNamedArray("tmpls").GetObjectAt(0);
            foreach (string type in new string[] { "qfmt", "afmt" })
            {
                string str = Regex.Replace(t.GetNamedString(type), @"{{cloze:1:(.+?)}}", "{{cloze:$1}}");
                t[type] = JsonValue.CreateStringValue(str);
            }
            t["name"] = JsonValue.CreateStringValue("Cloze");
            // delete non-cloze cards for the model
            JsonArray ja = m.GetNamedArray("tmpls");
            List<JsonObject> rem = new List<JsonObject>();
            for (uint i = 1; i < ja.Count; i++)
            {
                JsonObject ta = ja.GetObjectAt(i);
                if (!ta.GetNamedString("afmt").Contains("{{cloze:"))
                {
                    rem.Add(ta);
                }
            }
            foreach (JsonObject r in rem)
            {
                col.Models.RemoveTemplate(m, r);
            }
            JsonArray newArray = new JsonArray();
            newArray.Add(ja[0]);
            m["tmpls"] = newArray;
            col.Models.UpdateTemplOrds(m);
            col.Models.Save(m);
        }

        public static void AddIndices(DB database)
        {
            UpdateIndices(database);
        }
    }
}
