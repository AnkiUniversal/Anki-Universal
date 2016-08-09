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
using Windows.Storage;
using System.IO;
using System.Text.RegularExpressions;

namespace AnkiU.AnkiCore.Importer
{
    public struct DuplicateNoteUpdate
    {        
        public bool isAllow;
        public bool isNotAskAgain;        
    }
    public class Anki2Importer : Importer
    {
        private const string DEFAULT_AFFIX = " Imported";

        private const int MEDIA_PICK_LIMIT= 1024;
        private const int MID = 2;

        private string deckPrefix;
        private DuplicateNoteUpdate isAllowUpdate;
        private bool dupeOnSchemaChange;

        private Dictionary<string, object[]> notes;

        /**
        * Java can't use a tuple as a key, so java ver resorts to indexing twice with nested maps.
        * Python: (guid, ord) -> cid
        * Java: guid -> ord -> cid
        * We will do the same in C# 
        */
        private Dictionary<string, Dictionary<int, long>> cards;
        private Dictionary<long, long> deckMapSourceToDest;
        private Dictionary<long, long> modelMap;
        private Dictionary<string, string> changedGuids;
        private Dictionary<string, bool> ignoredGuids;

        private int dupes;
        private int added;
        private int updated;       

        public int Dupes { get { return dupes; } }
        public int Added { get { return added; } }
        public int Updated { get { return updated; } }

        #region Not in java and python ver
        public delegate Task<DuplicateNoteUpdate> DuplicateNoteEventHandler();        
        public delegate Task<bool> DuplicateDeckEventhandler(string deckName);
        public delegate void ImporterStateHandler(string message);
        public event DuplicateNoteEventHandler DuplicateNoteEvent;
        public event DuplicateDeckEventhandler DuplicateDeckEvent;
        protected event ImporterStateHandler ImporterStateChangeEvent;

        private List<long> importedNoteId;
        public List<long> ImportedNoteId { get { return importedNoteId; } }

        private Dictionary<long, long> importedDeckIdMap;
        public Dictionary<long, long> ImportedDeckIdMap { get { return importedDeckIdMap; } }
        #endregion

        public Anki2Importer(Collection collection, StorageFolder sourceFolder, string relativePathToFile) 
            : base(collection, sourceFolder, relativePathToFile)
        {
            needMapper = false;
            deckPrefix = null;
            isAllowUpdate = new DuplicateNoteUpdate();    
            dupeOnSchemaChange = false;
            importedNoteId = new List<long>();
            importedDeckIdMap = new Dictionary<long, long>();
        }

        public async override Task Run()
        {
            await PrepareFiles();
            await Import();
        }

        private async Task PrepareFiles()
        {
            ImporterStateChangeEvent?.Invoke("Preparing collections...");
            sourceCol = await Storage.OpenOrCreateCollection(sourceFolder, relativePathToFile);
            deckMapSourceToDest = new Dictionary<long, long>();

            //WARNING: Not in java and python ver.                       
            await MakeSureNoConflictDeckName();
        }

        private async Task MakeSureNoConflictDeckName()
        {            
            var destDeckNames = destCol.Deck.AllNames();
            bool isDefaultDeck = false;           
            
            foreach (var deck in sourceCol.Deck.All())
            {                
                string deckName = deck.GetNamedString("name");
                if (deckName.Equals(Constant.DEFAULT_DECKNAME, StringComparison.OrdinalIgnoreCase))
                {
                    deckName = ChangeDefaultDeckname(deck.GetNamedString("name"), deckName);
                    isDefaultDeck = true;
                }

                if (destDeckNames.Contains(deckName))
                {
                    if (DuplicateDeckEvent == null)
                        continue;

                    bool isRename;
                    if (isDefaultDeck)
                    {
                        isRename = true;                        
                    }
                    else
                        isRename = await DuplicateDeckEvent(deckName);
                    if (isRename)
                    {                        
                        string newName = deckName + " (1)";
                        int index = 2;
                        while (destDeckNames.Contains(newName))
                        {
                            newName = deckName + " (" + index + ")";
                            index++;
                        }
                        var sourNames = sourceCol.Deck.AllNames();
                        while (sourNames.Contains(newName))
                        {
                            newName = deckName + " (" + index + ")";
                            index++;
                        }
                        RenameDeckInSource(deckName, newName);
                    }
                }

                isDefaultDeck = false;
            }
        }

