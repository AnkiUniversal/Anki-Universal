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
using System.Text;
using Windows.Storage;
using Windows.Data.Json;
using System.IO;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;

namespace Shared.AnkiCore
{
    public class Collection : IDisposable
    {
        private static readonly Regex clozePatternQ = new Regex(@"{{(?!type:)(.*?)cloze:", RegexOptions.Compiled);
        private static readonly Regex clozePatternA = new Regex(@"{{(.*?)cloze:", RegexOptions.Compiled);
        private static readonly Regex clozeTagStart = new Regex(@"<%cloze:", RegexOptions.Compiled);

        // other options
        public static readonly string defaultConf = "{"
            +
            // review options
            "\"activeDecks\": [1], " + "\"curDeck\": 1, " + "\"newSpread\": " + (int)ReviewType.DISTRIBUTE + ", "
            + "\"collapseTime\": 1200, " + "\"timeLim\": 0, " + "\"estTimes\": true, " + "\"dueCounts\": true, "
            +
            // other config
            "\"curModel\": null, " + "\"nextPos\": 1, " + "\"sortType\": \"noteFld\", "
            + "\"sortBackwards\": false, \"addToCur\": true }"; 

        private const int UNDO_SIZE_MAX = 20;

        public const int UNDO_REVIEW = 0;
        public const int UNDO_BURY_NOTE = 2;
        public const int UNDO_SUSPEND_CARD = 3;
        public const int UNDO_SUSPEND_NOTE = 4;
        public const int UNDO_DELETE_NOTE = 5;
        public const int UNDO_BURY_CARD = 7;

        private DB database;
        private bool isSever;
        private long lastSave;
        private Deck decks;

        private Sched sched;

        // BEGIN: SQL table columns
        private long crt;
        private long timeModified;
        private long scm;
        private bool isDirty;
        private int usn;
        private long ls;
        private JsonObject conf;
        // END: SQL table columns

        private LinkedList<object[]> undoList = new LinkedList<object[]>();

        private StorageFolder folder;
        private string relativePath;
        private bool debugLog;
        
        public long Crt { get { return crt; } set { crt = value; } }
        public StorageFolder Folder { get { return folder; } }
        public DB Database { get { return database; } }
        public bool IsServer { get { return isSever; } set { isSever = value; } }
        public long LastSave { get { return lastSave; } }
        public int Usn
        {
            get
            {
                if (isSever)
                    return usn;
                else
                    return -1;
            }
        }
        //This getter is only for syncing routines, use Usn instead elsewhere
        public Sched Sched { get { return sched; } }
        public Deck Deck { get { return decks; } }
        public bool IsDirty { get { return isDirty; } }
        public string RelativePath { get { return relativePath; } }
        public long TimeModified { get { return timeModified; } }
        public long Ls { get { return ls; } set { ls = value; } }
        public JsonObject Conf { get { return conf; } set { conf = value; } }

        /// <summary>
        /// Collection class constructor
        /// </summary>
        /// <param name="db">Database</param>
        /// <param name="relativePath">Relative path to the collection file</param>
        /// <param name="folder">Folder that stores collection AND Media files</param>
        public Collection(DB db, string relativePath, StorageFolder folder)
            : this(db, relativePath, false, folder) { }

        /// <summary>
        /// Collection class constructor
        /// </summary>
        /// <param name="db">Database</param>
        /// <param name="relativePath">Relative path to the collection file</param>
        /// <param name="server">Is server?</param>
        /// <param name="folder">Folder that stores collection AND Media files</param>
        public Collection(DB db, string relativePath, bool server, StorageFolder folder)
            : this(db, relativePath, false, false, folder) { }

        /// <summary>
        /// Collection class constructor
        /// </summary>
        /// <param name="db">Database</param>
        /// <param name="relativePath">Relative path to the collection file</param>
        /// <param name="server">Is server?</param>
        /// <param name="log">Is log?</param>
        /// <param name="folder">Folder that stores collection AND Media files</param>
        public Collection(DB db, string relativePath, bool server, bool log, StorageFolder folder)
        {
            this.folder = folder;
            debugLog = log;
            database = db;

            this.relativePath = relativePath;
            this.isSever = server;
            this.lastSave = DateTimeOffset.Now.ToUnixTimeSeconds();
            ClearUndo();
            decks = new Deck(this);
            Load();
            if (crt == 0)
            {
                crt = GetDayStart();
            }

            sched = new Sched(this);
        }

        private long GetDayStart()
        {
            var dateOffset = DateTimeOffset.Now;
            TimeSpan FourHoursSpan = new TimeSpan(4, 0, 0);
            dateOffset = dateOffset.Subtract(FourHoursSpan);
            dateOffset = new DateTimeOffset(dateOffset.Year, dateOffset.Month, dateOffset.Day, 
                                            0, 0, 0, dateOffset.Offset);
            dateOffset = dateOffset.Add(FourHoursSpan);
            return dateOffset.ToUnixTimeSeconds();
        }

        /// <summary>
        /// WARNING: java ver call new LinkList<>
        /// python ver set to none
        /// </summary>
        public void ClearUndo()
        {
            undoList = new LinkedList<object[]>();
        }      

