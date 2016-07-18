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
using System.Diagnostics;

namespace AnkiU.AnkiCore
{
    public class Deck
    {
        public const string defaultDeck = ""
            + "{"
                + "\"newToday\": [0, 0]," // currentDay, count
                + "\"revToday\": [0, 0],"
                + "\"lrnToday\": [0, 0],"
                + "\"timeToday\": [0, 0]," // time in ms
                + "\"conf\": 1,"
                + "\"usn\": 0,"
                + "\"desc\": \"\","
                + "\"dyn\": 0," // anki uses int/bool interchangably here
                + "\"collapsed\": false,"
                // added in beta11
                + "\"extendNew\": 10,"
                + "\"extendRev\": 50"
            + "}";

        private const string defaultDynamicDeck = ""
            + "{"
                + "\"newToday\": [0, 0],"
                + "\"revToday\": [0, 0],"
                + "\"lrnToday\": [0, 0],"
                + "\"timeToday\": [0, 0],"
                + "\"collapsed\": false,"
                + "\"dyn\": 1,"
                + "\"desc\": \"\","
                + "\"usn\": 0,"
                + "\"delays\": null,"
                + "\"separate\": true,"
                // list of (search, limit, order); we only use first element for now
                + "\"terms\": [[\"\", 100, 0]],"
                + "\"resched\": true,"
                + "\"return\": true" // currently unused
            + "}";

        public static readonly string defaultConf = ""
            + "{"
                + "\"name\": \"Default\","
                + "\"new\": {"
                    + "\"delays\": [1, 10],"
                    + "\"ints\": [1, 4, 7]," // 7 is not currently used
                    + "\"initialFactor\": 2500,"
                    + "\"separate\": true,"
                    + "\"order\": " + (int)NewCardInsertOrder.DUE + ","
                    + "\"perDay\": 20,"
                    // Python and java ver set this to true by default
                    // But in AnkiU we set it to false since when user creates
                    // a sibling card they expects to be shown immediately
                    + "\"bury\": false"
                + "},"
                + "\"lapse\": {"
                    + "\"delays\": [10],"
                    + "\"mult\": 0,"
                    + "\"minInt\": 1,"
                    + "\"leechFails\": 8,"
                    // type 0=suspend, 1=tagonly
                    + "\"leechAction\": 0"
                + "},"
                + "\"rev\": {"
                    + "\"perDay\": 100,"
                    + "\"ease4\": 1.3,"
                    + "\"fuzz\": 0.05,"
                    + "\"minSpace\": 1," // not currently used
                    + "\"ivlFct\": 1,"
                    + "\"maxIvl\": 36500,"
                    // Python and java ver set this to true by default
                    // But in AnkiU we set it to false since when user creates
                    // a sibling card they expects to be shown immediately
                    + "\"bury\": false"
                + "},"
                + "\"maxTaken\": 60,"
                + "\"timer\": 0,"
                + "\"autoplay\": true," 
                + "\"replayq\": true,"
                + "\"mod\": 0,"
                + "\"usn\": 0"
            + "}";       

        private Collection collection;
        private Dictionary<long, JsonObject> deckDict;
        private Dictionary<long, JsonObject> deckConf;
        private bool isChanged;

        public Dictionary<long, JsonObject> DeckDict { get { return deckDict; } }
        public Dictionary<long, JsonObject> DeckConf { get { return deckConf; } }

        public Deck(Collection collection)
        {
            this.collection = collection;
        }

        public void Load(string decksName, string deckConf)
        {
            try
            {
                deckDict = new Dictionary<long, JsonObject>();
                this.deckConf = new Dictionary<long, JsonObject>();
                JsonObject decksArray = JsonObject.Parse(decksName);
                foreach (var json in decksArray)
                {
                    deckDict.Add(Convert.ToInt64(json.Key), json.Value.GetObject());
                }
                JsonObject confArray = JsonObject.Parse(deckConf);
                foreach (var json in confArray)
                {
                    this.deckConf.Add(Convert.ToInt64(json.Key), json.Value.GetObject());
                }

            }
            catch (Exception e)
            {
                throw new Exception("Decks load error!\n", e);
            }
            isChanged = false;
        }

        public void Save(JsonObject jObj = null)
        {
            if (jObj != null)
            {
                try
                {
                    jObj["mod"] = JsonValue.CreateNumberValue(DateTimeOffset.Now.ToUnixTimeSeconds());
                    jObj["usn"] = JsonValue.CreateNumberValue(collection.Usn);
                }
                catch (Exception e)
                {
                    throw new Exception("Decks save error!\n", e);
                }
            }
            isChanged = true;
        }