        private string ChangeDefaultDeckname(string name, string deckName)
        {
            var newName = deckName + DEFAULT_AFFIX;
            int index = 1;
            var sourNames = sourceCol.Deck.AllNames();
            while (sourNames.Contains(newName))
            {
                newName = deckName + " (" + index + ")";
                index++;
            }
            RenameDeckInSource(name, newName);
            return newName;
        }

        private void RenameDeckInSource(string sourceDeckName, string newName)
        {
            var deck = sourceCol.Deck.GetDeckByName(sourceDeckName);            
            sourceCol.Deck.Rename(deck, newName);
            sourceCol.Deck.Save(deck);
            sourceCol.SaveAndCommit();
        }

        private async Task Import()
        {            
            //Use transactions for performance and rollbacks in case of error            
            string saveDestCol = destCol.Database.SaveTransactionPoint();            
            string saveMedia = destCol.Media.Database.SaveTransactionPoint();

            try
            {
                await StartImporting();
                destCol.Database.Commit();
                destCol.Media.Database.Commit();
            }
            catch (Exception e)
            {
                destCol.Database.RollbackTo(saveDestCol);
                destCol.Media.Database.RollbackTo(saveMedia);
                throw new Exception(e.Message, e);
            }
            finally
            {
                destCol.Database.Execute("vacuum");
                destCol.Database.Execute("analyze");
            }
        }

        private async Task StartImporting()
        {
            if (!String.IsNullOrEmpty(deckPrefix))
            {
                long? id = destCol.Deck.AddOrResuedDeck(deckPrefix);
                if (id == null)
                    return;
                destCol.Deck.Select((long)id);
            }
            PrepareTimeStamp();
            PrepareModels();

            ImporterStateChangeEvent?.Invoke("Importing notes...");
            await ImportNotes();

            ImporterStateChangeEvent?.Invoke("Importing cards...");
            ImportCards();

            //In AnkiU we don't support static media
            //ImporterStateChangeEvent?.Invoke("Importing static media...");
            //await ImportStaticMedia();

            ImporterStateChangeEvent?.Invoke("Cleaning...");
            PostImport();
        }

