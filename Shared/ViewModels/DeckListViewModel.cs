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
using Shared.Models;
using Shared.AnkiCore;
using Windows.Data.Json;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Windows.UI.Xaml;
using System.Diagnostics;
using Shared;
using Windows.UI;

namespace Shared.ViewModels
{
    public struct DeckCardCount
    {
        public int New { get; set; }
        public int Due { get; set; }
    }

    public class DeckListViewModel
    {
        public Collection Collection { get; set; }

        private List<DeckInformation> decks;
        public List<DeckInformation> Decks
        {
            get { return decks; }
            set
            {
                decks = value;
            }
        }

        public long TotalNewCards { get; set; }
        public long TotalDueCards { get; set; }

        private List<DeckDueNode> dueDeckList;

        public DeckListViewModel (Collection collection)
        {
            this.Collection = collection;
            ResetCardsCount();
            dueDeckList = collection.Sched.DeckDueTree();
            Decks = new List<DeckInformation>();            
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
            Decks.Clear();

            foreach (var deck in dueDeckList)            
                AddNewDeck(deck);

            UpdatePrimaryTile();
        }

        public void AddNewDeck(DeckDueNode deck)
        {
            long did = deck.DeckId;
            if (did == Constant.DEFAULTDECK_ID)
                return;

            foreach (var child in deck.Children)
                AddNewDeck(child);

            AddNewDeckAndCardCount(deck);
        }

        private void AddNewDeckAndCardCount(DeckDueNode deck)
        {
            Decks.Add(new DeckInformation(deck.NewCount, deck.LearnCount + deck.ReviewCount, deck.DeckId));

            if (!Collection.Deck.HasParent(deck.DeckId))
                AddCardsCountToTotal(deck.NewCount, deck.LearnCount + deck.ReviewCount);
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
                return Colors.Orange;
            else
                return Colors.DodgerBlue;
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

        private struct SubDecks
        {
            /// <summary>
            /// The index of the first sub-deck
            /// </summary>
            public int StartIndex { get; set; }
            public List<DeckInformation> Desks { get; set; }
        }     

        private void RemoveChildrenFromDecks(SubDecks children)
        {
            for (int i = 0; i < children.Desks.Count; i++)
                Decks.RemoveAt(children.StartIndex);
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

        private void UpdateDecks(List<DeckInformation> listDeck)
        {
            Decks.Clear();
            foreach (var deck in listDeck)
                Decks.Add(deck);
        }

        private void SubtractCardsCountFromTotalIfNotChild(DeckInformation deckInfor)
        {
            if (Collection.Deck.HasParent(deckInfor.Id))
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