        public void SaveChangesToDatabase()
        {
            if (isChanged)
            {
                JsonObject decksArray = new JsonObject();
                foreach (KeyValuePair<long, JsonObject> d in deckDict)
                    decksArray.Add(d.Key.ToString(), d.Value);

                JsonObject confArray = new JsonObject();
                foreach (KeyValuePair<long, JsonObject> d in deckConf)
                    confArray.Add(d.Key.ToString(), d.Value);

                collection.Database.Execute("update col set decks=?, dconf=?",
                                             Utils.JsonToString(decksArray),
                                             Utils.JsonToString(confArray));
                isChanged = false;
            }
        }

        /// <summary>
        /// Add or reuse deck if already exists.
        /// </summary>
        /// <param name="name">Deck's name</param>
        /// <param name="create">If true, create a new deck if it does not exist</param>
        /// <returns>Deck ID</returns>
        public long? AddOrResuedDeck(string name, bool create = true)
        {
            return AddOrResuedDeck(name, create, defaultDeck);
        }

        /// <summary>
        /// Add or reuse deck if already exists.
        /// </summary>
        /// <param name="name">Deck's name</param>
        /// <param name="type"></param>
        /// <returns>Deck ID</returns>
        public long? AddOrResuedDeck(string name, string type)
        {
            return AddOrResuedDeck(name, true, type);
        }

        /// <summary>
        /// Add or reuse deck if already exists.
        /// </summary>
        /// <param name="name">Deck's name</param>
        /// <param name="create">If true, create a new deck if it does not exist</param>
        /// <param name="type"></param>
        /// <returns>Deck ID</returns>
        public long? AddOrResuedDeck(string name, bool create, string type)
        {
            name = name.Replace("\"", "");
            foreach (KeyValuePair<long, JsonObject> d in deckDict)
            {
                if (d.Value.GetNamedString("name").Equals(name, StringComparison.CurrentCultureIgnoreCase))
                    return d.Key;
            }
            if (!create)
                return null;
            if (name.Contains(":"))
            {
                name = EnsureParents(name);
            }
            JsonObject g;
            long id;
            g = JsonObject.Parse(type);
            g["name"] = JsonValue.CreateStringValue(name);
            while (true)
            {
                id = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                if (!deckDict.ContainsKey(id))
                    break;
            }
            g["id"] = JsonValue.CreateNumberValue(id);
            deckDict.Add(id, g);
            Save(g);
            MaybeAddToActive();
            //runHook("newDeck"); // TODO
            return id;
        }

        public string EnsureParents(string name)
        {
            StringBuilder s = new StringBuilder();
            string[] path = SplitPath(name);
            if (path.Length < 2)
                return name;

            for (int i = 0; i < path.Length - 1; i++)
            {
                if (s.Length == 0)
                    s.Append(path[i]);
                else
                {
                    s.Append("::");
                    s.Append(path[i]);
                }

                long? did = AddOrResuedDeck(s.ToString());
                s.Clear();
                s.Append(GetDeckName(did));
            }
            s.Append("::");
            s.Append(path[path.Length - 1]);
            return s.ToString();
        }

        private string[] SplitPath(string name)
        {
            string[] sep = new string[] { "::" };
            return name.Split(sep, StringSplitOptions.None);
        }

        public string GetDeckName(long? did, bool def = false)
        {
            if (did == null)
                return "[no deck]";

            JsonObject deck = Get(did, def);
            if (deck != null)
            {
                return deck.GetNamedString("name");
            }
            return "[no deck]";
        }

        public JsonObject Get(long? did, bool def = true)
        {
            if (did == null)
                return null;

            long deckID = (long)did;

            if (deckDict.ContainsKey(deckID))
            {
                return deckDict[deckID];
            }
            else if (def)
            {
                return deckDict[1L];
            }
            else
            {
                return null;
            }
        }

        private void MaybeAddToActive()
        {
            // reselect current deck, or default if current has disappeared
            JsonObject c = Current();
            Select((long)c.GetNamedNumber("id"));
        }

        public JsonObject Current()
        {
            return Get(Selected());
        }

        public long Selected()
        {
            return (long)collection.Conf.GetNamedNumber("curDeck");
        }

