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
using AnkiU.Anki;

namespace AnkiU.AnkiCore
{
    public class Note
    {
        private Collection collection;

        private long id;
        private string guId;
        private JsonObject model;
        private long modelId;
        private List<string> tags;
        private string[] fields;
        private int flags;
        private string data;
        private Dictionary<string, KeyValuePair<int, JsonObject>> map;
        private long scm;
        private int usn;
        private long timeModified;
        private bool newlyAdded;      

        public Collection Collection { get { return collection; } }

        public long Id { get { return id; } }
        public JsonObject Model { get { return model; } }
        public long ModelId { get { return modelId; } }
        public string[] Fields { get { return fields; } set { fields = value; } }
        public List<string> Tags { get { return tags; } set { tags = value; } }
        public string JointFields { get { return String.Join(" ", fields); } }
        public long TimeModified { get { return timeModified; } }

        //Not in java and python ver
        public long DupeNoteId { get; set; }

        public void SetField(int index, string value)
        {
            fields[index] = value;
        }

        public Note(Collection col, long id)
            : this(col, null, id) { }

        public Note(Collection col, JsonObject model)
            : this(col, model, null) { }

        public Note(Collection col, JsonObject model, long? id)
        {
            Debug.Assert(!(model != null && id != null));
            collection = col;
            if (id != null)
            {
                this.id = (long)id;
                LoadFromDatabase();
            }
            else
            {
                this.id = Utils.TimestampID(collection.Database, "notes");
                guId = Utils.Guid64();
                this.model = model;
                this.modelId = (long)model.GetNamedNumber("id");
                this.tags = new List<string>();
                this.fields = new string[model.GetNamedArray("flds").Count];
                for(int i = 0; i < fields.Length; i++)
                    fields[i] = "";
                this.flags = 0;
                this.data = "";
                this.map = col.Models.FieldMap(this.model);
                this.scm = col.Scm;
            }
        }

        public void LoadFromDatabase()
        {
            var list = collection.Database.QueryColumn<NoteTable>(
                        "SELECT guid, mid, mod, usn, tags, flds, flags, data FROM notes WHERE id = " + id);
            if (list == null)
            {
                throw new Exception("Notes.load: No result from query for note " + id);
            }
            guId = list[0].GuId;
            modelId = list[0].Mid;
            timeModified = list[0].TimeModified;
            usn = list[0].Usn;
            tags = collection.Tags.Split(list[0].Tags);
            fields = Utils.SplitFields(list[0].Fields);
            flags = list[0].Flags;
            data = list[0].Data;
            model = collection.Models.Get(modelId);
            map = collection.Models.FieldMap(model);
            scm = collection.Scm;
        }

        /// <summary>
        /// If fields or tags have changed, write changes to disk.
        /// </summary>
        /// <param name="mod"></param>
        /// <param name="changeUsn"></param>
        public void SaveChangesToDatabase(long? mod = null, bool changeUsn = true)
        {
            Debug.Assert(scm == collection.Scm);
            PreFlush();
            if (changeUsn)
            {
                usn = collection.Usn;
            }
            string sfld = Utils.StripHTMLMedia(this.fields[collection.Models.SortIdx(model)]);
            string tags = StringTags();
            string fields = JoinedFields();
            if (mod == null && collection.Database.QueryScalar<int>
                               ("select 1 from notes where id = ? and tags = ? and flds = ?", this.id, tags, fields) > 0)
            {
                return;
            }
            //WARNING: In AnkiU we always include content into <div> block
            //so before checksum we have to remove and trim text first            
            long csum = Utils.FieldChecksum(HtmlEditor.RemoveDivWrap(this.fields[0]).Trim());
            this.timeModified = (mod != null) ? (long)mod : DateTimeOffset.Now.ToUnixTimeSeconds();
            collection.Database.Execute("insert or replace into notes values (?,?,?,?,?,?,?,?,?,?,?)",
                    new object[] { this.id, guId, this.modelId, this.timeModified, usn, tags, fields, sfld, csum, this.flags, this.data });
            collection.Tags.Register(this.tags);
            PostFlush();
        }

        /// <summary>
        /// Have we been added yet
        /// </summary>
        private void PreFlush()
        {
            newlyAdded = collection.Database.QueryScalar<int>("SELECT 1 FROM cards WHERE nid = " + id) == 0;
        }

        /// <summary>
        /// Generate missing cards
        /// </summary>
        private void PostFlush()
        {
            if (!newlyAdded)
            {
                collection.GenCards(new long[] { id });
            }
        }

        public string JoinedFields()
        {
            return Utils.JoinFields(fields);
        }