        private async Task ImportNotes()
        {
            // build guid -> (id,mod,mid) hash & map of existing note ids
            notes = new Dictionary<string, object[]>();
            Dictionary<long, bool> existing = new Dictionary<long, bool>();
            var list =  destCol.Database.QueryColumn<NoteTable>("select id, guid, mod, mid from notes");
            foreach (NoteTable n in list)
            {
                long id = n.Id;
                string guid = n.GuId;
                long mod = n.TimeModified;
                long mid = n.Mid;
                notes.Add(guid, new object[] { id, mod, mid });
                existing.Add(id, true);
            }
            // we may need to rewrite the guid if the model schemas don't match,
            // so we need to keep track of the changes for the card import stage
            changedGuids = new Dictionary<string, string>();
            // apart from upgrading from anki1 decks, we ignore updates to changed
            // schemas. we need to note the ignored guids, so we avoid importing
            // invalid cards
            ignoredGuids = new Dictionary<string, bool>();
            // iterate over source collection
            List<object[]> add = new List<object[]>();
            List<object[]> update = new List<object[]>();
            List<long> dirty = new List<long>();
            int usn = destCol.Usn;
            int dupes = 0;
            List<string> dupesIgnored = new List<string>();

            list = sourceCol.Database.QueryColumn<NoteTable>("select * from notes");

            bool largeCollection = total > 200;
            int i = 0;

            foreach(NoteTable nl in list)
            {
                bool shouldAdd = UniquifyNote(nl);
                // turn the db result into a mutable list
                object[] note = new object[]{ nl.Id, nl.GuId, nl.Mid, nl.TimeModified, nl.Usn,
                                            nl.Tags, nl.Fields, nl.Sortfields, nl.CheckSum,
                                            nl.Flags, nl.Data};

                if (shouldAdd)
                {
                    // ensure id is unique
                    while (existing.ContainsKey(nl.Id))
                    {
                        nl.Id = nl.Id + 999;
                    }
                    existing.Add(nl.Id, true);
                    note[0] = nl.Id;
                    // bump usn
                    note[4] = usn;
                    //WARNING: in ankiU media files are store in their
                    //deckId folder so we simply replace exising files
                    //with imported files if they have the same same
                    //// update media references in case of dupes
                    //note[6] = await MungeMedia(nl.Mid, nl.Fields);
                    note[6] = nl.Fields;
                    add.Add(note);
                    dirty.Add(nl.Id);
                    // note we have the added guid
                    notes.Add(nl.GuId, new object[] { note[0], note[3], note[MID] });
                }
                else
                {
                    // a duplicate or changed schema - safe to update?
                    dupes += 1;
                    if (!isAllowUpdate.isNotAskAgain)
                        if (DuplicateNoteEvent != null)                        
                            isAllowUpdate = await DuplicateNoteEvent();                                                     

                    if (isAllowUpdate.isAllow)
                    {
                        object[] n = notes[nl.GuId];
                        long oldNid = Convert.ToInt64(n[0]);
                        long oldMod = Convert.ToInt64(n[1]);
                        long oldMid = Convert.ToInt64(n[2]);
                        // will update if incoming note more recent
                        if (oldMod < nl.TimeModified)
                        {
                            // safe if note types identical
                            if (oldMid == nl.Mid)
                            {
                                // incoming note should use existing id
                                note[0] = oldNid;
                                note[4] = usn;
                                //WARNING: in ankiU media files are store in their
                                //deckId folder so we simply replace exising files
                                //with imported files if they have the same same
                                //// update media references in case of dupes
                                //note[6] = await MungeMedia(nl.Mid, nl.Fields);
                                note[6] = nl.Fields;
                                update.Add(note);
                                dirty.Add(oldNid);
                            }
                            else
                            {
                                dupesIgnored.Add(String.Format("{0}: {1}",
                                        destCol.Models.Get(oldMid).GetNamedString("name"),
                                        (nl.Fields).Replace("\u001f", ",")));
                                ignoredGuids.Add(nl.GuId, true);
                            }
                        }
                    }
                }
                i++;
            }
            // export info for calling code
            this.dupes = dupes;
            added = add.Count;
            updated = update.Count;
            // add to col
            //Don't use excute many here as we've already call RunInTransaction in Import
            foreach (var c in add)
            {
                //Only in AnkU we will need this ID to move media files after importing
                importedNoteId.Add((long)c[0]);

                destCol.Database.Execute("insert or replace into notes values (?,?,?,?,?,?,?,?,?,?,?)", c);
            }
            foreach (var u in update)
            {
                //Only in AnkU we will need this ID to move media files after importing
                importedNoteId.Add((long)u[0]);

                destCol.Database.Execute("insert or replace into notes values (?,?,?,?,?,?,?,?,?,?,?)", u);
            }
            long[] das = dirty.ToArray();
            destCol.UpdateFieldCache(das);
            destCol.Tags.RegisterNotes(das);
        }

        /// <summary>
        /// Determine if note is a duplicate, and adjust mid and/or guid as required
        /// returns true if note should be added
        /// </summary>
        /// <param name="note"></param>
        /// <returns></returns>
        private bool UniquifyNote(NoteTable note)
        {
            string origGuid = note.GuId;
            long srcMid = note.Mid;
            long dstMid = GetMid(srcMid);
            // duplicate Schemas?
            if (srcMid == dstMid)
            {
                return !notes.ContainsKey(origGuid);
            }
            // differing schemas and note doesn't exist?
            note.Mid = dstMid;
            if (!notes.ContainsKey(origGuid))
            {
                return true;
            }
            // as the schemas differ and we already have a note with a different
            // note type, this note needs a new guid
            if (!dupeOnSchemaChange)
            {
                return false;
            }
            while (true)
            {
                note.GuId = Utils.GetGuidIncrease(note.GuId);
                changedGuids.Add(origGuid, (note.GuId));
                // if we don't have an existing guid, we can add
                if (!notes.ContainsKey(note.GuId))
                {
                    return true;
                }
                // if the existing guid shares the same mid, we can reuse
                if (dstMid == Convert.ToInt64(notes[note.GuId][MID]))
                {
                    return false;
                }
            }
        }