        public void Load()
        {
            var list = database.QueryColumn<CollectionTable>(
                    "SELECT crt, mod, scm, dty, usn, ls, " +
                    "conf, models, decks, dconf, tags FROM col");
            if (list == null || list.Count == 0)
                return;
            CollectionTable col = list[0];
            this.crt = col.Crt;
            this.timeModified = col.Mod;
            this.scm = col.Scm;
            this.isDirty = (col.Dirty == 1); // No longer used
            this.usn = col.Usn;
            this.ls = col.Ls;
            this.conf = JsonObject.Parse(col.Conf);
            this.decks.Load(col.Decks, col.DecksConf);
        }

        public void Close(bool save = true)
        {
            database.Close();
            database = null;
        }

        public void ReOpen()
        {
            if (database == null)
            {
                database = new DB(folder.Path + "\\" + RelativePath);
            }
        }

        public int NextID(string type)
        {
            type = "next" + Char.ToUpper(type[0]) + type.Substring(1);
            int id = (int)conf.GetNamedNumber(type, 1);
            conf[type] = JsonValue.CreateNumberValue(id + 1);
            return id;
        }


        public int NoteCount()
        {
            return Database.QueryScalar<int>("SELECT count() FROM notes");
        }

        public int DueOfCardForDid(long did, int due)
        {
            JsonObject conf = decks.ConfForDeckId(did);
            // in order due?
            if (conf.GetNamedObject("new").GetNamedNumber("order") == (double)NewCardInsertOrder.DUE)
            {
                return due;
            }
            else
            {
                // random mode; seed with note ts so all cards of this note get
                // the same random number
                Random r = new Random(due);
                return r.Next(Math.Max(due, 1000) - 1) + 1;
            }
        }

        public bool IsCardEmpty()
        {
            return database.QueryScalar<int>("SELECT 1 FROM cards LIMIT 1") == 0;
        }

        public int CardCount()
        {
            return database.QueryScalar<int>("SELECT count() FROM cards");
        }

        public int CardCount(params long[] deckIds)
        {
            return database.QueryScalar<int>("SELECT count() FROM cards WHERE did IN " + Utils.Ids2str(deckIds));
        }

        public int CardCount(List<long> deckIds)
        {
            return database.QueryScalar<int>("SELECT count() FROM cards WHERE did IN " + Utils.Ids2str(deckIds));
        }   

        [SQLite.Net.Attributes.Table("cards")]
        private class cardDB
        {
            [SQLite.Net.Attributes.Column("ord")]
            public int Ord { get; set; }
            public long Nid { get; set; }
            [SQLite.Net.Attributes.Column("flds")]
            public string Fields { get; set; }
        }
        public string EmptyCardReport(List<long> cids)
        {
            //Warning: The original sql query in both python and java ver is
            //   "select group_concat(ord+1), count(), flds from cards c, notes n " + 
            //   "where c.nid = n.id and c.id in " + Utils.ids2str(cids) + " group by nid"
            //However, value return by count() is NOT used int both ver so our query is simplified here
            var list = database.QueryColumn<cardDB>("select c.nid, c.flds, c.ord from cards c, notes n "
                                                 + "where c.nid = n.id and c.id in " + Utils.Ids2str(cids.ToArray()));
            
            ///// Since we can't use the original sql call because of SQLite.Net restriction
            ///// (or I failed to do it the right way with SQLite.Net)
            ///// this linq is a replacement for function group_concat in SQLite
            var listCard = from c in list
                           group c by c.Nid into g
                           select new
                           {
                               //Choose the longest fields
                               Fields = g.Aggregate((max, cur) => max.Fields.Length > cur.Fields.Length ? max : cur).Fields,
                               //Add +1 and concat ord in to one string as in the original sql
                               Ord = string.Join(",", g.Select(s => (s.Ord + 1).ToString()))
                           };

            StringBuilder rep = new StringBuilder();
            foreach (var c in listCard)
                rep.Append(String.Format("Empty card numbers: {0}\nFields: {1}\n\n", c.Ord, c.Fields.Replace("\u001F", " / ")));
            
            return rep.ToString();
        }
   
        public void SetTimeLimit(long seconds)
        {
            conf["timeLim"] = JsonValue.CreateNumberValue(seconds);
        }

        public long GetTimeLimit()
        {
            return (long)conf.GetNamedNumber("timeLim");
        }

        public void Dispose()
        {
            Close();
        }
    }

    /// <summary>
    /// Class used to get data from col table in database.
    /// </summary>
    [SQLite.Net.Attributes.Table("col")]
    public class CollectionTable
    {
        [SQLite.Net.Attributes.Column("crt")]
        public long Crt { get; set; }
        [SQLite.Net.Attributes.Column("mod")]
        public long Mod { get; set; }
        [SQLite.Net.Attributes.Column("scm")]
        public long Scm { get; set; }
        [SQLite.Net.Attributes.Column("dty")]
        public int Dirty { get; set; } //No longer used
        [SQLite.Net.Attributes.Column("usn")]
        public int Usn { get; set; }
        [SQLite.Net.Attributes.Column("ls")]
        public long Ls { get; set; }
        [SQLite.Net.Attributes.Column("conf")]
        public string Conf { get; set; }
        [SQLite.Net.Attributes.Column("models")]
        public string Models { get; set; }
        [SQLite.Net.Attributes.Column("decks")]
        public string Decks { get; set; }
        [SQLite.Net.Attributes.Column("dconf")]
        public string DecksConf { get; set; }
        [SQLite.Net.Attributes.Column("tags")]
        public string Tags { get; set; }
    }

    public class ConfirmModSchemaException : Exception
    {
        public ConfirmModSchemaException() : base() { }
    }

}