        public List<Card> Cards()
        {
            List<Card> cards = new List<Card>();
            
            var list = collection.Database.QueryColumn<CardIdOnlyTable>
                       ("SELECT id FROM cards WHERE nid = " + id + " ORDER BY ord");
            foreach (CardIdOnlyTable c in list)
            {
                cards.Add(collection.GetCard(c.Id));
            }
            return cards;
        }

        public string[] Keys()
        {
            return map.Keys.ToArray();
        }

        public string[] Values()
        {
            return fields;
        }

        public string[,] Items()
        {
            // TODO: Check if this method's returned field orders differs from python ver.
            // The items here are only used in the note editor, so it's a low priority.
            string[,] result = new string[map.Count, 2];
            foreach (string fname in map.Keys)
            {
                int i = map[fname].Key;
                result[i, 0] = fname;
                result[i, 1] = fields[i];
            }
            return result;
        }

        private int FieldOrd(string key)
        {
            return map[key].Key;
        }

        public string GetItem(string key)
        {
            return fields[FieldOrd(key)];
        }

        /// <summary>
        /// Set value of the specified field
        /// </summary>
        /// <param name="Field">Name of field</param>
        /// <param name="value">New value of the field</param>
        public void SetItem(string Field, string value)
        {
            fields[FieldOrd(Field)] = value;
        }

        public bool ContainsKey(String key)
        {
            return map.ContainsKey(key);
        }

        public bool HasTag(string tag)
        {
            return collection.Tags.IsInList(tag, tags);
        }

        public string StringTags()
        {
            return collection.Tags.Join(collection.Tags.Canonify(tags));
        }

        public void SetTags(string str)
        {
            tags = collection.Tags.Split(str);
        }

        public void DelTag(string tag)
        {
            LinkedList<string> rem = new LinkedList<string>();
            foreach (string t in tags)
            {
                if (t.Equals(tag, StringComparison.OrdinalIgnoreCase))
                {
                    rem.AddLast(t);
                }
            }
            foreach (string r in rem)
            {
                tags.Remove(r);
            }
        }

        public enum FirstField
        {
            Valid = 0,
            Empty = 1,
            Duplicate = 2
        }

        /// <summary>
        /// Check first field of a note is empty or dupe with other or not
        /// </summary>
        /// <returns></returns>
        public FirstField DupeOrEmpty()
        {            
            string text = HtmlEditor.RemoveDivWrap(fields[0]).Trim();
            if (text.Length == 0)
            {
                return FirstField.Empty;
            }
            long csum = Utils.FieldChecksum(text);
            var listNote = collection.Database.QueryColumn<NoteTable>
                        ("SELECT flds, id FROM notes WHERE csum = "
                        + csum + " AND id != " + (id != 0 ? id : 0) + " AND mid = " + modelId);            
            // find any matching csums and compare
            foreach (var note in listNote)
            {
                string compared = HtmlEditor.RemoveDivWrap(Utils.SplitFields(note.Fields)[0]).Trim();
                if (Utils.StripHTMLMedia(compared)
                    .Equals(Utils.StripHTMLMedia(text)))
                {
                    DupeNoteId = note.Id;
                    return FirstField.Duplicate;
                }
            }
            return FirstField.Valid;
        }

        public Note ShallowClone()
        {
            var clone = this.MemberwiseClone() as Note;
            if (clone == null)
                throw new Exception("Can not clone this Note!");
            return clone;
        }

        public string GetSFld()
        {
            return collection.Database.QueryScalar<string>("SELECT sfld FROM notes WHERE id = " + id);
        }

    }

    /// <summary>
    /// Class used to get data from notes table in database.
    /// Avoid using Note class to reduce unwanted fields.
    /// </summary>
    [SQLite.Net.Attributes.Table("notes")]
    public class NoteTable
    {
        [SQLite.Net.Attributes.Column("tags")]
        public string Tags { get; set; }

        [SQLite.Net.Attributes.Column("id")]
        public long Id { get; set; }

        [SQLite.Net.Attributes.Column("flds")]
        public string Fields { get; set; }

        [SQLite.Net.Attributes.Column("sfld")]
        public string Sortfields { get; set; }

        [SQLite.Net.Attributes.Column("guid")]
        public string GuId { get; set; }

        [SQLite.Net.Attributes.Column("mid")]
        public long Mid { get; set; }

        [SQLite.Net.Attributes.Column("flags")]
        public int Flags { get; set; }

        [SQLite.Net.Attributes.Column("data")]
        public string Data { get; set; }

        [SQLite.Net.Attributes.Column("usn")]
        public int Usn { get; set; }

        [SQLite.Net.Attributes.Column("mod")]
        public long TimeModified { get; set; }

        [SQLite.Net.Attributes.Column("csum")]
        public long CheckSum { get; set; }

        public NoteTable() { }
    }
}