        /**
        * Models
        * ***********************************************************
        * Models in the two decks may share an ID but not a schema, so we need to
        * compare the field & template signature rather than just rely on ID. If
        * the schemas don't match, we increment the mid and try again, creating a
        * new model if necessary.
        */

        ///<summary>Prepare index of schema hashes</summary>
        private void PrepareModels()
        {
            modelMap = new Dictionary<long, long>();
        }

        /// <summary>
        /// Return local id for remote MID
        /// </summary>
        /// <param name="srcMid"></param>
        /// <returns></returns>
        private long GetMid(long srcMid)
        {
            // already processed this mid?
            if (modelMap.ContainsKey(srcMid))
            {
                return modelMap[srcMid];
            }
            long mid = srcMid;
            JsonObject srcModel = sourceCol.Models.Get(srcMid);
            string srcScm = sourceCol.Models.SchemaHash(srcModel);
            while (true)
            {
                // missing from target col?
                if (!destCol.Models.Have(mid))
                {
                    // copy it over
                    JsonObject model = JsonObject.Parse(Utils.JsonToString(srcModel));
                    model["id"] = JsonValue.CreateNumberValue(mid);
                    model["mod"] = JsonValue.CreateNumberValue(DateTimeOffset.Now.ToUnixTimeSeconds());
                    model["usn"] = JsonValue.CreateNumberValue(destCol.Usn);
                    destCol.Models.Update(model);
                    break;
                }
                // there's an existing model; do the schemas match?
                JsonObject dstModel = destCol.Models.Get(mid);
                string dstScm = destCol.Models.SchemaHash(dstModel);
                if (srcScm.Equals(dstScm))
                {
                    // they do; we can reuse this mid
                    JsonObject model = JsonObject.Parse(Utils.JsonToString(srcModel));
                    model["id"] = JsonValue.CreateNumberValue(mid);
                    model["mod"] = JsonValue.CreateNumberValue(DateTimeOffset.Now.ToUnixTimeSeconds());
                    model["usn"] = JsonValue.CreateNumberValue(destCol.Usn);
                    destCol.Models.Update(model);
                    break;
                }
                // as they don't match, try next id
                mid += 1;
            }
            // save map and return new mid
            modelMap.Add(srcMid, mid);
            return mid;
        }

        /// <summary>
        /// Given did in src col, return local id
        /// </summary>
        /// <param name="did"></param>
        /// <returns></returns>
        private long GetDid(long did)
        {
            // already converted?
            if (deckMapSourceToDest.ContainsKey(did))
            {
                return deckMapSourceToDest[did];
            }
            // get the name in src
            JsonObject g = sourceCol.Deck.Get(did);
            string name = g.GetNamedString("name");

            // if there's a prefix, replace the top level deck
            if (!String.IsNullOrEmpty(deckPrefix))
            {
                List<string> parts = name.Split(new string[] { "::" }, 
                                    StringSplitOptions.None).ToList();
                string tmpname = String.Join("::", parts.GetRange(1, parts.Count));
                name = deckPrefix;
                if (!String.IsNullOrEmpty(tmpname))
                {
                    name += "::" + tmpname;
                }
            }
            // Manually create any parents so we can pull in descriptions
            string head = "";
            List<string> parents = name.Split(new string[] { "::" },
                                    StringSplitOptions.None).ToList();
            foreach (string parent in parents.GetRange(0, parents.Count - 1))
            {
                if (!String.IsNullOrEmpty(head))
                {
                    head += "::";
                }
                head += parent;
                long? idInSrc = sourceCol.Deck.AddOrResuedDeck(head);
                if (idInSrc == null)
                    throw new Exception("Anki2Importer.Did Invalid ID!");
                GetDid((long)idInSrc);
            }
            // create in local
            long? newidRef = destCol.Deck.AddOrResuedDeck(name);
            if (newidRef == null)
                throw new Exception("Anki2Importer.Did Invalid ID!");
            long newID = (long)newidRef;
            // pull conf over            
            if (g.ContainsKey("conf") && g.GetNamedNumber("conf") > (int)ConfigPresets.DueOnly)
            {
                JsonObject conf = sourceCol.Deck.GetConf((long)g.GetNamedNumber("conf"));
                destCol.Deck.Save(conf);
                destCol.Deck.UpdateConf(conf);
                JsonObject g2 = destCol.Deck.Get(newID);
                g2["conf"] = g.GetNamedValue("conf");
                destCol.Deck.Save(g2);
            }
            // save desc
            JsonObject deck = destCol.Deck.Get(newID);
            deck["desc"] = g.GetNamedValue("desc");
            destCol.Deck.Save(deck);
            // add to deck map and return
            deckMapSourceToDest.Add(did, newID);
            return newID;
        }

