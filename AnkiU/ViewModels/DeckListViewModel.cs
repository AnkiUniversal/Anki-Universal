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
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnkiU.Models;
using AnkiU.AnkiCore;
using Windows.Data.Json;
using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AnkiU.ViewModels
{
    public class DeckListViewModel : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public enum SortBy
        {
            DateAdded,
            Name
        }

        private SortBy sortBy;
        
        private void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public Collection Collection { get; set; }

        private ObservableCollection<DeckInformation> decks;
        public ObservableCollection<DeckInformation> Decks
        {
            get { return decks; }
            set
            {
                decks = value;
                RaisePropertyChanged("Decks");
            }
        }

        public long TotalNewCards { get; set; }
        public long TotalDueCards { get; set; }

        public DeckListViewModel (Collection collection)
        {
            this.Collection = collection;
            ResetCardsCount();
            sortBy = (SortBy)MainPage.UserPrefs.SortDeckBy;
            Decks = new ObservableCollection<DeckInformation>();
        }

        private void ResetCardsCount()
        {
            TotalNewCards = 0;
            TotalDueCards = 0;
        }

        public bool HasDeck(long deckId)
        {
            foreach(var deck in Decks)            
                if (deck.Id == deckId)
                    return true;
            return false;
        }

        public DeckInformation GetDeck(long deckId)
        {
            return Decks.First((x) => { return x.Id == deckId; });
        }

        public void GetAllDeckInformation()
        {
            ResetCardsCount();
            var deckList = Collection.Deck.All();
            deckList.Sort((a, b) =>
            {
                if(sortBy == SortBy.DateAdded)
                    return a.GetNamedNumber("id").CompareTo(b.GetNamedNumber("id"));
                else
                    return a.GetNamedString("name").CompareTo(b.GetNamedString("name"));
            });
            Decks.Clear();
            foreach (var deck in deckList)            
                AddNewDeck(deck);            
        }

        public void AddNewDeck(JsonObject deck)
        {
            long did = (long)deck.GetNamedNumber("id");
            if (did == Constant.DEFAULTDECK_ID)
                return;

            string name = deck.GetNamedString("name");

            AddNewDeck(did, name);
        }

        public void AddNewDeck(long deckId)
        {
            if (deckId == Constant.DEFAULTDECK_ID)
                return;
            
            string name = Collection.Deck.GetDeckName(deckId);

            AddNewDeck(deckId, name);
        }

        private void AddNewDeck(long did, string name)
        {
            Collection.Deck.Select(did, false);
            Collection.Sched.Reset();

            CardTypeCounts count = Collection.Sched.AllCardTypeCounts();
            int dueCards = count.Learn + count.Review;

            TotalNewCards += count.New;
            TotalDueCards += dueCards;

            Decks.Add(new DeckInformation(name, count.New, dueCards, did, Collection.Deck.IsDyn(did)));
        }

        public void UpdateDeckName(DeckInformation deck)
        {
            deck.Name = Collection.Deck.GetDeckName(deck.Id);
        }

        public void AddOrUpdateDeckCardCount(long deckId)
        {            
            if (HasDeck(deckId))
                UpdateCardCountForDeck(deckId);
            else
                AddNewDeck(deckId);
        }

        public void UpdateCardCountAllDecks()
        {
            foreach(var deck in decks)            
                UpdateCardCountForDeck(deck.Id, deck);            
        }

        public void UpdateCardCountForDeck(long deckId, DeckInformation deck = null)
        {
            if (deck == null)
                deck = GetDeck(deckId);

            SubtractCardsCountFromTotal(deckId);

            Collection.Deck.Select(deckId, false);
            Collection.Sched.Reset();
            
            CardTypeCounts count = Collection.Sched.AllCardTypeCounts();
            deck.NewCards = count.New;
            deck.DueCards = count.Learn + count.Review;

            AddCardsCountToTotal(deck.NewCards, deck.DueCards);
        }

        public async Task RemoveDeck(long deckId)
        {
            var toRemove = GetDeck(deckId);
            //Change back to default image to delete deck Image if has
            await toRemove.ChangeBackToDefaultImage();
            SubtractCardsCountFromTotal(toRemove.NewCards, toRemove.DueCards);
            Decks.Remove(toRemove);
        }

        public void SortByName()
        {
            sortBy = SortBy.Name;
            UpdateUserPrefs();

            var listDeck = Decks.ToList();
            listDeck.Sort((x, y) => { return x.Name.CompareTo(y.Name); });
            UpdateDecks(listDeck);
        }

        public void SortByDateAdded()
        {
            sortBy = SortBy.DateAdded;
            UpdateUserPrefs();

            var listDeck = Decks.ToList();
            listDeck.Sort((x, y) => { return x.Id.CompareTo(y.Id); });
            UpdateDecks(listDeck);
        }

        private void UpdateUserPrefs()
        {
            MainPage.UserPrefs.SortDeckBy = (int)sortBy;
        }

        private void UpdateDecks(List<DeckInformation> listDeck)
        {
            Decks.Clear();
            foreach (var deck in listDeck)
                Decks.Add(deck);
        }

        private void SubtractCardsCountFromTotal(long deckId)
        {
            var deckInfor = GetDeck(deckId);
            TotalNewCards -=  deckInfor.NewCards;
            TotalDueCards -= deckInfor.DueCards;
        }

        private void SubtractCardsCountFromTotal(long newCards, long dueCards)
        {            
            TotalNewCards -= newCards;
            TotalDueCards -= dueCards;
        }

        private void AddCardsCountToTotal(long newCards, long dueCards)
        {            
            TotalNewCards += newCards;
            TotalDueCards += dueCards;
        }

        private int GetNewCountOfDeck(long deckId, JsonObject deckConf)
        {
            int lim = (int)deckConf.GetNamedObject("new").GetNamedNumber("perDay");
            return Collection.Sched.NewCountForDeck(deckId, lim);
        }

        private int GetDueCountOfDeck(long deckId, JsonObject deckConf)
        {
            int lim = (int)deckConf.GetNamedObject("rev").GetNamedNumber("perDay");
            return Collection.Sched.ReviewCountForDeck(deckId, lim);
        }
    }
}
