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
using Windows.Storage;
using Windows.Data.Json;

namespace AnkiU.AnkiCore.Exporter
{
    public class AnkiExporter : Exporter
    {
        protected bool includeSched;
        protected bool includeMedia;
        protected StorageFolder mediaFolder;
        protected int count;
        protected List<string> mediaFiles = new List<string>();

        public bool IncludeSched { get { return includeSched; } set { includeSched = value; } }
        public bool IncludeMedia { get { return IncludeMedia; } set { includeMedia = value; } }

        public AnkiExporter(Collection col) : base(col)
        {
            includeSched = false;
            includeMedia = true;
        }

        public AnkiExporter(Collection col, long did) : base(col, did)
        {
            includeSched = false;
            includeMedia = true;
        }

        public virtual async Task ExportInto(StorageFolder destFolder, string fileName)
        {
            // create a new collection at the target
            StorageFile file = await destFolder.TryGetItemAsync(fileName) as StorageFile;
            if (file != null)
            {
                throw new Exception();
            }

            Collection dst = await Storage.OpenOrCreateCollection(destFolder, fileName);
            // find cards
            long[] cids;
            if (deckId == null)
            {
                var listCardID = sourceCol.Database.QueryColumn<CardIdOnlyTable>("SELECT id FROM cards");
                cids = (from s in listCardID select s.Id).ToArray();
            }
            else
            {
                cids = sourceCol.Deck.GetCardIds((long)deckId, true);
            }

            // copy cards, noting used nids (as unique set)
            var listCard = sourceCol.Database.QueryColumn<CardTable>("select * from cards where id in " + Utils.Ids2str(cids));
            dst.Database.InsertAll(listCard);

            HashSet<long> nids = new HashSet<long>(from s in listCard select s.Nid);

            // Copy notes
            List<long> uniqueNids = new List<long>(nids);
            string strnids = Utils.Ids2str(uniqueNids);
            var listNote = sourceCol.Database.QueryColumn<NoteTable>("select * from notes where id in " + strnids);
            dst.Database.InsertAll(listNote);

            // remove system tags if not exporting scheduling info
            if (!includeSched)
            {
                List<object[]> args = new List<object[]>(listNote.Count);
                object[] arg = new object[2];

                for (int row = 0; row < listNote.Count; row++)
                {
                    arg[0] = RemoveSystemTags(listNote[row].Tags);
                    arg[1] = uniqueNids[row];
                    args.Insert(row, arg);
                }
                dst.Database.ExecuteMany("UPDATE notes set tags=? where id=?", args);
            }
            // models used by the notes
            List<long> mids = (from s in listNote select s.Mid).ToList();
            // card history and revlog
            if (includeSched)
            {
                var listRevLog = sourceCol.Database.QueryColumn<revlog>("select * from revlog where cid in " + Utils.Ids2str(cids));
                dst.Database.InsertAll(listRevLog);
            }
            else
            {
                // Reset card state
                dst.Sched.ResetCardsForExport(cids);
            }

            // models - start with zero
            foreach (JsonObject m in sourceCol.Models.All())
            {
                if (mids.Contains((long)JsonHelper.GetNameNumber(m, "id")))
                {
                    dst.Models.Update(m);
                }
            }

            // decks
            List<long> dids = new List<long>();
            if (deckId != null)
            {
                dids.Add((long)deckId);
                foreach (long x in sourceCol.Deck.Children((long)deckId).Values)
                {
                    dids.Add(x);
                }
            }
            JsonObject dconfs = new JsonObject();
            foreach (JsonObject d in sourceCol.Deck.All())
            {
                if (JsonHelper.GetNameNumber(d, "id") == 1)
                {
                    continue;
                }
                if (deckId != null && !dids.Contains((long)JsonHelper.GetNameNumber(d, "id")))
                {
                    continue;
                }
                if (JsonHelper.GetNameNumber(d,"dyn") != 1 && JsonHelper.GetNameNumber(d,"conf") != 1)
                {
                    if (includeSched)
                    {
                        dconfs[JsonHelper.GetNameNumber(d,"conf").ToString()] = JsonValue.CreateBooleanValue(true);
                    }
                }
                if (!includeSched)
                {
                    // scheduling not included, so reset deck settings to default
                    d["conf"] = JsonValue.CreateNumberValue(1);
                }
                dst.Deck.Update(d);
            }

            // copy used deck confs
            foreach (JsonObject dc in sourceCol.Deck.AllConf())
            {
                if (dconfs.ContainsKey(JsonHelper.GetNameNumber(dc,"id").ToString()))
                {
                    dst.Deck.UpdateConf(dc);
                }
            }

            // find used media        
            JsonObject media = new JsonObject();
            mediaFolder = sourceCol.Media.MediaFolder;

            if (includeMedia)
            {
                for (int idx = 0; idx < listNote.Count(); idx++)
                {
                    foreach (string name in sourceCol.Media.FileNameInMediaFolder(listNote[idx].Mid, listNote[idx].Fields))
                    {
                        media[name] = JsonValue.CreateBooleanValue(true);
                    }
                }

                Dictionary<long, bool> ids = new Dictionary<long, bool>();
                foreach (var note in listNote)
                    ids[note.Mid] = true;
                var modelIds = ids.Keys.ToArray();

                if (mediaFolder != null)
                {
                    var files = await mediaFolder.GetFilesAsync();
                    foreach (StorageFile f in files)
                    {
                        if (f.Name.StartsWith("_"))
                        {
                            // Loop through every model that will be exported, and check if it contains a reference to f
                            for (int idx = 0; idx < modelIds.Length; idx++)
                            {
                                if (IsModelHasMedia(sourceCol.Models.Get(modelIds[idx]), f.Name))
                                {
                                    media[f.Name] = JsonValue.CreateBooleanValue(true);
                                    break;
                                }
                            }
                        }
                    }
                }
            }
            var keys = media.Keys.ToArray();
            if (keys != null)
            {
                for (int i = 0; i < keys.Length; i++)
                {
                    mediaFiles.Add(keys[i]);
                }
            }

            //Cleanup
            dst.Crt = sourceCol.Crt;
            // todo: tags?
            count = dst.CardCount();
            dst.SetIsModified();
            PostExport();
            dst.Close();
        }

        /// <summary>
        /// Returns whether or not the specified model contains a reference to the given media file.
        /// In order to ensure relatively fast operation we only check if the styling, front, back templates* contain* fname,
        /// and thus must allow for occasional false positives.
        /// </summary>
        /// <param name="model">The model to scan</param>
        /// <param name="fname">The name of the media file to check for</param>
        /// <returns></returns>
        private bool IsModelHasMedia(JsonObject model, string fname)
        {
            // Don't crash if the model is null
            if (model == null)
            {
                return true;
            }
            // First check the styling
            if (model.GetNamedString("css").Contains(fname))
            {
                return true;
            }
            // If not there then check the templates
            JsonArray tmpls = model.GetNamedArray("tmpls");
            for (uint idx = 0; idx < tmpls.Count; idx++)
            {
                JsonObject tmpl = tmpls.GetObjectAt(idx);
                if (tmpl.GetNamedString("qfmt").Contains(fname) 
                    || tmpl.GetNamedString("afmt").Contains(fname))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Overwrite to apply customizations to the deck before it's closed, such as update the deck description
        /// </summary>
        protected virtual void PostExport()
        {
        }

        private string RemoveSystemTags(string tags)
        {
            return sourceCol.Tags.RemoveFromStr("marked leech", tags);
        }

    }
}
