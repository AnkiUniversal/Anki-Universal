using System;
using System.Collections.Generic;
using System.Text;
using Windows.Storage;
using Windows.Data.Json;

namespace AnkiU.AnkiCore
{
    class Collection
    {
        private DB database;
        private bool isSever;
        private double lastSave;
        private Media media;
        private int usn;
        private Sched sched;
        private Deck decks;

        public DB Database { get { return database; } }
        public bool IsServer { get { return isSever; } }
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

        public Sched Sched { get { return sched; } }

        public Deck Decks { get { return decks; } }

        public string GetPath()
        {
            //Relative or absolute?
            //Remember to recheck the Media class
            //when implementing this function
            throw new NotImplementedException();
        }

        public void Log(string message)
        {
            throw new NotImplementedException();
        }

        public void Log(string message, StorageFile file)
        {
            throw new NotImplementedException();
        }

        public Models GetModels()
        {
            throw new NotImplementedException();
        }

        public DB GetDB()
        {
            return database;
        }

        public Note GetNote(long id)
        {
            throw new NotImplementedException();
            //return new Note(this, id);
        }

        public JsonObject GetConf()
        {
            throw new NotImplementedException();
        }

        public void LogRem(long[] ids, RemovalType type)
        {
            throw new NotImplementedException();
        }

        public void RemoveCards(long[] ids, bool notes = true)
        {
            throw new NotImplementedException();
        }

        public void ModSchema(bool check = true)
        {
            throw new NotImplementedException();
        }

        public Dictionary<string, string> RenderQA(object[] data, string qfmt = null, string afmt = null)
        {
            throw new NotImplementedException();
        }

        public List<long> GenCards(long[] nids)
        {
            throw new NotImplementedException();
        }

        public void UpdateFieldCache(long[] nids)
        {
            throw new NotImplementedException();
        }

        /// <summary>
        ///  Mark DB modified. DB operations and the deck/tag/model managers do this automatically, so this is only necessary
        ///  if you modify properties of this object or the conf dict.
        /// </summary>
        public void SetMod()
        {
            database.IsModified = true;
        }
    }
}