        private void ImportCards()
        {
            // build map of guid -> (ord -> cid) and used id cache
            this.cards = new Dictionary<string, Dictionary<int, long>>();
            Dictionary<long, bool> existing = new Dictionary<long, bool>();
            var listCard = destCol.Database.QueryColumn<CardTable>(
                    "select distinct c.ord, c.id, c.nid from cards c, notes f where c.nid = f.id");
            var listNote = destCol.Database.QueryColumn<NoteTable>(
                    "select distinct f.guid, f.id from notes f, cards c where c.nid = f.id");
            var list = from c in listCard
                       join n in listNote
                       on c.Nid equals n.Id
                       select new { Guid = n.GuId, Id = c.Id, Ord = c.Ord };

            foreach (var l in list)
            {
                existing.Add(l.Id, true);
                if (this.cards.ContainsKey(l.Guid))
                {
                    this.cards[l.Guid].Add(l.Ord, l.Id);
                }
                else
                {
                    Dictionary<int, long> map = new Dictionary<int, long>();
                    map.Add(l.Ord, l.Id);
                    this.cards.Add(l.Guid, map);
                }
            }
            // loop through src
            List<object[]> cards = new List<object[]>();
            List<object[]> revlog = new List<object[]>();
            int cnt = 0;
            int usn = destCol.Usn;
            long aheadBy = sourceCol.Sched.Today - destCol.Sched.Today;
            listCard = sourceCol.Database.QueryColumn<CardTable>(
                    "select distinct c.* from cards c, notes f where c.nid = f.id");
            listNote = sourceCol.Database.QueryColumn<NoteTable>(
                    "select distinct f.guid, f.id, f.mid from notes f, cards c where  f.id = c.nid");
            var newList = from c in listCard
                          join n in listNote
                          on c.Nid equals n.Id
                          select new { Card = c, Guid = n.GuId, Mid = n.Mid };

            foreach (var nl in newList)
            {
                string guid = nl.Guid;
                if (changedGuids.ContainsKey(guid))
                {
                    guid = changedGuids[guid];
                }
                if (ignoredGuids.ContainsKey(guid))
                {
                    continue;
                }
                // does the card's note exist in dst col?
                if (!notes.ContainsKey(guid))
                {
                    continue;
                }
                object[] dnid = notes[guid];
                // does the card already exist in the dst col?
                int ord = nl.Card.Ord;
                if (this.cards.ContainsKey(guid) && this.cards[guid].ContainsKey(ord))
                {
                    // fixme: in future, could update if newer mod time
                    continue;
                }
                // doesn't exist. strip off note info, and save src id for later
                object[] card = new object[] { nl.Card.Id,
                        nl.Card.Nid, nl.Card.Did, nl.Card.Ord, nl.Card.Mod, nl.Card.Usn,
                        nl.Card.Type, nl.Card.Queue, nl.Card.Due, nl.Card.Interval, nl.Card.Factor,
                        nl.Card.Reps, nl.Card.Lapses, nl.Card.Left, nl.Card.ODue,
                        nl.Card.ODid, nl.Card.Flags, nl.Card.Data };
                long scid = nl.Card.Id;
                // ensure the card id is unique
                while (existing.ContainsKey(nl.Card.Id))
                {
                    nl.Card.Id = nl.Card.Id + 999;
                }
                card[0] = nl.Card.Id;
                existing.Add(nl.Card.Id, true);
                // update cid, nid, etc
                card[1] = notes[guid][0];

                var deckId = GetDid(nl.Card.Did);
                importedDeckIdMap[deckId] = nl.Card.Did;
                card[2] = deckId;                

                card[4] = DateTimeOffset.Now.ToUnixTimeSeconds();
                card[5] = usn;
                // review cards have a due date relative to collection
                if (nl.Card.Queue == 2 || nl.Card.Queue == 3 || nl.Card.Type == 2)
                {
                    nl.Card.Due = nl.Card.Due - aheadBy;
                }
                card[8] = nl.Card.Due;
                // if odid true, convert card from filtered to normal
                if (nl.Card.ODid != 0)
                {
                    nl.Card.ODid = 0;
                    card[15] = nl.Card.ODid;
                    // odue
                    card[8] = card[14];
                    card[14] = 0;

                    if (nl.Card.Type == 1)
                    {
                        nl.Card.Queue = 0;
                        nl.Card.Type = 0;
                        card[6] = nl.Card.Type;
                        card[7] = nl.Card.Queue;
                    }
                    else
                    {
                        card[7] = card[6];
                    }
                }
                cards.Add(card);
                // we need to import revlog, rewriting card ids and bumping usn
                var listRevLog = sourceCol.Database.QueryColumn<revlog>("select * from revlog where cid = " + scid);
                foreach (var rlog in listRevLog)
                {
                    object[] rev = new object[] { rlog.Id, rlog.Cid, rlog.Usn, rlog.Ease,
                            rlog.Interval, rlog.LastInterval, rlog.Factor, rlog.Time, rlog.Type };
                    rev[1] = card[0];
                    rev[2] = destCol.Usn;
                    revlog.Add(rev);
                }
                cnt += 1;
            }

            //Don't use excute many here as we've already call RunInTransaction in Import
            foreach (var c in cards)
            {
                destCol.Database.Execute("insert or ignore into cards values (?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?,?)", c);
            }
            foreach (var r in revlog)
                destCol.Database.Execute("insert or ignore into revlog values (?,?,?,?,?,?,?,?,?)", r);
        }