        public void Select(long did)
        {
            string name = deckDict[did].GetNamedString("name");

            collection.Conf["curDeck"] = JsonValue.CreateNumberValue(did);

            // and active decks (current + all children)
            SortedDictionary<string, long> actv = new SortedDictionary<string, long>(Children(did));
            actv.Add(name, did);

            JsonArray ja = new JsonArray();

            foreach (long n in actv.Values)
                ja.Add(JsonValue.CreateNumberValue(n));

            collection.Conf["activeDecks"] = ja;
            isChanged = true;
        }

        public Dictionary<string, long> Children(long did)
        {
            string name;
            name = Get(did).GetNamedString("name");
            Dictionary<string, long> actv = new Dictionary<string, long>();
            foreach (JsonObject g in All())
            {
                if (g.GetNamedString("name").StartsWith(name + "::"))
                    actv.Add(g.GetNamedString("name"), (long)g.GetNamedNumber("id"));
            }
            return actv;
        }

        /// <summary>
        /// A list of all decks.
        /// </summary>
        /// <returns></returns>
        public List<JsonObject> All()
        {
            List<JsonObject> retunDecks = new List<JsonObject>();
            foreach (JsonObject d in deckDict.Values)
            {
                retunDecks.Add(d);
            }
            return retunDecks;
        }

        public void Remove(long deckId, bool cardsToo = false, bool childrenToo = true)
        {            
            JsonObject deck;
            if (deckId == 1)
            {
                // we won't allow the default deck to be deleted, but if it's a
                // child of an existing deck then it needs to be renamed
                deck = Get(deckId);
                if (deck.GetNamedString("name").Contains("::"))
                {
                    deck["name"] = JsonValue.CreateStringValue("Default");
                    Save(deck);
                }
                return;
            }

            // log the removal regardless of whether we have the deck or not
            collection.LogRem(new long[] { deckId }, RemovalType.DECK);

            if (!deckDict.ContainsKey(deckId))
                return;

            deck = Get(deckId);
            if (deck.GetNamedNumber("dyn") != 0)
            {
                // deleting a cramming deck returns cards to their previous deck
                // rather than deleting the cards
                collection.Sched.EmptyDyn(deckId);
                RemoveIfChildrenToo(deckId, childrenToo, cardsToo);
            }
            else
            {
                RemoveIfChildrenToo(deckId, childrenToo, cardsToo);
                RemoveIfNoteAndCardsToo(deckId, cardsToo);
            }

            deckDict.Remove(deckId);
            EnsureHaveActiveDeck(deckId);
            Save();
        }

        private void RemoveIfChildrenToo(long deckId, bool childrenToo, bool cardsToo)
        {
            if (childrenToo)
                foreach (long id in Children(deckId).Values)
                    Remove(id, cardsToo);
        }

        private void RemoveIfNoteAndCardsToo(long deckId, bool cardsToo)
        {
            string sql;
            if (cardsToo)
            {
                // don't use cids(), as we want cards in cram decks too
                sql = "SELECT id FROM cards WHERE did = " + deckId + " OR odid = " + deckId;
                long[] cids = (from s in collection.Database.QueryColumn<CardIdOnlyTable>(sql) select s.Id).ToArray();
                collection.RemoveCardsAndNoteIfNoCardsLeft(cids);
            }
        }

        private void EnsureHaveActiveDeck(long deckId)
        {
            if (Active().Contains(deckId))
                Select(deckDict.Keys.ToArray()[0]);
        }

        public LinkedList<long> Active()
        {
            JsonArray ja = collection.Conf.GetNamedArray("activeDecks");
            LinkedList<long> result = new LinkedList<long>();
            for (uint i = 0; i < ja.Count; i++)
            {
                result.AddLast((long)ja.GetNumberAt(i));
            }
            return result;
        }

        /// <summary>
        /// An unsorted list of all deck names.
        /// </summary>
        /// <param name="dyn"></param>
        /// <returns></returns>
        public List<string> AllNames(bool dyn= true)
        {
            List<string> list = new List<string>();
            if (dyn)
            {
                foreach (JsonObject x in deckDict.Values)
                    list.Add(x.GetNamedString("name"));

            }
            else
            {
                foreach (JsonObject x in deckDict.Values)
                    if (x.GetNamedNumber("dyn") == 0)
                        list.Add(x.GetNamedString("name"));
            }
            return list;
        }

