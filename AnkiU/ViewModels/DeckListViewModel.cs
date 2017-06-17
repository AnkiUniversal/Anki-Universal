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
using Windows.UI.Xaml;
using AnkiU.Interfaces;
using AnkiU.UIUtilities;
using Shared;
using System.Diagnostics;
using Windows.UI;

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

        public void ToggleChildrenVisibility(DeckInformation parent, IAnkiDecksView decksView)
        {
            var children = GetChildren(parent);            
            parent.IsShowChildren = !parent.IsShowChildren;

            foreach (var deck in children.Desks)
            {
                if (parent.IsShowChildren)
                {
                    if (Collection.Deck.IsParent(parent.Name, deck.Name))
                        deck.Visibility = Visibility.Visible;
                }
                else
                {
                    deck.Visibility = Visibility.Collapsed;
                    deck.IsShowChildren = false;
                }
                var item = decksView.GetItemView(deck);
                if (item != null)
                    item.Visibility = deck.Visibility;                
            }
        }

        public void ShowAllDecks(IAnkiDecksView decksView)
        {
            foreach (var deck in Decks)
            {                
                deck.Visibility = Visibility.Visible;
                deck.IsShowChildren = true;                
                var item = decksView.GetItemView(deck);
                if (item != null)
                    item.Visibility = deck.Visibility;
            }
        }

        public bool HasDeck(long deckId)
        {
            foreach(var deck in Decks)            
                if (deck.Id == deckId)
                    return true;
            return false;
        }

        public DeckInformation GetDeck(string baseName)
        {
            foreach(var deck in decks)
            {
                if (deck.BaseName.Equals(baseName, StringComparison.OrdinalIgnoreCase))
                    return deck;
            }
            return null;
        }

        public DeckInformation GetDeck(long deckId)
        {
            return Decks.FirstOrDefault((x) => { return x.Id == deckId; });
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

            UpdatePrimaryTile();
        }

        public void AddNewDeck(JsonObject deck)
        {
            long did = (long)JsonHelper.GetNameNumber(deck,"id");

            //Previously, AnkiU hide default deck in all cases.
            //Since we start to support AnkiWeb, we'll have to show default deck if it has cards to review                
            if (did == Constant.DEFAULTDECK_ID)
            {
                if (Collection.CardCount(did) < 1)
                    return;
            }

            string name = deck.GetNamedString("name");

            AddNewDeckAndCardCount(did, name);
        }

        public void AddNewDeck(long deckId)
        {
            //Previously, AnkiU hide default deck in all cases.
            //Since we start to support AnkiWeb, we'll have to show default deck if it has cards to review                
            if (deckId == Constant.DEFAULTDECK_ID)
            {
                if (Collection.CardCount(deckId) < 1)
                    return;
            }

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

        public List<string> GetAllDeckBaseName()
        {
            List<string> names = new List<string>();
            foreach (var deck in decks)
                names.Add(deck.BaseName);
            return names;
        }

        public string GetNewFullName(DeckInformation deck, string newBaseName)
        {
            var split = deck.Name.Split(new string[] { Constant.SUBDECK_SEPERATE }, StringSplitOptions.RemoveEmptyEntries);
            if (split.Length < 2)
                return newBaseName;

            split[split.Length - 1] = newBaseName;
            return String.Join(Constant.SUBDECK_SEPERATE, split);
        }

        public void UpdateDeckName(DeckInformation deck, bool isAlsoUpdateChildren = true)
        {
            deck.Name = Collection.Deck.GetDeckName(deck.Id);            

            if(isAlsoUpdateChildren)
            {
                var children = GetChildren(deck);
                foreach(var child in children.Desks)
                {
                    child.Name = Collection.Deck.GetDeckName(child.Id);
                }
            }
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
            UpdatePrimaryTile();
            var task = UpdateAllSecondaryTilesIfHas();
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

            UpdatePrimaryTile();
            var task = UpdateSecondaryTileIfHas(deck);
        }

        public async Task RemoveDeck(long deckId)
        {
            var toRemove = GetDeck(deckId);
            //Change back to default image to delete deck Image if has
            await toRemove.ChangeBackToDefaultImage();            
            SubtractCardsCountFromTotalIfNotChild(toRemove);
            Decks.Remove(toRemove);
            await RemoveSecondaryTileIfHas(deckId);
        }

        public void SortByName()
        {
            sortBy = SortBy.Name;
            UpdateUserPrefs();

            var listDeck = Decks.ToList();
            SortAllDecks(listDeck, DoSortByName);
            UpdateDecks(listDeck);
        }

        public void SortByDateAdded()
        {
            sortBy = SortBy.DateAdded;
            UpdateUserPrefs();

            var listDeck = Decks.ToList();            
            SortAllDecks(listDeck, DoSortByDate);
            UpdateDecks(listDeck);
        }

        public void UpdatePrimaryTile()
        {
            TilesHelper.UpdatePrimaryTile(TotalNewCards, TotalDueCards);                        
        }

        public async Task UpdateAllSecondaryTilesIfHas()
        {
            try
            {
                var tiles = await TilesHelper.FindAllSecondaryTilesAsync();
                foreach (var tile in tiles)
                {
                    long deckId = 0;
                    var success = long.TryParse(tile.TileId, out deckId);
                    if (success)
                    {
                        var deck = GetDeck(deckId);
                        TilesHelper.SendSecondaryTileNotification(tile.TileId, deck.NewCards.ToString(), deck.DueCards.ToString());
                        tile.VisualElements.BackgroundColor = GetColors(deck);

                        await tile.UpdateAsync();
                    }
                }
            }
            catch(Exception e)
            { //App should not crash if any error happen
                Debug.WriteLine("DeckListViewModel.UpdateAllSecondaryTilesIfHas: " + e.Message);
            }
        }

        public static Windows.UI.Color GetColors(DeckInformation deck)
        {
            if (deck.NewCards + deck.DueCards > 0)
                return UIHelper.DeckWithNewOrDueCardsBrush.Color;
            else
                return UIHelper.AppDefaultTileBackgroundBrush.Color;
        }

        private async Task UpdateSecondaryTileIfHas(DeckInformation deck)
        {
            try
            {
                var color = GetColors(deck);
                await TilesHelper.UpdateTile(deck.Id.ToString(), deck.NewCards.ToString(), deck.DueCards.ToString(), color);

            }
            catch (Exception e)
            { //App should not crash if any error happen
                Debug.WriteLine("DeckListViewModel.DeleteSecondaryTileIfHas: " + e.Message);
            }
        }

        private async Task RemoveSecondaryTileIfHas(long deckId)
        {
            try
            {
                await TilesHelper.RemoveTile(deckId.ToString());

            }
            catch (Exception e)
            { //App should not crash if any error happen
                Debug.WriteLine("DeckListViewModel.DeleteSecondaryTileIfHas: " + e.Message);
            }
        }

        private void SortAllDecks(List<DeckInformation> decks, Comparison<DeckInformation> comarison)
        {
            decks.Sort(comarison);

            for (int i = 0; i < decks.Count; i++)
            {
                for (int j = i + 1; j < decks.Count; j++)
                {
                    if (Collection.Deck.IsParent(decks[i].Name, decks[j].Name))
                    {
                        decks[i].IsParent = true;
                        ChangePosition(decks, i + 1, j);
                    }
                }
            }
        }

        private int DoSortByName(DeckInformation first, DeckInformation second)
        {
            return SortBySubDeckLevel(first, second, (x, y) => { return x.BaseName.CompareTo(y.BaseName); });
        }

        private int DoSortByDate(DeckInformation first, DeckInformation second)
        {
            return SortBySubDeckLevel(first, second, (x, y) => { return x.Id.CompareTo(y.Id); });
        }

        private int SortBySubDeckLevel(DeckInformation first, DeckInformation second, Comparison<DeckInformation> comarison)
        {
            int firstChildLevel = first.ChildLevel;
            int secondChildLevel = second.ChildLevel;

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

        public void DragAnDrop(DeckInformation parent, DeckInformation child, IAnkiDecksView deckView)
        {
            var oldName = child.Name;
            var oldParent = child.ParentName;
            var childrenOfChild = GetChildren(child);

            if (Collection.Deck.IsParent(parent.Name, child.Name))
                RemoveFromSubDeck(child); //Remove from children if child deck is dragged on parent deck
            else
                CreateSubDeck(parent, child);

            //Children name of child must be updated later
            UpdateDeckName(child, false);
            //If name does not change no need to update anything
            if (oldName.Equals(child.Name, StringComparison.OrdinalIgnoreCase))            
                return;

            //Remove children of child first to avoid sorting errors
            RemoveChildrenFromDecks(childrenOfChild);

            if (Collection.Deck.IsParent(parent.Name, child.Name))
            {
                parent.IsParent = true;
                if(!parent.IsShowChildren)
                    ToggleChildrenVisibility(parent, deckView);
                SortIntoSubdeck(parent, child);
            }
            else
                ResortNonSubdeck(child);

            SetInShowChildrenState(child);
            UpdateChildrenPositionAndName(child, childrenOfChild);
            UpdateOldParent(oldParent);

            UpdateCardCountAllDecks();
            Collection.SaveAndCommitAsync();
        }

        private void RemoveFromSubDeck(DeckInformation child)
        {            
            Collection.Deck.RenameForDragAndDrop(child.Id, null);
        }

        private void CreateSubDeck(DeckInformation parent, DeckInformation child)
        {            
            Collection.Deck.RenameForDragAndDrop(child.Id, parent.Id);
        }

        private static void SetInShowChildrenState(DeckInformation child)
        {
            child.Visibility = Visibility.Visible;
            child.IsShowChildren = true;
        }

        private void UpdateOldParent(string parentName)
        {
            if (String.IsNullOrEmpty(parentName))
                return;

            var parent = GetDeck(parentName);
            if (parent == null)
                return;

            var nextIndex = decks.IndexOf(parent) + 1;
            if ((nextIndex >= decks.Count )
                || (decks[nextIndex].ChildLevel <= parent.ChildLevel))
                parent.IsParent = false;
            else
                parent.IsParent = true;
        }

        private struct SubDecks
        {
            /// <summary>
            /// The index of the first sub-deck
            /// </summary>
            public int StartIndex { get; set; }
            public List<DeckInformation> Desks { get; set; }
        }
        private SubDecks GetChildren(DeckInformation deck)
        {
            SubDecks children = new SubDecks();
            children.Desks = new List<DeckInformation>();
            children.StartIndex = decks.IndexOf(deck) + 1;

            var level = deck.ChildLevel;
            for (int i = children.StartIndex; i < decks.Count; i++)
            {
                var childLevel = decks[i].ChildLevel;
                if (level >= childLevel)
                    break;
                children.Desks.Add(decks[i]);
            }
            return children;
        }

        /// <summary>
        /// Sort a sub-deck into its parent
        /// </summary>
        /// <param name="parent"></param>
        /// <param name="child"></param>
        private void SortIntoSubdeck(DeckInformation parent, DeckInformation child)
        {
            int currentIndex = decks.IndexOf(child);
            int parentIndex = decks.IndexOf(parent);
            //No need to do anything if it's already next to its parent
            if (currentIndex == parentIndex + 1)
                return;

            Decks.Remove(child);
            int childLevel = child.ChildLevel;
            parentIndex = decks.IndexOf(parent);

            for (int i = parentIndex + 1; i < decks.Count; i++)
            {
                int level = decks[i].ChildLevel;
                if (level > childLevel)
                    continue;

                if ((level < childLevel) || (Compare(child, decks[i]) < 0))
                {
                    Decks.Insert(i, child);
                    return;
                }
            }

            Decks.Add(child);
        }

        /// <summary>
        /// Re-Sort a non-subdeck
        /// </summary>
        /// <param name="deck"></param>
        public void ResortNonSubdeck(DeckInformation deck)
        {
            Decks.Remove(deck);
            for(int i = 0; i < decks.Count; i++)
            {
                if (decks[i].Name.Contains(Constant.SUBDECK_SEPERATE))
                    continue;

                if(Compare(deck, decks[i]) < 0)
                {
                    Decks.Insert(i, deck);
                    return;
                }
            }
            Decks.Add(deck);
        }

        private void RemoveChildrenFromDecks(SubDecks children)
        {
            for (int i = 0; i < children.Desks.Count; i++)
                Decks.RemoveAt(children.StartIndex);
        }

        private void UpdateChildrenPositionAndName(DeckInformation parent, SubDecks children)
        {
            if (children.Desks.Count == 0)
                return;

            var newStartIndex = decks.IndexOf(parent) + 1;
            for (int i = 0; i < children.Desks.Count; i++)
            {
                //No need to update children of a child
                //as they will be aslo updated in this for loop
                UpdateDeckName(children.Desks[i], false);
                if (parent.IsShowChildren)
                {
                    children.Desks[i].IsShowChildren = true;
                    children.Desks[i].Visibility = Visibility.Visible;
                }

                Decks.Insert(newStartIndex + i, children.Desks[i]);
            }
        }

        private static void ChangePosition(List<DeckInformation> decks, int newIndex, int oldIndex)
        {
            var temp = decks[oldIndex];
            decks.RemoveAt(oldIndex);
            if (newIndex < decks.Count)
                decks.Insert(newIndex, temp);
            else //Use add here to prevent index go out of range
                decks.Add(temp);
        }

        private int Compare(DeckInformation comparer, DeckInformation compared)
        {
            if (sortBy == SortBy.Name)
            {
                return comparer.BaseName.CompareTo(compared.BaseName);
            }
            else
            {
                return comparer.Id.CompareTo(compared.Id);
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

        private void ResetCardsCount()
        {
            TotalNewCards = 0;
            TotalDueCards = 0;
        }

    }
}