        /// <summary>
        /// This func only applies to imports of .anki2. For .apkg files, the
        /// apkg importer does the copying
        /// </summary>
        [Obsolete]
        private async Task ImportStaticMedia()
        {
            // Import any '_foo' prefixed media files regardless of whether
            // they're used on notes or not
            string dir = sourceCol.Media.MediaFolder.Name;
            
            StorageFolder mediaFolder = await sourceCol.Folder.TryGetItemAsync(dir) as StorageFolder;
            var files = await mediaFolder.GetFilesAsync();
            if (mediaFolder == null)
            {
                return;
            }
            foreach (var file in files)
            {
                if (file.Name.StartsWith("_") && ! (await destCol.Media.HaveInMainFolder(file.Name)))
                {
                    await CopyToDestMediaFolderAsync(file.Name, await GetSourceMediaFile(file.Name));
                }
            }
        }

        private enum MediaFolder
        {
            DestinationFolder,
            SourceFolder
        }

        protected async virtual Task<StorageFile> GetSourceMediaFile(string fname)
        {
            return await sourceCol.Media.MediaFolder
                         .TryGetItemAsync(fname) as StorageFile;
        }

        private async Task<StorageFile> GetDestMediaFile(string fname)
        {
            StorageFile file = await destCol.Media.MediaFolder.TryGetItemAsync(fname) as StorageFile;
            return file;
        }

        [Obsolete]
        private async Task CopyToDestMediaFolderAsync(string fname, StorageFile sourceFile)
        {
            try
            {
                var task = sourceFile.CopyAsync(destCol.Media.MediaFolder, fname.Normalize(NormalizationForm.FormC),
                                                NameCollisionOption.ReplaceExisting);                
                // Mark file addition to media db (see note in Media.java)
                await destCol.Media.MarkFileAdd(sourceFile);
            }
            catch (IOException)
            {
                // the user likely used subdirectories
                throw new WriteToDestinationMediaExeption("Anki2Importer.WriteDstMedia: Failed!");
            }
        }

