using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Text.RegularExpressions;
using Windows.Data.Json;

namespace AnkiU.AnkiCore
{
    class Tags
    {
        private readonly static Regex sCanonify = new Regex("[\"']", RegexOptions.Compiled);

        private Collection collection;
        private bool isChanged;
        private JsonObject tags;

        public Tags(Collection collection)
        {
            this.collection = collection;
        }

        public void Load(string json)
        {
            tags = JsonObject.Parse(json);
            isChanged = false;
        }

        public void Flush()
        {
            if(isChanged)
            {
                collection.Database.Execute("update col set tags=?", Utils.JsonToString(tags));
                isChanged = false;
            }
        }

        public void Register(List<string> tags, int? usn = null)
        {
            //bool found = false;
            foreach (string t in tags)
            {
                if (!this.tags.ContainsKey(t))
                {
                    int addUsn = (int)(usn == null ? collection.Usn : usn);
                    this.tags.Add(t, JsonValue.CreateNumberValue(addUsn));
                    isChanged = true;
                    //found = true;
                }
            }
            //TODO:
            //if(found)
            //    runHook("newTag")
        }

        //TODO: check if we really need a list
        public List<string> All()
        {
            return this.tags.Keys.ToList();
        }

        public void RegisterNotes(long[] nids)
        {
            string lim;
            if(nids != null)
                lim = " WHERE id IN " + Utils.Ids2str(nids);
            else
            {
                lim = "";
                this.tags.Clear();
                isChanged = true;
            }

            var notes = collection.Database.QueryColumn<notes>("SELECT DISTINCT tags FROM notes" + lim);
            string[] tags = (from s in notes select s.Tags).ToArray();
            
            Register(Split(String.Join(" ", tags)));
        }

        public void AllItems()
        {
            //TODO
        }

        public void Save()
        {
            isChanged = true;
        }

        /// <summary>
        /// Returns the tags of the cards in the deck
        /// </summary>
        /// <param name="deckId"></param>
        /// <param name="children">Whether to include the deck's children</param>
        /// <returns>A list of tags</returns>
        public List<string> ByDeck(long deckId, bool children)
        {
            StringBuilder sql = new StringBuilder();
            sql.Append("SELECT DISTINCT n.tags from cards c, notes n WHERE c.nid = n.id");
            if(children)
            {
                List<long> dids = new List<long>();
                dids.Add(deckId);
                foreach(long id in collection.Decks.Children(deckId).Values)
                    dids.Add(deckId);

                sql.Append(" AND c.did IN ");
                sql.Append(Utils.Ids2str(dids.ToArray()));
            }
            else
            {
                sql.Append(" AND c.did = ");
                sql.Append(deckId.ToString());
            }
            var cards = collection.Database.QueryColumn<notes>(sql.ToString());
            string[] tags = (from s in cards select s.Tags).ToArray();
            return Split(String.Join(" ", tags));
        }

        /// <summary>
        /// Parse a string and return a list of tags.
        /// </summary>
        /// <param name="tags"></param>
        /// <returns></returns>
        public List<string> Split(String tags)
        {
            List<string> list = new List<string>();
            var temp = tags.Replace('\u3000', ' ').Split(new string[] { "\\s" }, StringSplitOptions.RemoveEmptyEntries);
            foreach (string s in temp)
                if (s.Length > 0)
                    list.Add(s);

            return list;
        }

        delegate string SomeFunc(string aTag, string bTag);

