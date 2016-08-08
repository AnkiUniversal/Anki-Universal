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
    public struct DeckCardCount
    {
        public int New { get; set; }
        public int Due { get; set; }
    }

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
            Decks.Clear();
            foreach (var deck in deckList)            
                AddNewDeck(deck);

            if (sortBy == SortBy.DateAdded)
                SortByDateAdded();
            else
                SortByName();
        }

        public void AddNewDeck(JsonObject deck)
        {
            long did = (long)deck.GetNamedNumber("id");
            if (did == Constant.DEFAULTDECK_ID)
                return;

            string name = deck.GetNamedString("name");

            AddNewDeckAndCardCount(did, name);
        }

        public void AddNewDeck(long deckId)
        {
            if (deckId == Constant.DEFAULTDECK_ID)
                return;
            
            string name = Collection.Deck.GetDeckName(deckId);

            AddNewDeckAndCardCount(deckId, name);
        }

        private void AddNewDeckAndCardCount(long did, string name)
        {
            DeckCardCount count = AddNewDeckCardCount(did);
            Decks.Add(new DeckInformation(name, count.New, count.Due, did, Collection.Deck.IsDyn(did)));
        }

        private DeckCardCount AddNewDeckCardCount(long did)
        {
            Collection.Deck.Select(did, false);
            Collection.Sched.Reset();

            DeckCardCount deckCount = new DeckCardCount();
            CardTypeCounts count = Collection.Sched.AllCardTypeCounts();
            deckCount.Due = count.Learn + count.Review;
            deckCount.New = count.New;

            if (!Collection.Deck.HasParent(did))
                AddCardsCountToTotal(count.New, deckCount.Due);

            return deckCount;
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
            ResetCardsCount();
            foreach (var deck in decks)
            {                
                var deckCardCount = AddNewDeckCardCount(deck.Id);
                deck.NewCards = deckCardCount.New;
                deck.DueCards = deckCardCount.Due;
            }
        }

        public void UpdateCardCountMultiDecks(IEnumerable<long> deckIds)
        {
            foreach (var id in deckIds)
                UpdateCardCountForDeck(id);
        }

        /// <summary>
        /// Update total card count (new + review) of a deck        
        /// </summary>
        /// <param name="deckId">Id of the updated deck</param>
        /// <param name="deck">If this parameter is null, it will be retrieved by using deckId
        /// which resutls in slower performance.</param>
        public void UpdateCardCountForDeck(long deckId, DeckInformation deck = null)
        {
            if (deck == null)
                deck = GetDeck(deckId);

            SubtractCardsCountFromTotalIfNotChild(deck);
            var deckCardCount = AddNewDeckCardCount(deckId);
            
            deck.NewCards = deckCardCount.New;
            deck.DueCards = deckCardCount.Due;
        }

        public async Task RemoveDeck(long deckId)
        {
            var toRemove = GetDeck(deckId);
            //Change back to default image to delete deck Image if has
            await toRemove.ChangeBackToDefaultImage();            
            SubtractCardsCountFromTotalIfNotChild(toRemove);
            Decks.Remove(toRemove);
        }

        public void SortByName()
        {
            sortBy = SortBy.Name;
            UpdateUserPrefs();

            var listDeck = Decks.ToList();
            SortIntoSubDeck(listDeck, DoSortByName);
            UpdateDecks(listDeck);
        }

        public void SortByDateAdded()
        {
            sortBy = SortBy.DateAdded;
            UpdateUserPrefs();

            var listDeck = Decks.ToList();            
            SortIntoSubDeck(listDeck, DoSortByDate);
            UpdateDecks(listDeck);
        }

        private void SortIntoSubDeck(List<DeckInformation> decks, Comparison<DeckInformation> comarison)
        {
            decks.Sort(comarison);

            for (int i = 0; i < decks.Count; i++)
            {
                for (int j = i; j < decks.Count; j++)
                {
                    if (Collection.Deck.IsParent(decks[i].Name, decks[j].Name))
                    {
                        var temp = decks[j];
                        decks.RemoveAt(j);
                        decks.Insert(i + 1, temp);
                    }
                }
            }
        }

        private int DoSortByName(DeckInformation first, DeckInformation second)
        {
            return SortBySubDeckLevel(first, second, (x, y) => { return x.DisplayName.CompareTo(y.DisplayName); });
        }

        private int DoSortByDate(DeckInformation first, DeckInformation second)
        {
            return SortBySubDeckLevel(first, second, (x, y) => { return x.Id.CompareTo(y.Id); });
        }

        private int SortBySubDeckLevel(DeckInformation first, DeckInformation second, Comparison<DeckInformation> comarison)
        {
            int firstChildLevel = first.Name.Split(new string[] { Constant.SUBDECK_SEPERATE }, 
                StringSplitOptions.RemoveEmptyEntries).Length;
            int secondChildLevel = second.Name.Split(new string[] { Constant.SUBDECK_SEPERATE }, 
                StringSplitOptions.RemoveEmptyEntries).Length;

            if (firstChildLevel > secondChildLevel)
                return 1;
            else if (firstChildLevel < secondChildLevel)
                return -1;
            else
            {
                if(firstChildLevel == 1)                
                    return comarison(first, second);
                else // Child deck in reverse so the next sort will put them in the correct order
                    return -comarison(first, second);
            }
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

        private void SubtractCardsCountFromTotalIfNotChild(DeckInformation deckInfor)
        {
            if (Collection.Deck.HasParent(deckInfor.Name))
                return;            

            TotalNewCards -=  deckInfor.NewCards;
            TotalDueCards -= deckInfor.DueCards;
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