        [Obsolete]
        private async Task<string> MungeMedia(long mid, string fields)
        {
            foreach (Regex p in Media.RegExps)
            {
                MatchCollection matches = p.Matches(fields);
                StringBuilder sb = new StringBuilder();
                foreach (Match m in matches)
                {
                    string fname = m.Groups["fname"].Value;
                    StorageFile srcData = await GetSourceMediaFile(fname);
                    StorageFile dstData = await GetDestMediaFile(fname);
                    if (srcData == null)
                    {
                        // file was not in source, ignore
                        sb.AppendAndReplace(m.GetGroup(0), fields, m);
                        continue;
                    }

                    
                    string[] split = Utils.SplitNameAndExtension(fname);
                    string name = split[0];
                    string ext = split[1];
                    // if model-local file exists from a previous import, use that
                    string lname = String.Format(Media.locale, "{0}_{1}{2}", name, mid, ext);
                    if (await destCol.Media.HaveInMainFolder(lname))
                    {
                        sb.AppendAndReplace(m.GetGroup(0).Replace(fname, lname), fields, m);
                        continue;
                    }
                    else if (dstData == null || CompareMedia(srcData, dstData)) // If missing or the same, pass unmodified
                    {
                        //Need to copy?
                        if (dstData == null)
                            await CopyToDestMediaFolderAsync(fname, srcData);

                        sb.AppendAndReplace(m.GetGroup(0), fields, m);
                        continue;
                    }
                    //Exists but does not match, so we need to dedupe
                    await CopyToDestMediaFolderAsync(lname, srcData);
                    sb.AppendAndReplace(m.GetGroup(0).Replace(fname, lname), fields, m);
                }
                if(sb.Length != 0)
                    fields = sb.ToString();
            }
            return fields;
        }

        private bool CompareMedia(StorageFile srcData, StorageFile desData, int compareLength = MEDIA_PICK_LIMIT*2)
        {
            using (FileStream srcStream = new FileStream(srcData.Path, FileMode.Open, FileAccess.Read, FileShare.Read, MEDIA_PICK_LIMIT))
            using (FileStream desStream = new FileStream(desData.Path, FileMode.Open, FileAccess.Read, FileShare.Read, MEDIA_PICK_LIMIT))
            {
                //Make sure they has the same length
                if (srcStream.Length != desStream.Length)
                    return false;

                byte[] bufSrc = new byte[MEDIA_PICK_LIMIT];
                byte[] bufDes = new byte[MEDIA_PICK_LIMIT];
                int oldReadLength = 0;
                for (int readLength = 0; readLength < compareLength;)
                {
                    readLength += srcStream.Read(bufSrc, 0, MEDIA_PICK_LIMIT);
                    //Reach end of file
                    if (oldReadLength == readLength)
                        return true;
                    oldReadLength = readLength;

                    //Compare with destination
                    desStream.Read(bufDes, 0, MEDIA_PICK_LIMIT);
                    if (!bufSrc.SequenceEqual(bufDes))
                        return false;
                }
                return true;
            }
        }

        /// <summary>
        /// Post-import cleanup
        /// </summary>
        private void PostImport()
        {
            foreach (long did in deckMapSourceToDest.Values)
            {
                destCol.Sched.MaybeRandomizeDeck(did);
            }
            // make sure new position is correct
            JsonValue value = JsonValue.CreateNumberValue(
                              destCol.Database.QueryScalar<long>(
                              "select max(due)+1 from cards where type = 0"));
            destCol.Conf["nextPos"] = value;
            destCol.Save();
        }

        /// <summary>
        /// This method is only used for testing
        /// </summary>
        /// <param name="b"></param>
        public void SetDupeOnSchemaChange(bool b)
        {
            dupeOnSchemaChange = b;
        }
    }

    public class WriteToDestinationMediaExeption : Exception
    {
        public WriteToDestinationMediaExeption() : base() { }
        public WriteToDestinationMediaExeption(string msg) : base(msg) { }
    }

    public class AnkiImportException : Exception
    {
        private AnkiImportFinishCode error;
        public AnkiImportFinishCode Error { get { return error; } }

        public AnkiImportException(AnkiImportFinishCode error) : base() { this.error = error; }
        public AnkiImportException(AnkiImportFinishCode error, string msg) : base(msg) { this.error = error; }
    }

    public enum AnkiImportFinishCode
    {
        Success,
        NotFoundValidDecks,
        UnableToUnzip,
        NotFoundMediaFile,
        NotFoundCollection,
        MediaFileIsCorrupted,
        UnknownExpception
    }
}