        /// <summary>
        /// TODO: This function will need to be tested carefully.
        /// Since the java source code is not correct,
        /// the implementation is based entirely on the python source
        /// </summary>
        /// <param name="ids"></param>
        /// <param name="tags"></param>
        /// <param name="add"></param>
        public void BulkAdd(List<long> ids, string tags, bool add = true)
        {
            List<string> newTags = Split(tags);
            if (newTags == null)
                return;

            //cache tag names
            Register(newTags);

            // find notes missing the tags
            string l;
            SomeFunc fn;
            if(add)
            {
                l = "tags not";
                fn = AddToStr;
            }
            else
            {
                l = "tags";
                fn = RemoveFromStr;
            }

            StringBuilder lim = new StringBuilder();
            int count = 0;
            string str;
            Dictionary<string, string> tagDict = new Dictionary<string, string>();
            foreach(string t in newTags)
            {
                if (lim.Length != 0)
                    lim.Append(" or ");

                lim.Append(l);
                str = "like :_" + count;
                lim.Append(str);

                tagDict.Add(str, String.Format("% {0} %", t));
                count++;
            }
            string sql = String.Format("select id, tags from notes where id in {0} and ({1})", 
                                        Utils.Ids2str(ids.ToArray()), lim);

            var res = collection.Database.QueryColumn<notes>(sql, tagDict);
            //update tags
            Dictionary<string, object> fixNotes = FixRow(res, fn, tags);
            collection.Database.ExecuteMany("update notes set tags=:t,mod=:n,usn=:u where id = :id",
                                                fixNotes);
        }

        private Dictionary<string, object> FixRow(List<notes> res, SomeFunc fn, string tags)
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            foreach (notes row in res)
            {
                dict.Add("id", row.Id);
                dict.Add("t", fn(tags, row.Tags));
                dict.Add("n", DateTimeOffset.Now.ToUnixTimeSeconds());
                dict.Add("u", collection.Usn);
            }
            return dict;
        }

        public void BulkRem(List<long> ids, string tags)
        {
            BulkAdd(ids, tags, false);
        }

        public string AddToStr(string addTags, string tags)
        {
            var currentTags = Split(tags);
            foreach(string tag in Split(addTags))
            {
                if (!IsInList(tag, currentTags))
                    currentTags.Add(tag);
            }

            return Join(Canonify(currentTags));
        }

        public string RemoveFromStr(string delTags, string tags)
        {
            List<string> currentTags = Split(tags);
            foreach(string tag in Split(delTags))
            {
                List<string> remove = new List<string>();
                foreach(string tx in currentTags)
                    if (tag.Equals(tx, StringComparison.OrdinalIgnoreCase))
                        remove.Add(tx);

                foreach (string r in remove)
                    currentTags.Remove(r);
            }
            return Join(currentTags);
        }

        public bool IsInList(string tag, List<string> tags)
        {
            foreach (string t in tags)
                if (t.Equals(tag, StringComparison.OrdinalIgnoreCase))
                    return true;

            return false;
        }

        public string Join(ICollection<string> tags)
        {
            if (tags == null || tags.Count == 0)
                return "";
            else
            {
                StringBuilder str = new StringBuilder();
                str.Append(" ");
                str.Append(String.Join(" ", tags));
                str.Append(" ");
                return str.ToString();
            }
        }

        /// <summary>
        /// Strip duplicates, adjust case to match existing tags, and sort
        /// </summary>
        /// <param name="tagList"></param>
        /// <returns></returns>
        public SortedSet<string> Canonify(List<string> tagList)
        {
            // NOTE: The python version creates a list of tags, puts them into a set, then sorts them. The SortedSet
            // used in C# guarantees uniqueness and sort order, so we return it as-is without those steps.
            SortedSet<string> strippedTags = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(string t in tagList)
            {
                string s = sCanonify.Replace(t, "");
                foreach(string existingTag in this.tags.Keys)
                {
                    if (s.Equals(existingTag, StringComparison.OrdinalIgnoreCase))
                        s = existingTag;
                }
                strippedTags.Add(s);
            }
            return strippedTags;
        }

        public void BeforeUpload()
        {
            foreach (string k in this.tags.Keys)
            {
                this.tags.Add(k, JsonValue.CreateNumberValue(0));
            }
            Save();
        }

        public void Add(string key, int value)
        {
            this.tags.Add(key, JsonValue.CreateNumberValue(value));
        }
    }
}
