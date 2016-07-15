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

using AnkiU.AnkiCore;
using AnkiU.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace AnkiU.Anki
{
    public class DeckPreferences<T> where T : IDeckPreference, new()
    {
        protected List<long> ToRemoveFromDatabaseList;
        protected List<long> ToAddToDatabaseDeckList;
        //Use Dictionary to ensure uniqueness  
        protected Dictionary<long, bool> ToUpdateToDatabaseDeckDict;

        protected Dictionary<long, T> deckPrefDict;       

        public DeckPreferences(List<T> deckInkPrefList)
        {
            ToRemoveFromDatabaseList = new List<long>();
            ToAddToDatabaseDeckList = new List<long>();
            ToUpdateToDatabaseDeckDict = new Dictionary<long, bool>();

            deckPrefDict = new Dictionary<long, T>();
            foreach (var deck in deckInkPrefList)
            {
                deckPrefDict.Add(deck.Id, deck);
            }
        }

        public virtual bool IsEmpty()
        {
            if (deckPrefDict.Count == 0)
                return true;
            return false;
        }

        public virtual bool HasId(long deckId)
        {
            if(deckPrefDict.ContainsKey(deckId))
                    return true;            
            return false;
        }

        public virtual JsonValue GetJsonPref(long deckId, string name)
        {
            return deckPrefDict[deckId].GetPreferenceJson(name);
        }

        public virtual void SetDeckPrefJson(long deckId, string name, JsonValue value)
        {
            deckPrefDict[deckId].AddOrChangePreferenceJson(name, value);
            ToUpdateToDatabaseDeckDict[deckId] = true;
        }

        public virtual void AddNewDeckPref(long deckId)
        {
            T newDeck = new T();
            newDeck.Id = deckId;
            deckPrefDict.Add(deckId, newDeck);
            ToAddToDatabaseDeckList.Add(deckId);
            ToRemoveFromDatabaseList.Remove(deckId);
        }

        public virtual void RemoveDeckInkPref(long deckId)
        {
            deckPrefDict.Remove(deckId);
            ToRemoveFromDatabaseList.Add(deckId);
            ToAddToDatabaseDeckList.Remove(deckId);
            ToUpdateToDatabaseDeckDict.Remove(deckId);
        }

        public virtual void SaveToDatabase(DB database)
        {
            database.RunInTransaction(() =>
            {
                foreach (var deckId in ToRemoveFromDatabaseList)
                    database.Delete<InkPreference>(deckId);

                foreach (var deckId in ToAddToDatabaseDeckList)
                    database.InsertOrReplace(deckPrefDict[deckId]);

                foreach (var deckId in ToUpdateToDatabaseDeckDict.Keys)
                    database.Update(deckPrefDict[deckId]);
            });
        }

    }
}
