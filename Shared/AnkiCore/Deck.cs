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

namespace Shared.AnkiCore
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
                if (d.Value.GetNamedString("name").Equals(name, StringComparison.OrdinalIgnoreCase))
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
                    s.Append(Constant.SUBDECK_SEPERATE);
                    s.Append(path[i]);
                }

                long? did = AddOrResuedDeck(s.ToString());
                s.Clear();
                s.Append(GetDeckName(did));
            }
            s.Append(Constant.SUBDECK_SEPERATE);
            s.Append(path[path.Length - 1]);
            return s.ToString();
        }

        private string[] SplitPath(string name)
        {
            string[] sep = new string[] { Constant.SUBDECK_SEPERATE };
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

        public void Select(long did, bool isSetchanged = true)
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
        }

        public Dictionary<string, long> Children(long did)
        {
            string name;
            name = Get(did).GetNamedString("name");
            Dictionary<string, long> actv = new Dictionary<string, long>();
            foreach (JsonObject g in All())
            {
                string deckName = g.GetNamedString("name");
                if (deckName.StartsWith(name + Constant.SUBDECK_SEPERATE))
                    actv.Add(deckName, (long)g.GetNamedNumber("id"));
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

        private string BaseName(string name)
        {
            string[] path = SplitPath(name);
            return path[path.Length - 1];
        }

        private bool CanDragAndDrop(string draggedDeckName, string ontoDeckName)
        {
            if (draggedDeckName.Equals(ontoDeckName, StringComparison.OrdinalIgnoreCase)
                    || IsOneOfDeckIsParent(ontoDeckName, draggedDeckName)
                    || IsAncestor(draggedDeckName, ontoDeckName))
            {
                return false;
            }
            else
            {
                return true;
            }
        }
        
        private bool IsOneOfDeckIsParent(string parentDeckName, string childDeckName)
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

        public bool IsParent(string parentDeckName, string childDeckName)
        {           
            var childSplit = childDeckName.Split(new string[] { Constant.SUBDECK_SEPERATE }, StringSplitOptions.RemoveEmptyEntries);
            if (childSplit.Length < 2)
                return false;

            var parentSplit = parentDeckName.Split(new string[] { Constant.SUBDECK_SEPERATE }, StringSplitOptions.RemoveEmptyEntries);
            if (childSplit.Length <= parentSplit.Length)
                return false;
            var baseParentName = parentSplit[parentSplit.Length - 1];

            var realParent = childSplit[childSplit.Length - 2];
            if (baseParentName.Equals(realParent, StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
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

        public string GetDeckNameOrNull(long did)
        {
            JsonObject deck = Get(did, false);
            if (deck != null)
                return deck.GetNamedString("name");

            return null;
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

        public bool HasParent(long did)
        {
            return Get(did).GetNamedString("name").Contains(Constant.SUBDECK_SEPERATE);            
        }

        public bool HasParent(string deckName)
        {
            return deckName.Contains(Constant.SUBDECK_SEPERATE);
        }

        public List<JsonObject> Parents(long did)
        {
            List<string> parents = new List<string>();
            string[] array = Get(did).GetNamedString("name").Split(new string[] { Constant.SUBDECK_SEPERATE }, 
                                                    StringSplitOptions.None);
            List<string> parts = new List<string>(array);
            for(int i = 0; i < parts.Count - 1; i++)
            {
                if (parents.Count == 0)
                    parents.Add(parts[i]);
                else
                    parents.Add(parents[parents.Count - 1] + Constant.SUBDECK_SEPERATE + parts[i]);
            }

            List<JsonObject> oParents = new List<JsonObject>();
            for (int i = 0; i < parents.Count; i++)
                oParents.Insert(i, Get(AddOrResuedDeck(parents[i])));

            return oParents;
        }

        public void Remove(long deckId, bool cardsToo = false, bool childrenToo = true)
        {
            JsonObject deck;
            if (deckId == 1)
            {
                // we won't allow the default deck to be deleted, but if it's a
                // child of an existing deck then it needs to be renamed
                deck = Get(deckId);
                if (deck.GetNamedString("name").Contains(Constant.SUBDECK_SEPERATE))
                {
                    deck["name"] = JsonValue.CreateStringValue("Default");
                }
                return;
            }

            if (!deckDict.ContainsKey(deckId))
                return;

            deck = Get(deckId);
            if (deck.GetNamedNumber("dyn") != 0)
            {
                RemoveIfChildrenToo(deckId, childrenToo, cardsToo);
            }
            else
            {
                RemoveIfChildrenToo(deckId, childrenToo, cardsToo);
            }

            deckDict.Remove(deckId);
            EnsureHaveActiveDeck(deckId);
        }

        private void RemoveIfChildrenToo(long deckId, bool childrenToo, bool cardsToo)
        {
            if (childrenToo)
                foreach (long id in Children(deckId).Values)
                    Remove(id, cardsToo);
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

        //WARNING: Not in java and python ver
        public long? TryGetOriginalDeckId(long deckId)
        {
            if (!IsDyn(deckId))
                return null;

            var cards = collection.Database.QueryFirstRow<CardTable>("Select * from cards where did = ?", deckId);
            if (cards.Count == 0 || cards[0].ODid == 0)
                return null;

            return cards[0].ODid;
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