        public List<JsonObject> AllSorted()
        {
            List<JsonObject> decks = All();
            decks.Sort( (JsonObject first, JsonObject second) =>
            {
                return first.GetNamedString("name").CompareTo(second.GetNamedString("name"));
            });
            return decks;
        }

        public bool HasDeckId(long id)
        {
            return deckDict.ContainsKey(id);
        }

        public long[] AllIds()
        {
            return deckDict.Keys.ToArray();
        }

        public void Collapse(long deckId)
        {
            JsonObject deck = Get(deckId);
            deck["collapsed"] = 
                JsonValue.CreateBooleanValue(!deck.GetNamedBoolean("collapsed"));
            Save(deck);
        }

        public void CollapseBrowser(long deckId)
        {
            JsonObject deck = Get(deckId);
            string browCollap = "browserCollapsed";
            bool collapsed = deck.GetNamedBoolean(browCollap, false);
            deck[browCollap] = JsonValue.CreateBooleanValue(!collapsed);
        }

        public int Count()
        {
            return deckDict.Count();
        }

        public JsonObject GetDeckByName(string name)
        {
            foreach(JsonObject m in deckDict.Values)
                if (m.GetNamedString("name").Equals(name))
                    return m;

            return null;
        }

        public void Update(JsonObject jObj)
        {
            deckDict[(long)jObj.GetNamedNumber("id")] = jObj;
            MaybeAddToActive();
            Save();
        }

        public void Rename(JsonObject jObj, string newName)
        {
            if (AllNames().Contains(newName))
                throw new DeckRenameException(DeckRenameException.ErrorCode.ALREADY_EXISTS);

            newName = EnsureParents(newName);

            // make sure we're not nesting under a filtered deck
            if (newName.Contains("::"))
            {
                string[] parts = newName.Split(new string[] { "::" }, StringSplitOptions.None);
                string[] subParts = new string[parts.Length - 1];
                Array.Copy(parts, subParts, subParts.Length);
                string newParent = String.Join("::", subParts);
                if (GetDeckByName(newParent).GetNamedNumber("dyn") != 0)
                    throw new DeckRenameException(DeckRenameException.ErrorCode.FILTERED_NOSUBDEKCS);
            }

            //Rename children
            string oldName = jObj.GetNamedString("name");
            string str;
            foreach (JsonObject grp in All())
            {
                if(grp.GetNamedString("name").StartsWith(oldName + "::"))
                {
                    str = grp.GetNamedString("name").ReplaceFirst(oldName + "::", newName + "::");
                    grp["name"] = JsonValue.CreateStringValue(str);
                    Save(grp);
                }
            }
            //adjust name
            jObj["name"] = JsonValue.CreateStringValue(newName);
            // ensure we have parents again, as we may have renamed parent->child
            newName = EnsureParents(newName);
            Save(jObj);
            // renaming may have altered active deckId order
            MaybeAddToActive();
        }

        public void RenameForDragAndDrop(long draggedDeckDid, long? ontoDeckDid)
        {
            JsonObject draggedDeck = Get(draggedDeckDid);
            string draggedDeckName = draggedDeck.GetNamedString("name");
            string ontoDeckName; ;

            if (ontoDeckDid == null)
            {
                if (SplitPath(draggedDeckName).Length > 1)
                {
                    Rename(draggedDeck, BaseName(draggedDeckName));
                }
            }
            else
            {
                ontoDeckName = Get(ontoDeckDid).GetNamedString("name");
                if (CanDragAndDrop(draggedDeckName, ontoDeckName))
                {
                    draggedDeck = Get(draggedDeckDid);
                    draggedDeckName = draggedDeck.GetNamedString("name");
                    ontoDeckName = Get(ontoDeckDid).GetNamedString("name");
                    Rename(draggedDeck, ontoDeckName + "::" + BaseName(draggedDeckName));
                }
            }
        }

        private string BaseName(string name)
        {
            string[] path = SplitPath(name);
            return path[path.Length - 1];
        }

        private bool CanDragAndDrop(string draggedDeckName, string ontoDeckName)
        {
            if (draggedDeckName.Equals(ontoDeckName, StringComparison.OrdinalIgnoreCase)
                    || IsParent(ontoDeckName, draggedDeckName)
                    || IsAncestor(draggedDeckName, ontoDeckName))
            {
                return false;
            }
            else
            {
                return true;
            }
        }

