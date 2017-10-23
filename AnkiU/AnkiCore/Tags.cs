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
using System.Text.RegularExpressions;
using Windows.Data.Json;

namespace AnkiU.AnkiCore
{
    public class Tags
    {
        private readonly static Regex canonifyRegex = new Regex("[\"']", RegexOptions.Compiled);

        private Collection collection;
        private bool isChanged;
        private JsonObject tags;

        public JsonObject GetTags()
        {
            return tags;
        }

        public Tags(Collection collection)
        {
            this.collection = collection;
        }

        public void Load(string json)
        {
            tags = JsonObject.Parse(json);
            isChanged = false;
        }

        public void SaveChangesToDatabase()
        {
            if(isChanged)
            {
                collection.Database.Execute("update col set tags=?", Utils.JsonToString(tags));
                isChanged = false;
            }
        }

        public void Register(IEnumerable<string> tags, int? usn = null)
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

        public void RegisterNotes(long[] nids = null)
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

            var notes = collection.Database.QueryColumn<NoteTable>("SELECT DISTINCT tags FROM notes" + lim);
            string[] tags = (from s in notes select s.Tags).ToArray();
            
            Register(Split(String.Join(" ", tags)));
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
                foreach(long id in collection.Deck.Children(deckId).Values)
                    dids.Add(deckId);

                sql.Append(" AND c.did IN ");
                sql.Append(Utils.Ids2str(dids.ToArray()));
            }
            else
            {
                sql.Append(" AND c.did = ");
                sql.Append(deckId.ToString());
            }
            var cards = collection.Database.QueryColumn<NoteTable>(sql.ToString());
            string[] tags = (from s in cards select s.Tags).ToArray();
            return Split(String.Join(" ", tags));
        }

        /// <summary>
        /// Parse a string and return a list of tags.
        /// </summary>
        /// <param name="tags"></param>
        /// <returns></returns>
        public List<string> Split(string tags)
        {
            List<string> list = new List<string>();
            var temp = tags.Replace('\u3000', ' ').Split(new string[] { " " }, StringSplitOptions.None);
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
            if (newTags == null || (newTags.Count == 0))
            {
                return;
            }
            // cache tag names
            Register(newTags);
            // find notes missing the tags
            string l;
            if (add)
            {
                l = "tags not ";
            }
            else
            {
                l = "tags ";
            }
            StringBuilder lim = new StringBuilder();
            foreach (string t in newTags)
            {
                if (lim.Length != 0)
                {
                    lim.Append(" or ");
                }
                lim.Append(l);
                lim.Append("like '% ");
                lim.Append(t);
                lim.Append(" %'");
            }

            List<long> nids = new List<long>();
            List<object[]> res = new List<object[]>();
            var listNotes = collection.Database.QueryColumn<NoteTable>
                            ("select id, tags from notes where id in " + Utils.Ids2str(ids) +
                            " and (" + lim.ToString() + ")");
            if (add)
            {
                foreach (NoteTable n in listNotes)
                {
                    nids.Add(n.Id);
                    res.Add(new object[] { AddToStr(tags, n.Tags),
                                           DateTimeOffset.Now.ToUnixTimeSeconds(),
                                           collection.Usn, n.Id });
                }
            }
            else
            {
                foreach (NoteTable n in listNotes)
                {
                    nids.Add(n.Id);
                    res.Add(new object[] { RemoveFromStr(tags, n.Tags),
                                           DateTimeOffset.Now.ToUnixTimeSeconds(),
                                           collection.Usn, n.Id });
                }
            }

            // update tags
            collection.Database.ExecuteMany("update notes set tags=:t,mod=:n,usn=:u where id = :id", res);
        }

        private Dictionary<string, object> FixRow(List<NoteTable> res, SomeFunc fn, string tags)
        {
            Dictionary<string, object> dict = new Dictionary<string, object>();
            foreach (NoteTable row in res)
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
                for(int i = 0; i < currentTags.Count; i++)
                {
                    if (tag.Equals(currentTags[i], StringComparison.OrdinalIgnoreCase))
                    {
                        currentTags.RemoveAt(i);
                        break;
                    }
                }                
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
                string s = canonifyRegex.Replace(t, "");
                foreach(string existingTag in this.tags.Keys)
                {
                    if (s.Equals(existingTag, StringComparison.OrdinalIgnoreCase))
                    {
                        s = existingTag;
                        break;
                    }
                }
                strippedTags.Add(s);
            }
            return strippedTags;
        }

        public void BeforeUpload()
        {
            var keys = this.tags.Keys.ToArray();
            foreach (string k in keys)
            {
                this.tags[k] = JsonValue.CreateNumberValue(0);
            }
            Save();
        }

        public void Add(string key, int value)
        {
            this.tags[key] = JsonValue.CreateNumberValue(value);
        }

        public void RemoveTagFromNotesAndCollection(List<long> ids, string tags)
        {
            List<string> tagsToRemove = Split(tags);
            if (tagsToRemove == null || (tagsToRemove.Count == 0))            
                return;            

            List<object[]> res = new List<object[]>();
            var listNotes = collection.Database.QueryColumn<NoteTable>
                            ("select id, tags from notes where id in " + Utils.Ids2str(ids));

            foreach (NoteTable n in listNotes)
            {
                res.Add(new object[] {RemoveFromStr(tags, n.Tags),
                                      DateTimeOffset.Now.ToUnixTimeSeconds(),
                                      collection.Usn, n.Id });
            }

            collection.Database.ExecuteMany("update notes set tags=:t,mod=:n,usn=:u where id = :id", res);
            foreach(var tag in tagsToRemove)
            {
                if (this.tags.ContainsKey(tag))
                    this.tags.Remove(tag);
            }
            isChanged = true;            
            collection.Tags.SaveChangesToDatabase();
        }

        public void RenameTag(List<long> ids, string oldName, string newName)
        {
            if (newName.Contains(" "))
                throw new Exception("New tag name should not have white space!");
            if(this.tags.ContainsKey(newName))
                throw new Exception("A tag with the same name already exists!");

            List<object[]> res = new List<object[]>();
            var listNotes = collection.Database.QueryColumn<NoteTable>
                            ("select id, tags from notes where id in " + Utils.Ids2str(ids));

            foreach (NoteTable n in listNotes)
            {
                string newTags = RenameTagInTags(oldName, newName, n);

                res.Add(new object[] {RemoveFromStr(newTags, n.Tags),
                                      DateTimeOffset.Now.ToUnixTimeSeconds(),
                                      collection.Usn, n.Id });
            }

            collection.Database.ExecuteMany("update notes set tags=:t,mod=:n,usn=:u where id = :id", res);
            if (this.tags.ContainsKey(oldName))
            {                
                this.tags.Remove(oldName);
                Add(newName, 0);
            }            
            isChanged = true;
            collection.Tags.SaveChangesToDatabase();
        }

        private string RenameTagInTags(string oldName, string newName, NoteTable n)
        {
            List<string> currentTags = Split(n.Tags);
            for (int i = 0; i < currentTags.Count; i++)
            {
                if (currentTags[i].Equals(oldName, StringComparison.Ordinal))
                {
                    currentTags[i] = newName;
                    break;
                }
            }
            string newTags = Join(currentTags);
            return newTags;
        }
    }
}
