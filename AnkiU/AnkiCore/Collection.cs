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

namespace AnkiU.AnkiCore
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
        private Media media;
        private Deck decks;
        private Models models;
        private Tags tags;

        private Sched sched;

        private double startTime;
        private int startReps;

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
        private StreamWriter logHnd;
        
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
        public int GetUsnForSync { get { return usn; } }
        public int SetUsnAfterSync { set { usn = value; } }
        public Models Models { get { return models; } }
        public Media Media { get { return media; } }
        public Tags Tags { get { return tags; } }
        public long Scm { get { return scm; } set { scm = value; } }
        public Sched Sched { get { return sched; } }
        public Deck Deck { get { return decks; } }
        public bool IsDirty { get { return isDirty; } }
        public string RelativePath { get { return relativePath; } }
        public long TimeModified { get { return timeModified; } }
        public long Ls { get { return ls; } set { ls = value; } }
        public JsonObject Conf { get { return conf; } set { conf = value; } }

        public delegate void ConfirmModSchema();
        public event ConfirmModSchema ConfirmModSchemaEvent;

        /// <summary>
        ///  Set IsModified of DB to true. 
        ///  DB operations and the deck/tag/model managers do this automatically, so this is only necessary
        ///  if you modify properties of this object or the conf dict.
        /// </summary>
        public void SetIsModified()
        {
            database.IsModified = true;
        }

        public Note GetNote(long id)
        {
            return new Note(this, id);
        }

        public Card GetCard(long id)
        {
            return new Card(this, id);
        }

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
            OpenLog();
            Log(args: new object[] { relativePath, ApplicationData.Current.Version });
            this.isSever = server;
            this.lastSave = DateTimeOffset.Now.ToUnixTimeSeconds();
            ClearUndo();
            media = new Media(this, server, folder);
            models = new Models(this);
            decks = new Deck(this);
            tags = new Tags(this);
            Load();
            if (crt == 0)
            {
                crt = GetDayStart();
            }
            startReps = 0;
            startTime = 0;
            sched = new Sched(this);
            if (!conf.GetNamedBoolean("newBury", false))
            {
                conf["newBury"] = JsonValue.CreateBooleanValue(true);
                SetIsModified();
            }
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

        private void OpenLog()
        {
            if (!debugLog)
            {
                return;
            }
            Task task = Task.Factory.StartNew(async () =>
            {
                try
                {
                    string rlPath = relativePath.ReplaceFirst(".anki2", ".log");
                    StorageFile lpath = null;

                    lpath = await folder.TryGetItemAsync(rlPath) as StorageFile;
                    if (lpath != null)
                    {
                        var fprop = (await lpath.GetBasicPropertiesAsync()).Size;
                        if (fprop > 10 * 1024 * 1024)
                        {
                            await lpath.RenameAsync(rlPath + ".old", NameCollisionOption.ReplaceExisting);
                        }
                    }
                    FileStream file = new FileStream(folder.Path + "\\" + rlPath, FileMode.OpenOrCreate);
                    logHnd = new StreamWriter(file, Encoding.UTF8);
                }
                catch (FieldAccessException)
                {
                    // turn off logging if we can't open the log file
                    Debug.WriteLine("Failed to open collection.log file - disabling logging");
                    debugLog = false;
                }
            });
            task.Wait();
        }

        private void CloseLog()
        {
            if (logHnd != null)
            {
                logHnd.Dispose();
                logHnd = null;
            }
        }

        public void Load()
        {
            var list = database.QueryColumn<CollectionTable>(
                    "SELECT crt, mod, scm, dty, usn, ls, " +
                    "conf, models, decks, dconf, tags FROM col");
            if (list == null)
                return;
            CollectionTable col = list[0];
            this.crt = col.Crt;
            this.timeModified = col.Mod;
            this.scm = col.Scm;
            this.isDirty = (col.Dirty == 1); // No longer used
            this.usn = col.Usn;
            this.ls = col.Ls;
            this.conf = JsonObject.Parse(col.Conf);
            this.models.Load(col.Models);
            this.decks.Load(col.Decks, col.DecksConf);
            this.tags.Load(col.Tags);
        }

        /// <summary>
        /// Flush state to DB, updating mod time
        /// </summary>
        /// <param name="mod"></param>
        public void Flush(long mod = 0)
        {
            this.timeModified = (mod == 0 ? DateTimeOffset.Now.ToUnixTimeMilliseconds() : mod);
            database.Execute("update col set crt =?, mod =?, scm =?, dty =?, usn =?, ls =?, conf =?",
                               this.Crt, 
                               this.timeModified, 
                               this.scm, 
                               this.isDirty ? 1 : 0, 
                               this.usn, 
                               this.ls, 
                               Utils.JsonToString(conf));
        }

        private static readonly object syncLock = new object();
        public void Save(long mod = 0, string name = null)
        {
            lock (syncLock)
            {
                // let the managers conditionally flush
                models.SaveChangesToDatabse();
                decks.SaveChangesToDatabase();
                tags.SaveChangesToDatabase();
                // and flush deck + bump mod if db has been changed
                if (database.IsModified)
                {
                    Flush(mod);
                    Lock();
                    database.IsModified = false;
                }
                lastSave = DateTimeOffset.Now.ToUnixTimeSeconds();
            }
        }

        public void SaveAndCommit()
        {
            Save();
            database.Commit();
        }

        public void SaveAndCommitAsync()
        {
            var task = Task.Run(() =>
            {
                Save();
                database.Commit();
            });
        }

        /// <summary>
        /// Make sure we don't accidentally bump mod time
        /// </summary>
        public void Lock()
        {
            bool isModified = database.IsModified;
            database.Execute("UPDATE col SET mod=mod");
            database.IsModified = isModified;
        }

        public void Close(bool save = true)
        {
            lock (syncLock)
            {
                try
                {
                    if (database != null)
                    {
                        if (save)
                        {
                            database.RunInTransaction(() =>
                           {
                               Save();
                               // Both java and python differs with each other
                               // in how to implement this.
                               // Since our lib support nested transaction
                               // this is not necessary
                               //else
                               //    RollBack();
                               //if (!isSever)
                               //        database.Execute("pragma journal_mode = delete");
                           });
                        }
                        database.Close();
                        database = null;
                        if (media != null)
                            media.Close();
                        CloseLog();
                    }
                }
                catch(SQLite.Net.SQLiteException e)
                {
                    Debug.WriteLine(e.Message);
                }
            }
        }

        public void ReOpen()
        {
            if (database == null)
            {
                database = new DB(folder.Path + "\\" + RelativePath);
                media.ConnectDatabaseInNewThread();
                OpenLog();
            }
        }

        /// <summary>
        ///  / Note: not in libanki.
        ///  Mark schema modified to force a full sync, but with the confirmation checking function disabled
        ///  This is a convenience method which doesn't throw ConfirmModSchemaException
        /// </summary>
        public void ModSchemaNoCheck()
        {
            ModSchema(false);
        }

        /// <summary>
        /// Mark schema modified to force a full sync. 
        /// If check==true and the schema has not already been marked modified then ConfirmModSchemaException will be thrown.
        /// If the user chooses to confirm then modSchema(false) should be called, after which the exception can
        /// be safely ignored, and the outer code called again.
        /// </summary>
        /// <param name="check"></param>
        public void ModSchema(bool check = true)
        {
            if (!IsSchemaChanged())
            {
                if (check)
                {
                    // In java ver exception is throw to ask outer code to handle user's choice
                    // of whether to do a full sync or not.
                    // Here we use event 
                    if (ConfirmModSchemaEvent == null)
                        throw new ConfirmModSchemaException();
                    else
                        ConfirmModSchemaEvent();
                }
            }
            scm = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            SetIsModified();
        }

        public bool IsSchemaChanged()
        {
            return scm > ls;
        }

        public void BeforeUpload()
        {
            string[] tables = new string[] { "notes", "cards", "revlog" };
            foreach (string t in tables)
            {
                database.Execute("UPDATE " + t + " SET usn=0 WHERE usn=-1");
            }
            // we can save space by removing the log of deletions
            database.Execute("delete from graves");
            usn += 1;
            models.BeforeUpload();
            tags.BeforeUpload();
            decks.BeforeUpload();
            ModSchemaNoCheck();
            ls = scm;
            // ensure db is compacted before upload
            database.Execute("vacuum");
            database.Execute("analyze");
            Close();
        }

        public int NextID(string type)
        {
            type = "next" + Char.ToUpper(type[0]) + type.Substring(1);
            int id = (int)conf.GetNamedNumber(type, 1);
            conf[type] = JsonValue.CreateNumberValue(id + 1);
            return id;
        }

        /// <summary>
        /// Rebuild the queue and reload data after DB modified
        /// </summary>
        public void Reset()
        {
            sched.Reset();
        }

        public void LogRem(long[] ids, RemovalType type)
        {
            Database.RunInTransaction(() =>
            {
                foreach (long id in ids)
                {
                    string sql = String.Format("insert into graves values ({0}, ?, {1})",
                                                Usn, (int)type);
                    Database.Execute(sql, id);
                }
            });
        }

        public int NoteCount()
        {
            return Database.QueryScalar<int>("SELECT count() FROM notes");
        }

        /// <summary>        
        /// Return a new note with the model derived from the deck or the configuration
        /// </summary>
        /// <param name="forDeck">When true it uses the model specified in the deck(mid), 
        /// otherwise it uses the model specified in the configuration(curModel)</param>
        /// <returns>The new note</returns>
        public Note NewNote(bool forDeck = true)
        {
            return NewNote(models.GetCurrent(forDeck));
        }

        /// <summary>
        /// Return a new note with a specific model
        /// </summary>
        /// <param name="m">The model to use for the new note</param>
        /// <returns>The new note</returns>
        public Note NewNote(JsonObject m)
        {
            return new Note(this, m);
        }

        /// <summary>
        /// Add a note to the collection. Return number of new cards
        /// </summary>
        /// <param name="note"></param>
        /// <returns></returns>
        public int AddNote(Note note)
        {
            // check we have card models available, then save
            List<JsonObject> cms = FindTemplates(note);
            if (cms.Count == 0)
                return 0;
            
            note.SaveChangesToDatabase();
            // deck conf governs which of these are used
            int due = NextID("pos");
            // add cards
            int ncards = 0;
            foreach (JsonObject template in cms)
            {
                NewCard(note, template, due);
                ncards += 1;
            }
            return ncards;
        }

        public void RemoveNotesAndCards(long[] noteIds)
        {
            List<long> list = (from s in
                              database.QueryColumn<CardIdOnlyTable>("SELECT id FROM cards WHERE nid IN " + Utils.Ids2str(noteIds))
                               select s.Id).ToList();
            long[] cids = new long[list.Count];
            int i = 0;
            foreach (long l in list)
            {
                cids[i++] = l;
            }
            RemoveCardsAndNoteIfNoCardsLeft(cids);
        }

        /// <summary>
        /// Bulk delete facts by ID. 
        /// Don't call this directly because it won't delete cards
        /// associate with the deleted notes and can lead to invalid collections.
        /// </summary>
        /// <param name="ids"></param>
        public void RemoveNotesOnly(long[] ids)
        {
            if (ids.Length == 0)
                return;

            string strids = Utils.Ids2str(ids);
            // we need to log these independently of cards, as one side may have
            // more card templates
            LogRem(ids, RemovalType.NOTE);
            database.Execute("DELETE FROM notes WHERE id IN " + strids);
        }

        public List<JsonObject> FindTemplates(Note note)
        {
            JsonObject model = note.Model;
            List<int> avail = models.AvailableOrds(model, Utils.JoinFields(note.Fields));
            return TmplsFromOrds(model, avail);
        }

        private List<JsonObject> TmplsFromOrds(JsonObject model, List<int> avail)
        {
            List<JsonObject> ok = new List<JsonObject>();
            JsonArray tmpls;
            if (model.GetNamedNumber("type") == (double)ModelType.STD)
            {
                tmpls = model.GetNamedArray("tmpls");
                for (uint i = 0; i < tmpls.Count; i++)
                {
                    JsonObject t = tmpls.GetObjectAt(i);
                    if (avail.Contains((int)t.GetNamedNumber("ord")))
                    {
                        ok.Add(t);
                    }
                }
            }
            else
            {
                // cloze - generate temporary templates from first
                string str = Utils.JsonToString(model.GetNamedArray("tmpls").GetObjectAt(0));
                foreach (int ord in avail)
                {
                    JsonObject t = JsonObject.Parse(str);
                    t["ord"] = JsonValue.CreateNumberValue(ord);
                    ok.Add(t);
                }
            }
            return ok;
        }

        private Card NewCard(Note note, JsonObject template, int due, bool flush = true)
        {
            Card card = new Card(this);
            card.NoteId = note.Id;
            card.Ord = (int)template.GetNamedNumber("ord");
            // Use template did (deck override) if valid, otherwise model did
            long did = 0;
            JsonValue didValue = template.GetNamedValue("did", null);
            //WARNING: A work around for the issue of no null allows int Json API!
            if (didValue.GetType().DeclaringType != null)
                did = (long)didValue.GetNumber();

            if (did > 0 && decks.DeckDict.ContainsKey(did))
            {
                card.DeckId = did;
            }
            else
            {
                card.DeckId = (long)note.Model.GetNamedNumber("did", 0);
            }
            
            // if invalid did, use default instead
            JsonObject deck = decks.Get(card.DeckId);
            if (deck.GetNamedNumber("dyn") == 1)
            {
                // must not be a filtered deck
                card.DeckId = 1;
            }
            else
            {
                card.DeckId = (long)deck.GetNamedNumber("id");
            }
            card.Due = DueOfCardForDid(card.DeckId, due);
            if (flush)
            {
                card.SaveChangesToDatabase();
            }
            return card;
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

        public List<long> GenCards(long[] nids)
        {
            // build map of (nid,ord) so we don't create dupes
            string snids = Utils.Ids2str(nids);
            var have = new Dictionary<long, Dictionary<int, long>>();
            Dictionary<long, long> dids = new Dictionary<long, long>();
            var list = database.QueryColumn<CardTable>("SELECT id, nid, ord, did FROM cards WHERE nid IN " + snids);
            foreach(CardTable c in list)
            {
                // existing cards
                long nid = c.Nid;
                if (!have.ContainsKey(nid))
                {
                    have.Add(nid, new Dictionary<int, long>());
                    have[nid].Add(c.Ord, c.Id);
                }
                else
                    have[nid][c.Ord] = c.Id;

                // and their dids
                long did = c.Did;
                if (dids.ContainsKey(nid))
                {
                    if (dids[nid] != 0 && dids[nid] != did)
                    {
                        // cards are in two or more different decks; revert to model default
                        dids[nid] = 0;
                    }
                }
                else
                {
                    // first card or multiple cards in same deck
                    dids.Add(nid, did);
                }
            }

            // build cards for each note
            List<object[]> data = new List<object[]>();
            long ts = Utils.MaxID(database);
            long now = DateTimeOffset.Now.ToUnixTimeSeconds();
            List<long> rem = new List<long>();
            int usn = Usn;
            var noteList = database.QueryColumn<NoteTable>("SELECT id, mid, flds FROM notes WHERE id IN " + snids);
            foreach(NoteTable n in noteList)
            {
                JsonObject model = models.Get(n.Mid);
                List<int> avail = models.AvailableOrds(model, n.Fields);
                long nid = n.Id;
                long did = dids[n.Id];
                if (did == 0)
                {
                    did = (long)model.GetNamedNumber("did");
                }
                // add any missing cards
                foreach (JsonObject t in TmplsFromOrds(model, avail))
                {
                    int tord = (int)t.GetNamedNumber("ord");
                    bool doHave = have.ContainsKey(nid) && have[nid].ContainsKey(tord);
                    if (!doHave)
                    {
                        // check deck is not a cram deck
                        long ndid;
                        try
                        {
                            if (t.GetNamedValue("did").ValueType != JsonValueType.Null)
                            {
                                ndid = (long)t.GetNamedNumber("did");
                                if (ndid != 0)
                                {
                                    did = ndid;
                                }
                            }
                        }
                        catch (Exception)
                        {
                            // do nothing
                        }
                        if (Deck.IsDyn(did))
                        {
                            did = 1;
                        }
                        // if the deck doesn't exist, use default instead
                        did = (long)decks.Get(did).GetNamedNumber("id");
                        // we'd like to use the same due# as sibling cards, but we can't retrieve that quickly, so we
                        // give it a new id instead
                        data.Add(new Object[] { ts, nid, did, tord, now, usn, NextID("pos") });
                        ts += 1;
                    }
                }
                // note any cards that need removing
                if (have.ContainsKey(nid))
                {
                    foreach (var entrySet in have[nid])
                    {
                        if (!avail.Contains(entrySet.Key))
                        {
                            rem.Add(entrySet.Value);
                        }
                    }
                }
            }
            //Bulk update
            database.ExecuteMany("INSERT INTO cards VALUES (?,?,?,?,?,?,0,0,?,0,0,0,0,0,0,0,0,\"\")", data);
            return rem;
        }

        public enum PreviewType
        {
            /// <summary>
            /// When previewing in add dialog, only non-empty
            /// </summary>
            NonEmpty,
            /// <summary>
            /// When previewing edit, only existing
            /// </summary>
            Existing,
            /// <summary>
            /// When previewing in models dialog, all templates
            /// </summary>
            AllTemplates
        }
        /// <summary>
        /// Return cards of a note, without saving them
        /// </summary>
        /// <param name="note">The note whose cards are going to be previewed</param>
        /// <param name="type">Preview type</param>
        /// <returns>list of cards</returns>
        public List<Card> PreviewCards(Note note, PreviewType type = 0)
        {
            List<JsonObject> cms = null;
            switch(type)
            {
                case PreviewType.NonEmpty:
                    cms = FindTemplates(note);
                    break;

                case PreviewType.Existing:
                    cms = new List<JsonObject>();
                    foreach (Card c in note.Cards())
                        cms.Add(c.GetTemplate());
                    break;

                case PreviewType.AllTemplates:
                    cms = new List<JsonObject>();
                    JsonArray ja = note.Model.GetNamedArray("tmpls");
                    for (uint i = 0; i < ja.Count; ++i)
                        cms.Add(ja.GetObjectAt(i));
                    break;
            }
            if (cms.Count == 0)
                return new List<Card>();
            
            List<Card> cards = new List<Card>();
            foreach (JsonObject template in cms)
                cards.Add(NewCard(note, template, 1, false));
            
            return cards;
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

        public void RemoveCardsAndNoteIfNoCardsLeft(long[] ids, bool removeNoteToo = true)
        {
            if (ids.Length == 0)
            {
                return;
            }
            string sids = Utils.Ids2str(ids);
            var listCard = database.QueryColumn<CardTable>("SELECT nid FROM cards WHERE id IN " + sids);
            long[] nids = (from s in listCard select s.Nid).ToArray();
            // remove cards
            LogRem(ids, RemovalType.CARD);
            database.Execute("DELETE FROM cards WHERE id IN " + sids);
            // then notes
            if (!removeNoteToo)
                return;
            
            var listNote = database.QueryColumn<NoteTable>("SELECT id FROM notes WHERE id IN " + Utils.Ids2str(nids)
                                                        + " AND id NOT IN (SELECT nid FROM cards)");
            nids = (from s in listNote select s.Id).ToArray();
            RemoveNotesOnly(nids);
        }

        public List<long> EmptyCids()
        {
            List<long> rem = new List<long>();
            foreach (JsonObject m in Models.All())
            {
                rem.AddRange(GenCards(Models.GetNoteIds(m).ToArray()));
            }
            return rem;
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

        private List<object[]> FieldData(string snids)
        {
            List<object[]> result = new List<object[]>();
            var list = database.QueryColumn<NoteTable>("SELECT id, mid, flds FROM notes WHERE id IN " + snids);
            foreach(NoteTable n in list)
            {
                result.Add(new object[] { n.Id, n.Mid, n.Fields });
            }
            return result;
        }

        public void UpdateFieldCache(long[] nids)
        {
            string snids = Utils.Ids2str(nids);
            List<object[]> r = new List<object[]>();
            foreach (object[] o in FieldData(snids))
            {
                string[] fields = Utils.SplitFields((String)o[2]);
                JsonObject model = models.Get((long)o[1]);
                if (model == null)
                {
                    // note point to invalid model
                    continue;
                }
                r.Add(new Object[] { Utils.StripHTML(fields[models.SortIdx(model)]), Utils.FieldChecksum(fields[0]), o[0] });
            }
            // apply, relying on calling code to bump usn+mod
            database.ExecuteMany("UPDATE notes SET sfld=?, csum=? WHERE id=?", r);
        }

        public List<Dictionary<string, string>> BulkRenderQA(int[] ids = null, string type = "card")
        {
            string where;
            if (type.Equals("card"))
            {
                where = "AND c.id IN " + Utils.Ids2str(ids);
            }
            else if (type.Equals("fact"))
            {   
                where = "AND n.id IN " + Utils.Ids2str(ids);
            }
            else if (type.Equals("model"))
            {   //WARNING: original code in java and python ver "AND m.id IN"
                //However there is no m in QaData
                //where = "AND m.id IN " + Utils.Ids2str(ids);
                where = "AND n.mid IN " + Utils.Ids2str(ids);
            }
            else if (type.Equals("all"))
            {
                where = "";
            }
            else
            {
                throw new Exception("Collection.renderqa!");
            }
            var result = new List<Dictionary<string, string>>();
            foreach (object[] row in  QaData(where))
            {
                result.Add(RenderQA(row));
            }
            return result;
        }

        /// <summary>
        /// Returns hash of id, question, answer.
        /// </summary>
        /// <param name="data"></param>
        /// <param name="qfmt"></param>
        /// <param name="afmt"></param>
        /// <returns></returns>
        public Dictionary<string, string> RenderQA(object[] data, string qfmt = null, string afmt = null)
        {
            // data is [cid, nid, mid, did, ord, tags, flds]
            // unpack fields and create dict
            string[] flist = Utils.SplitFields((string)data[6]);
            Dictionary<string, string> fields = new Dictionary<string, string>();
            JsonObject model = models.Get(Convert.ToInt64(data[2]));
            Dictionary<string, KeyValuePair<int, JsonObject>> fmap = models.FieldMap(model);
            int key;
            foreach (string name in fmap.Keys)
            {
                key = fmap[name].Key;
                fields.Add(name, flist[key]);
            }
            int cardNum = ((int)data[4]) + 1;
            fields["Tags"] = ((string)data[5]).Trim();
            fields["Type"] = model.GetNamedString("name");
            fields["Deck"] = decks.GetDeckName((long)data[3]);
            string[] parents = fields["Deck"].Split(new string[] { "::" }, 
                                    StringSplitOptions.None);
            fields["Subdeck"] = parents[parents.Length - 1];
            JsonObject template;
            if (model.GetNamedNumber("type") == (double)ModelType.STD)
            {
                template = model.GetNamedArray("tmpls").GetObjectAt(Convert.ToUInt32(data[4]));
            }
            else
            {
                template = model.GetNamedArray("tmpls").GetObjectAt(0);
            }
            fields.Add("Card", template.GetNamedString("name"));
            fields.Add(String.Format(Media.locale, "c{0}", cardNum), "1");
            // render q & a
            Dictionary<string, string> d = new Dictionary<string, string>();
            d.Add("id", ((long)data[0]).ToString());
            qfmt = string.IsNullOrEmpty(qfmt) ? template.GetNamedString("qfmt") : qfmt;
            afmt = string.IsNullOrEmpty(afmt) ? template.GetNamedString("afmt") : afmt;

            var dict = new KeyValuePair<string, string>[] 
                            { new KeyValuePair<string, string>("q", qfmt),
                              new KeyValuePair<string, string>("a", afmt) };

            foreach (var p in dict)
            {
                string type = p.Key;
                string format = p.Value;
                if (type.Equals("q"))
                {
                    format = clozePatternQ.Replace(format, String.Format(Media.locale, "{{{{$1cq-{0}:", cardNum));
                    format = clozeTagStart.Replace(format, String.Format(Media.locale, "<%%cq:{0}:", cardNum));
                }
                else
                {
                    format = clozePatternA.Replace(format, String.Format(Media.locale, "{{{{$1ca-{0}:", cardNum));
                    format = clozeTagStart.Replace(format, String.Format(Media.locale, "<%%ca:{0}:", cardNum));
                    // Java ver: The following line differs from python ver // TODO: why?
                    fields.Add("FrontSide", d["q"]); // fields.put("FrontSide", mMedia.stripAudio(d.get("q")));
                }
                //WARNING: there is no hook name "mungeFields" in both java and python ver
                //so this line basically just return back the args!
                fields = (Dictionary<string, string>)Hooks.Hooks.RunFilter("mungeFields", fields, model, data, this);
                string html = new Templates.Template(format, fields).Render();
                d.Add(type, (string)Hooks.Hooks.RunFilter("mungeQA", html, type, fields, model, data, this));
                // empty cloze?
                if (type.Equals("q") && model.GetNamedNumber("type") == (double)ModelType.CLOZE)
                {
                    var avail = Models.AvailableClozeOrds(model, (string)data[6], false);
                    if (avail.Count == 0)
                    {
                        string link = string.Format("<a href={0}#cloze>{1}</a>", HttpSite.HELP, "help");
                        d["q"] = string.Format("Please edit this note and add some cloze deletions. ({0})", link);
                    }
                }
            }
            return d;
        }

        /// <summary>
        /// Return [cid, nid, mid, did, ord, tags, flds] db query
        /// </summary>
        /// <param name="where"></param>
        /// <returns></returns>
        public List<object[]> QaData(string where = "")
        {
            try
            {
                List<object[]> data = new List<object[]>();
                List<CardTable> listCard = null;
                List<NoteTable> listNote = null;
                database.RunInTransaction(() =>
                {
                    //WARNING: Different with python and java ver since we can only use class
                    //to access database
                    //  original sql:  "SELECT c.id, n.id, n.mid, c.did, c.ord, " 
                    //  + "n.tags, n.flds FROM cards c, notes n WHERE c.nid == n.id " + where
                    listCard = Database.QueryColumn<CardTable>(
                        "SELECT distinct id, did, ord, nid FROM cards c, notes n WHERE c.nid == n.id " + where);


                    listNote = Database.QueryColumn<NoteTable>(
                            "SELECT distinct n.id, n.mid, n.tags, n.flds FROM notes n, cards c WHERE c.nid == n.id " + where);
                });
                listNote.Distinct();
                var list = from c in listCard 
                           from n in listNote
                           where c.Id == n.Id
                           select new
                           {
                               cid = c.Id,
                               nid = n.Id,
                               mid = n.Mid,
                               did = c.Did,
                               ord = c.Ord,
                               tags = n.Tags,
                               fields = n.Fields
                           };

                foreach (var l in list)
                {
                    data.Add(new object[] { l.cid, l.nid, l.mid, l.did, l.ord,
                    l.tags, l.fields });
                }
                return data;
            }
            catch(Exception e)
            {
                throw new Exception("Collection.QaData Wrong queries?", e);
            }
        }

        /// <summary>
        /// Return a list of card ids
        /// </summary>
        /// <param name="search"></param>
        /// <param name="order"></param>
        /// <returns></returns>
        public List<long> FindCards(string search, string order = null)
        {
            return new Finder(this).FindCards(search, order);
        }

        public List<long> FindCards(string search, bool order)
        {
            return new Finder(this).FindCards(search, order);
        }

        public List<Dictionary<string, string>> FindCardsForCardBrowser(string search, bool order, Dictionary<string, string> deckNames)
        {
            return new Finder(this).FindCardsForCardBrowser(search, order, deckNames);
        }

        /// <summary>
        /// Return a list of note ids 
        /// </summary>
        /// <param name="query"></param>
        /// <returns></returns>
        public List<long> FindNotes(string query)
        {
            return new Finder(this).FindNotes(query);
        }

        public int FindReplace(List<long> nids, string src, string dst)
        {
            return Finder.FindReplace(this, nids, src, dst);
        }

        public int FindReplace(List<long> nids, string src, string dst, bool regex)
        {
            return Finder.FindReplace(this, nids, src, dst, regex);
        }

        public int FindReplace(List<long> nids, string src, string dst, string field)
        {
            return Finder.FindReplace(this, nids, src, dst, field);
        }

        public int FindReplace(List<long> nids, string src, string dst, bool regex, string field, bool fold)
        {
            return Finder.FindReplace(this, nids, src, dst, regex, field, fold);
        }

        public List<KeyValuePair<string, List<long>>> FindDupes(string fieldName)
        {
            return Finder.FindDupes(this, fieldName, "");
        }

        public List<KeyValuePair<string, List<long>>> FindDupes(string fieldName, string search)
        {
            return Finder.FindDupes(this, fieldName, search);
        }

        public void SetTimeLimit(long seconds)
        {
            conf["timeLim"] = JsonValue.CreateNumberValue(seconds);
        }

        public long GetTimeLimit()
        {
            return (long)conf.GetNamedNumber("timeLim");
        }

        public void StartTimebox()
        {
            startTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            startReps = sched.Reps;
        }

        public long[] TimeboxReached()
        {
            if (conf.GetNamedNumber("timeLim") == 0)
            {
                // timeboxing disabled
                return null;
            }
            double elapsed = DateTimeOffset.Now.ToUnixTimeMilliseconds() - startTime;
            if (elapsed > conf.GetNamedNumber("timeLim"))
            {
                return new long[] { (long)conf.GetNamedNumber("timeLim"), (long)(sched.Reps - startReps) };
            }
            return null;
        }

        public void AutoSaveAfterFiveMinuts()
        {
            if (DateTimeOffset.Now.ToUnixTimeSeconds() - lastSave > 300)
                Save();
        }

        public bool UndoAvailable()
        {
            return undoList.Count > 0;
        }

        public long Undo()
        {
            object[] data = undoList.Last();
            undoList.RemoveLast();
            switch ((int)data[0])
            {
                case UNDO_REVIEW:
                    Card c = (Card)data[1];
                    // remove leech tag if it didn't have it before
                    bool wasLeech = (bool)data[2];
                    if (!wasLeech && c.LoadNote().HasTag("leech"))
                    {
                        c.LoadNote().DelTag("leech");
                        c.LoadNote().SaveChangesToDatabase();
                    }
                    // write old data
                    c.SaveChangesToDatabase(false);
                    // and delete revlog entry
                    long last = database.QueryScalar<long>("SELECT id FROM revlog WHERE cid = " + c.Id + " ORDER BY id DESC LIMIT 1");
                    database.Execute("DELETE FROM revlog WHERE id = " + last);
                    // restore any siblings
                    database.Execute("update cards set queue=type,mod=?,usn=? where queue=-2 and nid=?",
                            new object[] { DateTimeOffset.Now.ToUnixTimeSeconds(), Usn, c.NoteId });
                    // and finally, update daily count
                    int n = c.Queue == 3 ? 1 : c.Queue;
                    string type = (new string[] { "new", "lrn", "rev" })[n];
                    sched.UpdateTodayStats(c, type, -1);
                    sched.Reps = sched.Reps - 1;
                    return c.Id;

                case UNDO_BURY_NOTE:
                    foreach (Card cc in (List<Card>)data[2])
                    {
                        cc.SaveChangesToDatabase(false);
                    }
                    return (long)data[3];

                case UNDO_SUSPEND_CARD:
                    Card suspendedCard = (Card)data[1];
                    suspendedCard.SaveChangesToDatabase(false);
                    return suspendedCard.Id;

                case UNDO_SUSPEND_NOTE:
                    foreach (Card ccc in (List<Card>)data[1])
                    {
                        ccc.SaveChangesToDatabase(false);
                    }
                    return (long)data[2];

                case UNDO_DELETE_NOTE:
                    List<long> ids = new List<long>();
                    Note note2 = (Note)data[1];
                    note2.SaveChangesToDatabase(note2.TimeModified, false);
                    ids.Add(note2.Id);
                    foreach (Card c4 in (List<Card>)data[2])
                    {
                        c4.SaveChangesToDatabase(false);
                        ids.Add(c4.Id);
                    }
                    Database.Execute("DELETE FROM graves WHERE oid IN " + Utils.Ids2str(ids.ToArray()));
                    return (long)data[3];

                case UNDO_BURY_CARD:
                    foreach (Card cc in (IEnumerable<Card>)data[2])
                    {
                        cc.SaveChangesToDatabase(false);
                    }
                    return (long)data[3];
                default:
                    return 0;
            }
        }

        public void MarkUndo(int type, params object[] o)
        {
            switch (type)
            {
                case UNDO_REVIEW:
                    undoList.AddLast(new object[] { type, ((Card)o[0]).ShallowClone(), o[1] });
                    break;
                case UNDO_BURY_NOTE:
                    undoList.AddLast(new object[] { type, o[0], o[1], o[2] });
                    break;
                case UNDO_SUSPEND_CARD:
                    undoList.AddLast(new object[] { type, ((Card)o[0]).ShallowClone() });
                    break;
                case UNDO_SUSPEND_NOTE:
                    undoList.AddLast(new object[] { type, o[0], o[1] });
                    break;
                case UNDO_DELETE_NOTE:
                    undoList.AddLast(new object[] { type, o[0], o[1], o[2] });
                    break;
                case UNDO_BURY_CARD:
                    undoList.AddLast(new object[] { type, o[0], o[1], o[2] });
                    break;
            }
            while (undoList.Count > UNDO_SIZE_MAX)
            {
                undoList.RemoveFirst();
            }
        }

        public void MarkReview(Card card)
        {
            MarkUndo(UNDO_REVIEW, new object[] { card, card.LoadNote().HasTag("leech") });
        }

        /// <summary>
        /// Basic integrity check for syncing. True if ok.
        /// </summary>
        /// <returns></returns>
        public bool BasicCheck()
        {
            // cards without notes
            if (database.QueryScalar<int>("select 1 from cards where nid not in (select id from notes) limit 1") > 0)
            {
                return false;
            }
            bool badNotes = database.QueryScalar<int>(String.Format(Media.locale,
                    "select 1 from notes where id not in (select distinct nid from cards) " +
                    "or mid not in {0} limit 1", Utils.Ids2str(models.Ids()))) > 0;
            // notes without cards or models
            if (badNotes)
            {
                return false;
            }
            // invalid ords
            foreach (JsonObject m in models.All())
            {
                // ignore clozes
                if (m.GetNamedNumber("type") != (double) ModelType.STD)
                {
                    continue;
                }
                // Make a list of valid ords for this model
                JsonArray tmpls = m.GetNamedArray("tmpls");
                int[] ords = new int[tmpls.Count];
                for (uint t = 0; t < tmpls.Count; t++)
                {
                    ords[t] = (int)tmpls.GetObjectAt(t).GetNamedNumber("ord");
                }

                bool badOrd = database.QueryScalar<int>(String.Format(Media.locale,
                        "select 1 from cards where ord not in {0} and nid in ( " +
                        "select id from notes where mid = {1}) limit 1",
                        Utils.Ids2str(ords), m.GetNamedNumber("id"))) > 0;
                if (badOrd)
                {
                    return false;
                }
            }
            return true;
        }

        public async Task<long> FixIntegrity()
        {
            StorageFile file = await folder.GetFileAsync(relativePath);
            List<string> problems = new List<string>();
            ulong oldSize = (await file.GetBasicPropertiesAsync()).Size;
            long result = 0;

            try
            {
                database.RunInTransaction(() =>
               {
                   Save();
                   if (!database.QueryScalar<string>("PRAGMA integrity_check").Equals("ok"))
                   {
                       result = -1;
                       return;
                   }
                   // note types with a missing model
                   List<long> ids = (from s in database.
                                   QueryColumn<NoteTable>("SELECT id FROM notes WHERE mid NOT IN " + Utils.Ids2str(models.Ids()))
                                     select s.Id).ToList();

                   if (ids.Count != 0)
                   {
                       problems.Add("Deleted " + ids.Count + " note(s) with missing note type.");

                       RemoveNotesOnly(ids.ToArray());
                   }
                   // for each model
                   foreach (JsonObject m in models.All())
                   {
                       // cards with invalid ordinal
                       if (m.GetNamedNumber("type") == (double)ModelType.STD)
                       {
                           List<int> ords = new List<int>();
                           JsonArray tmpls = m.GetNamedArray("tmpls");
                           for (uint t = 0; t < tmpls.Count; t++)
                           {
                               ords.Add((int)tmpls.GetObjectAt(t).GetNamedNumber("ord"));
                           }
                           //WARNING: Not sure if this query will only return card id or also note id
                           ids = (from s in database
                                .QueryColumn<CardIdOnlyTable>("SELECT id FROM cards WHERE ord NOT IN "
                                + Utils.Ids2str(ords.ToArray()) + " AND nid IN ( "
                                + "SELECT id FROM notes WHERE mid = " + m.GetNamedNumber("id") + ")")
                                  select s.Id).ToList();
                           if (ids.Count > 0)
                           {
                               problems.Add("Deleted " + ids.Count + " card(s) with missing template.");
                               RemoveCardsAndNoteIfNoCardsLeft(ids.ToArray());
                           }
                       }
                       // notes with invalid field counts
                       ids.Clear();

                       var listNotes = database.QueryColumn<NoteTable>("select id, flds from notes where mid = "
                                                                   + m.GetNamedNumber("id"));
                       foreach (NoteTable n in listNotes)
                       {
                           String flds = n.Fields;
                           long id = n.Id;
                           int fldsCount = 0;
                           for (int i = 0; i < flds.Length; i++)
                           {
                               if (flds[i] == 0x1f)
                               {
                                   fldsCount++;
                               }
                           }
                           if (fldsCount + 1 != m.GetNamedArray("flds").Count)
                           {
                               ids.Add(id);
                           }
                       }
                       if (ids.Count > 0)
                       {
                           problems.Add("Deleted " + ids.Count + " note(s) with wrong field count.");
                           RemoveNotesOnly(ids.ToArray());
                       }

                   }
                   // delete any notes with missing cards
                   var idsArray = (from s in database.
                         QueryColumn<NoteTable>("SELECT id FROM notes WHERE id NOT IN (SELECT DISTINCT nid FROM cards)")
                                   select s.Id).ToArray();
                   if (idsArray.Length != 0)
                   {
                       problems.Add("Deleted " + idsArray.Length + " note(s) with missing no cards.");

                       RemoveNotesOnly(idsArray);
                   }
                   // cards with missing notes
                   idsArray = (from s in database.
                        QueryColumn<CardIdOnlyTable>("SELECT id FROM cards WHERE nid NOT IN (SELECT id FROM notes)")
                               select s.Id).ToArray();

                   if (idsArray.Length != 0)
                   {
                       problems.Add("Deleted " + idsArray.Length + " card(s) with missing note.");
                       RemoveCardsAndNoteIfNoCardsLeft(idsArray);
                   }
                   // cards with odue set when it shouldn't be
                   idsArray = (from s in database.
                             QueryColumn<CardIdOnlyTable>("select id from cards where odue > 0 and (type=1 or queue=2) and not odid")
                               select s.Id).ToArray();
                   if (idsArray.Length != 0)
                   {
                       problems.Add("Fixed " + idsArray.Length + " card(s) with invalid properties.");
                       database.Execute("update cards set odue=0 where id in " + Utils.Ids2str(idsArray));
                   }
                   // cards with odid set when not in a dyn deck
                   List<long> dids = new List<long>();
                   foreach (long id in decks.AllIds())
                   {
                       if (!decks.IsDyn(id))
                       {
                           dids.Add(id);
                       }
                   }
                   idsArray = (from s in database.
                               QueryColumn<CardIdOnlyTable>("select id from cards where odid > 0 and did in "
                               + Utils.Ids2str(dids.ToArray()))
                               select s.Id).ToArray();
                   if (idsArray.Length != 0)
                   {
                       problems.Add("Fixed " + idsArray.Length + " card(s) with invalid properties.");
                       database.Execute("update cards set odid=0, odue=0 where id in " + Utils.Ids2str(idsArray));
                   }
                   // tags
                   tags.RegisterNotes();
                   // field cache
                   foreach (JsonObject m in models.All())
                   {
                       UpdateFieldCache(models.GetNoteIds(m).ToArray());
                   }
                   // new cards can't have a due position > 32 bits
                   database.Execute("UPDATE cards SET due = 1000000, mod = "
                                  + DateTimeOffset.Now.ToUnixTimeSeconds() + ", usn = " + Usn
                                  + " WHERE due > 1000000 AND queue = 0");
                   // new card position
                   conf["nextPos"] = JsonValue.CreateNumberValue(
                                      database.QueryScalar<int>("SELECT max(due) + 1 FROM cards WHERE type = 0"));
                   // reviews should have a reasonable due
                   idsArray = (from s in database.
                              QueryColumn<CardIdOnlyTable>("SELECT id FROM cards WHERE queue = 2 AND due > 10000")
                               select s.Id).ToArray();
                   if (idsArray.Length > 0)
                   {
                       problems.Add("Reviews had incorrect due date.");
                       database.Execute("UPDATE cards SET due = 0, mod = " + DateTimeOffset.Now.ToUnixTimeSeconds() + ", usn = " + Usn
                               + " WHERE id IN " + Utils.Ids2str(idsArray));
                   }

                   // DB must have indices. Older versions of AnkiDroid didn't create them for new collections.
                   int ixs = database.QueryScalar<int>("select count(name) from sqlite_master where type = 'index'");
                   if (ixs < 7)
                   {
                       problems.Add("Indices were missing.");
                       Storage.AddIndices(database);
                   }
               });
            }
            catch
            {
                throw new Exception("Collection.FixIntegrity Failed!");
            }
            if (result == -1)
                return result;

            // and finally, optimize
            Optimize();
            file = await folder.GetFileAsync(relativePath);
            ulong newSize = (await file.GetBasicPropertiesAsync()).Size;
            // if any problems were found, force a full sync
            if (problems.Count > 0)
            {
                ModSchemaNoCheck();
            }
            // TODO: report problems
            return (long)(oldSize - newSize) / 1024;
        }

        public void Optimize()
        {
            database.Execute("VACUUM");
            database.Execute("ANALYZE");
            database.IsModified = true;
        }

        public void Log([CallerMemberName] string memberName = "", [CallerFilePath] string filePath = "", params object[] args)
        {
            if (!debugLog)
                return;
            // Overwrite any args that need special handling for an appropriate string representation
            for (int i = 0; i < args.Length; i++)
            {
                if (args[i] is long[])
                {
                    args[i] = ((long[])args[i]).ToString();
                }

                string s = String.Format("[{0}] {1}:{2}(): {3}", DateTimeOffset.Now.ToUnixTimeSeconds(), memberName, filePath,
                            String.Join(",  ", args));
                logHnd.WriteLine(s);
            }
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