        private bool IsParent(string parentDeckName, string childDeckName)
        {
            List<string> parentDeckPath = new List<string>(SplitPath(parentDeckName));
            parentDeckPath.Add(BaseName(childDeckName));

            string[] childDeckPath = SplitPath(childDeckName);
            int length = (parentDeckPath.Count > childDeckPath.Length)
                            ? childDeckPath.Length : parentDeckPath.Count;
            for (int i = 0; i < length; i++)
            {
                if (!childDeckPath[i].Equals(parentDeckPath[i], StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// Verify if the a deck name is the ancestor of the other
        /// </summary>
        /// <param name="ancestorDeckName">Ancestor name</param>
        /// <param name="descendantDeckName">Descendant name</param>
        /// <returns></returns>
        private bool IsAncestor(string ancestorDeckName, string descendantDeckName)
        {
            string[] ancestorDeckPath = SplitPath(ancestorDeckName);
            string[] descendantDeckPath = SplitPath(descendantDeckName);
            
            //WARNING: the java ver implementation of this function is wrong
            //we use the python ver implementation instead
            int length = ancestorDeckPath.Length;
            
            //An ancestor cannot have a longer or equal path as its descendant
            if (length >= descendantDeckPath.Length)
                return false;
            
            for (int i = 0; i < length; i++)
            {
                if (!ancestorDeckPath[i].Equals(descendantDeckPath[i], StringComparison.OrdinalIgnoreCase))
                    return false;
            }
            return true;
        }

        /// <summary>
        /// A list of all deck config
        /// </summary>
        /// <returns></returns>
        public List<JsonObject> AllConf()
        {
            List<JsonObject> confs = new List<JsonObject>();
            foreach (JsonObject c in deckConf.Values)
            {
                confs.Add(c);
            }
            return confs;
        }

        public JsonObject ConfForDeckId(long did)
        {
            JsonObject deck = Get(did, false);
            if (deck == null)
                throw new DeckNotFoundException();

            if (deck.ContainsKey("conf"))
            {
                JsonObject conf = GetConf((long)deck.GetNamedNumber("conf"));
                conf["dyn"] = JsonValue.CreateNumberValue(0);
                return conf;
            }
            // dynamic decks have embedded conf
            return deck;
        }

        public JsonObject GetConf(long confId)
        {
            return deckConf[confId];
        }

        public void UpdateConf(JsonObject jObj)
        {
            deckConf[(long)jObj.GetNamedNumber("id")] = jObj;
            Save();
        }

        /// <summary>
        /// Create a new configuration and return ID
        /// </summary>
        /// <param name="name"></param>
        /// <param name="cloneFrom"></param>
        /// <returns>ID of the new configuration</returns>
        public long CreateNewConfiguration(string name)
        {
            return CreateNewConfiguration(name, defaultConf);
        }

        /// <summary>
        /// Create a new configuration and return ID
        /// </summary>
        /// <param name="name"></param>
        /// <param name="cloneFrom"></param>
        /// <returns>ID of the new configuration</returns>
        public long CreateNewConfiguration(string name, string cloneFrom)
        {
            JsonObject jObj;
            long id;
            jObj = JsonObject.Parse(cloneFrom);
            while (true)
            {
                id = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                if (!deckConf.ContainsKey(id))
                    break;
            }
            jObj["id"] = JsonValue.CreateNumberValue(id);
            jObj["name"] = JsonValue.CreateStringValue(name);
            deckConf.Add(id, jObj);
            Save(jObj);
            return id;
        }

        /// <summary>
        /// Remove a configuration and update all decks using it.
        /// </summary>
        /// <param name="id"></param>
        public void RemoveConfiguration(long id) 
        {
            Debug.Assert(id != 1);
            
            collection.ModSchema(true);
            deckConf.Remove(id);
            foreach (JsonObject g in All())
            {
                // ignore cram decks
                if (!g.ContainsKey("conf"))
                    continue;
                
                if (g.GetNamedNumber("conf") == id)
                {
                    g["conf"] = JsonValue.CreateNumberValue(1);
                    Save(g);
                }
            }
        }

        public void SetConf(JsonObject grp, long id)
        {
            grp["conf"] = JsonValue.CreateNumberValue(id);
            Save(grp);
        }

        public List<long> DeckIdsForConf(JsonObject conf)
        {
            long confId = (long)conf.GetNamedNumber("id");
            return DeckIdsForConf(confId);
        }

        public List<long> DeckIdsForConf(long confId)
        {
            List<long> dids = new List<long>();
            foreach (JsonObject deck in deckDict.Values)
            {
                if (deck.ContainsKey("conf") && (deck.GetNamedNumber("conf") == confId))
                {
                    dids.Add((long)deck.GetNamedNumber("id"));
                }
            }
            return dids;
        }

        public void RestoreToDefault(JsonObject conf)
        {
            int oldOrder = (int) conf.GetNamedObject("new").GetNamedNumber("order");
            JsonObject temp = JsonObject.Parse(defaultConf);
            temp.Add("id", conf.GetNamedValue("id"));
            temp.Add("name", conf.GetNamedValue("name"));
            deckConf.Add((long)conf.GetNamedNumber("id"), temp);
            Save(temp);
            // if it was previously randomized, resort
            if (oldOrder == 0)
            {
                collection.Sched.ResortConf(temp);
            }
        }

        public string GetDeckNameOrNull(long did)
        {
            JsonObject deck = Get(did, false);
            if (deck != null)
                return deck.GetNamedString("name");

            return null;
        }

        public void SetDeck(long[] cids, long did)
        {
            collection.Database.Execute("update cards set did=?,usn=?,mod=? where id in " + Utils.Ids2str(cids),
                    new object[] { did, collection.Usn, DateTimeOffset.Now.ToUnixTimeSeconds() });
        }

        public long[] GetCardIds(long deckId, bool children)
        {
            if(!children)
            {
                var list = collection.Database.QueryColumn<CardIdOnlyTable>("select id from cards where did=" + deckId);
                return (from s in list select s.Id).ToArray();
            }

            List<long> deckIds = new List<long>();
            deckIds.Add(deckId);
            foreach(KeyValuePair<string, long> entry in Children(deckId))
                deckIds.Add(entry.Value);

            string str = Utils.Ids2str(deckIds.ToArray());
            var returnList = collection.Database.QueryColumn<CardIdOnlyTable>("select id from cards where did in " + str);
            return (from s in returnList select s.Id).ToArray();
        }

        public void RecoverOrphans()
        {
            long[] dids = AllIds();
            bool mod = collection.Database.IsModified;
            collection.Database.Execute("update cards set did = 1 where did not in " + Utils.Ids2str(dids));
            collection.Database.IsModified = mod;
        }

        public List<JsonObject> Parents(long did)
        {
            List<string> parents = new List<string>();
            string[] array = Get(did).GetNamedString("name").Split(new string[] { "::" }, 
                                                    StringSplitOptions.None);
            List<string> parts = new List<string>(array);
            for(int i = 0; i < parts.Count - 1; i++)
            {
                if (parents.Count == 0)
                    parents.Add(parts[i]);
                else
                    parents.Add(parents[parents.Count - 1] + "::" + parts[i]);
            }

            List<JsonObject> oParents = new List<JsonObject>();
            for (int i = 0; i < parents.Count; i++)
                oParents.Insert(i, Get(AddOrResuedDeck(parents[i])));

            return oParents;
        }

        public void BeforeUpload()
        {
            foreach (JsonObject d in All())
            {
                d["usn"] = JsonValue.CreateNumberValue(0);
            }
            foreach (JsonObject c in AllConf())
            {
                c["usn"] = JsonValue.CreateNumberValue(0);
            }
            Save();
        }

        public long NewDynamicDeck(string name)
        {
            long? did = AddOrResuedDeck(name, defaultDynamicDeck);
            if (did == null)
                throw new Exception("NewDyn: did is null!");

            long lDid = (long)did;
            Select(lDid);
            return lDid;
        }

        public bool IsDyn(long did)
        {
            return Get(did).GetNamedNumber("dyn") != 0;
        }

        public string GetActualDescription()
        {
            return Current().GetNamedString("desc", "");
        }
    }

    public class DeckRenameException : Exception
    {
        public enum ErrorCode
        {
            ALREADY_EXISTS,
            FILTERED_NOSUBDEKCS
        }

        private ErrorCode error;
        public ErrorCode Error { get { return error; } }

        public DeckRenameException(ErrorCode errorCode)
        : base()
        { this.error = errorCode; }

        public DeckRenameException(ErrorCode errorCode,string message, Exception e)
            : base(message, e)
        { this.error = errorCode; }
    }

    public class DeckNotFoundException : Exception
    {
        public DeckNotFoundException()
            : base()
        { }

        public DeckNotFoundException(string message, Exception e)
            : base(message, e)
        { }
    }
}
