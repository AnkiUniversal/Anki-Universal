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
using AnkiU.Models;
using AnkiU.UIUtilities;
using AnkiU.UserControls;
using AnkiU.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace AnkiU.Pages
{
    public sealed partial class SearchPage : Page, INightReadMode
    {

        private const string IS_DUE = "is:due";
        private const string IS_NEW = "is:new";
        private const string IS_LEARN = "is:learn";
        private const string IS_REVIEW = "is:review";
        private const string IS_SUSPEND = "is:suspended";

        private int numberOfCardsAPage;

        private event EventHandler SearchStringChange;

        private bool isNightMode = false;

        private MainPage mainPage;
        private Collection collection;
        private long currentDeckId;

        private DeckNameViewModel deckNameSearchViewModel;
        private string searchDeck = "";

        private TagInformationViewModel tagInforViewModel;
        private string searchTag = "";

        private Dictionary<string, bool> cardState = new Dictionary<string, bool>();
        private string searchCardState = "";

        private CardInformationViewModel cardInforViewModel = null;
        private Dictionary<int, List<long>> searchedCardId = new Dictionary<int, List<long>>();        
        private int currentPage = 0;
        private long numberOfSearchedCards;

        private bool isSuppressEnterKey = false;
        private RescheduleFlyout rescheduleFlyout = null;
        private KeyValuePair<List<CardInformation>, List<long>> listRescheduleCard;

        private KeyValuePair<SearchSortColumn, bool> currentSortColumn;

        private CardViewPopup cardViewPopup;

        public SearchPage()
        {
            this.InitializeComponent();      
                  
        }

        private void SearchStringChangeHandler(object sender, EventArgs e)
        {
            searchTextBox.Text = searchDeck  + searchTag + searchCardState;
        }

        public void ToggleReadMode()
        {
            isNightMode = !isNightMode;
            ChangeBackgroundColor();
            if (cardViewPopup != null)
                cardViewPopup.ToggleReadMode();
        }

        private void ChangeBackgroundColor()
        {
            UIHelper.ToggleNightLight(isNightMode, this);
            if (isNightMode)
            {
                cardInformationView.Background = new SolidColorBrush(UIHelper.ContentNightModeColor);
                searchTextBox.Background = new SolidColorBrush(Windows.UI.Colors.Black);
            }
            else
            {
                cardInformationView.Background = new SolidColorBrush(Windows.UI.Colors.White);
                searchTextBox.Background = cardInformationView.Background;
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            mainPage = e.Parameter as MainPage;
            if (this.mainPage == null)
                throw new Exception("Wrong input parameter!");

            collection = mainPage.Collection;
            currentDeckId = collection.Deck.Selected();

            SetupDefaultCardsAPage();
            SetupCardListView();
            SetupCartStateSelection();
            SetupDeckSelection();
            SetupTagSelection();
            HookAllEvents();
        }

        private void SetupCardListView()
        {
            SearchStringChange += SearchStringChangeHandler;
            currentSortColumn = new KeyValuePair<SearchSortColumn, bool>(cardInformationView.CurrentSortColumn, false);
            cardInformationView.SortColumnChangedEvent += CardInformationViewSorttColumnChangedHandler;
            cardInformationView.CardListViewMenuFlyout = Resources["CardListViewContextMenu"] as MenuFlyout;
            cardInformationView.CardListViewMenuFlyout.Closed += (s, args) => { cardInformationView.CardShowMenuFlyout = null; };

            pageButtonRoot.Visibility = Visibility.Collapsed;
        }

        private void SetupCartStateSelection()
        {
            cardState.Add(IS_DUE, false);
            cardState.Add(IS_NEW, false);
            cardState.Add(IS_LEARN, false);
            cardState.Add(IS_REVIEW, false);
            cardState.Add(IS_SUSPEND, false);
        }

        private void SetupDefaultCardsAPage()
        {
            if (UIHelper.GetDeviceFamily() == "Windows.Mobile")
                numberOfCardsAPage = 10;
            else
                numberOfCardsAPage = 20;
        }

        private void SetupTagSelection()
        {
            tagInforViewModel = new TagInformationViewModel(collection, collection.NewNote());
            tagInformationView.ViewModel = tagInforViewModel;
            tagInformationView.TagFlyoutClosedEvent += TagFlyoutClosedEventHandler;
        }

        private void SetupDeckSelection()
        {
            deckNameSearchViewModel = new DeckNameViewModel(collection);
            deckNameView.DataContext = deckNameSearchViewModel.Decks;
            deckNameView.SelectionChangedEvent += DeckNameViewSelectionChangedHandler;
            deckNameView.ChangeSelectedItem(currentDeckId);
        }

        private void CardInformationViewSorttColumnChangedHandler(SearchSortColumn column, bool isReverse)
        {
            currentSortColumn = new KeyValuePair<SearchSortColumn, bool>(column, isReverse);
            if (cardInforViewModel == null)
                return;
            SortCardInforViewModel(column, isReverse);
        }

        private void SortCardInforViewModel(SearchSortColumn column, bool isReverse)
        {
            switch (column)
            {
                case SearchSortColumn.SortField:
                    cardInforViewModel.SortWithSortField(isReverse);
                    break;
                case SearchSortColumn.Question:
                    cardInforViewModel.SortWithQuestion(isReverse);
                    break;
                case SearchSortColumn.Answer:
                    cardInforViewModel.SortWithAnswer(isReverse);
                    break;
                case SearchSortColumn.Due:
                    cardInforViewModel.SortWithDue(isReverse);
                    break;
                case SearchSortColumn.Lapse:
                    cardInforViewModel.SortWithLapse(isReverse);
                    break;
            }
        }

        private void TagFlyoutClosedEventHandler(object sender, EventArgs e)
        {
            StringBuilder builder = new StringBuilder();
            AppendTags(builder, "tag:", tagInforViewModel);
            searchTag = builder.ToString();
            SearchStringChange(null, null);
        }

        public static void AppendTags(StringBuilder tags, string prefix, TagInformationViewModel viewModel)
        {
            foreach (var tag in viewModel.CurrentNote.Tags)
            {
                tags.Append(prefix);
                tags.Append(tag);
                tags.Append(" ");
            }
        }

        private void HookAllEvents()
        {
            mainPage.HookZooming(cardInformationView);
            mainPage.IsAutoSwitchZoomButtonToSecondary = false;
            mainPage.ZoomButtonsSeparator.Visibility = Visibility.Collapsed;
            if (mainPage.WindowSizeState == WindowSizeState.narrow)                            
                mainPage.MoveZoomButtonToPrimary();            

            mainPage.EnableChangingReadMode(this);
            ChangeBackgroundColor();

            CoreWindow.GetForCurrentThread().KeyUp += SearchPageKeyUpHandler;
        }

        private void UnHookAllEvents()
        {
            mainPage.UnhookZooming();            
            if (mainPage.WindowSizeState == WindowSizeState.narrow)                            
                mainPage.MoveZoomButtonToSecondary();
            
            mainPage.IsAutoSwitchZoomButtonToSecondary = true;            

            mainPage.DisableChangingReadMode();
            CoreWindow.GetForCurrentThread().KeyUp -= SearchPageKeyUpHandler;
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            UnHookAllEvents();
            if (cardViewPopup != null)
                cardViewPopup.Close();
            base.OnNavigatingFrom(e);
        }

        private void DeckNameViewSelectionChangedHandler(object sender, SelectionChangedEventArgs e)
        {            
            var deck = (sender as ComboBox).SelectedItem as DeckInformation;
            if (deck.Id == DeckNameViewModel.ALL_DECKS_ID)
                searchDeck = "";
            else
                searchDeck =  "\"deck:" + deck.Name + "\" ";
            SearchStringChange(null, null);
        }

        private void FilterExpandToggleButtonClickHandler(object sender, RoutedEventArgs e)
        {
            if (deckNameView.Visibility == Visibility.Visible)
            {                
                expandSymbolRotation.Rotation = 0;
                deckNameView.Visibility = Visibility.Collapsed;
                tagInformationView.Visibility = Visibility.Collapsed;
                cardStateRoot.Visibility = Visibility.Collapsed;
            }
            else
            {
                expandSymbolRotation.Rotation = 180;
                deckNameView.Visibility = Visibility.Visible;
                tagInformationView.Visibility = Visibility.Visible;
                cardStateRoot.Visibility = Visibility.Visible;
            }
        }

        private void CheckBoxCheckedHandler(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            ChangeCardStateString(checkBox, true);
        }

        private void ChangeCardStateString(CheckBox checkBox, bool state)
        {
            switch (checkBox.Content.ToString().ToLower())
            {
                case "due":
                    cardState[IS_DUE] = state;
                    break;
                case "new":
                    cardState[IS_NEW] = state;
                    break;
                case "learn":
                    cardState[IS_LEARN] = state;
                    break;
                case "review":
                    cardState[IS_REVIEW] = state;
                    break;
                case "suspended":
                    cardState[IS_SUSPEND] = state;
                    break;
            }
            StringBuilder builder = new StringBuilder();
            foreach(var key in cardState)
            {
                if (key.Value)
                {
                    builder.Append(key.Key);
                    builder.Append(" ");
                }
            }
            searchCardState = builder.ToString();
            SearchStringChange(checkBox, null);
        }

        private void CheckBoxUncheckedHandler(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            ChangeCardStateString(checkBox, false);
        }

        private async void SearchPageKeyUpHandler(CoreWindow sender, KeyEventArgs args)
        {
            await mainPage.CurrentDispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if(args.VirtualKey == Windows.System.VirtualKey.Enter)     
                    if(!isSuppressEnterKey)           
                        UpdateSeachResultsAsync();
                if (args.VirtualKey == Windows.System.VirtualKey.Left)
                    PreviousPageButtonClickHandler(null, null);
                if (args.VirtualKey == Windows.System.VirtualKey.Right)
                    NextButtonClickHandler(null, null);
            });            
        }

        private void UpdateSeachResultsAsync()
        {
            ShowProgressRing();
            string text = searchTextBox.Text;
            Task.Run(() =>
            {
                var cardIdlist = collection.FindCards(text, false);
                numberOfSearchedCards = cardIdlist.Count;
                ArrangeResultsIntoPages(cardIdlist);
                HideProgressRingAsync();
            });
        }

        private void ArrangeResultsIntoPages(List<long> cardIdlist)
        {
            DivideIntoPages(cardIdlist);            
            ArrangeResults();
        }

        private void ArrangeResults()
        {
            currentPage = 0;
            cardInforViewModel = new CardInformationViewModel(collection, searchedCardId[currentPage]);

            var task = mainPage.CurrentDispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                SetupPageView();
                SortCardInforViewModel(currentSortColumn.Key, currentSortColumn.Value);
                cardInformationView.DataContext = cardInforViewModel.Cards;
            });
        }

        private void DivideIntoPages(List<long> cardIdlist)
        {
            searchedCardId.Clear();
            if (numberOfCardsAPage == 0)
            {
                searchedCardId.Add(0, cardIdlist);
                return;
            }

            var numberOfPages = cardIdlist.Count / numberOfCardsAPage;

            if (numberOfPages == 0)
                searchedCardId.Add(0, cardIdlist);
            else
            {
                int i = 0;
                for (; i < numberOfPages; i++)
                {
                    var range = cardIdlist.GetRange(i * numberOfCardsAPage, numberOfCardsAPage);
                    searchedCardId.Add(i, range);
                }

                if ((cardIdlist.Count % numberOfCardsAPage) != 0)
                {
                    var range = cardIdlist.GetRange(i * numberOfCardsAPage, cardIdlist.Count - i * numberOfCardsAPage);
                    searchedCardId.Add(i, range);
                }
            }
        }

        private void SetupPageView()
        {
            pageButtonRoot.Visibility = Visibility.Visible;
            UpdatePageTextBlock();
            if (searchedCardId.Count > 1)
            {
                cardInforViewModel.GetNextCards(searchedCardId[1]);                                
                previousButton.Visibility = Visibility.Collapsed;
                nextButton.Visibility = Visibility.Visible;                
            }
            else
            {
                cardInforViewModel.NextCards.Clear();
                cardInforViewModel.PreviousCards.Clear();
                previousButton.Visibility = Visibility.Collapsed;
                nextButton.Visibility = Visibility.Collapsed;
            }
        }

        private void PreviousPageButtonClickHandler(object sender, RoutedEventArgs e)
        {
            if (currentPage == 0)
                return;

            nextButton.Visibility = Visibility.Visible;
            cardInforViewModel.MoveToPreviousPage();
            currentPage--;
            if (currentPage != 0)
                cardInforViewModel.GetPreviousCards(searchedCardId[currentPage-1]);
            else
                previousButton.Visibility = Visibility.Collapsed;
            UpdatePageTextBlock();
        }

        private void NextButtonClickHandler(object sender, RoutedEventArgs e)
        {
            if (currentPage >= searchedCardId.Count - 1)
                return;

            previousButton.Visibility = Visibility.Visible;
            cardInforViewModel.MoveToNextPage();
            currentPage++;
            if (currentPage < searchedCardId.Count - 1)
                cardInforViewModel.GetNextCards(searchedCardId[currentPage+1]);
            else                            
                nextButton.Visibility = Visibility.Collapsed;
            
            UpdatePageTextBlock();
        }

        private void UpdatePageTextBlock()
        {
            pageTextBlock.Text = (currentPage + 1) + "/" + searchedCardId.Count + " (" + numberOfSearchedCards + ")";
        }

        private void TenCardPageButtonClickHandler(object sender, RoutedEventArgs e)
        {
            numberOfCardsAPage = 10;
            ChangeNumberOfCardsAPageHandler();
        }

        private void TwentyCardPageButtonClickHandler(object sender, RoutedEventArgs e)
        {
            numberOfCardsAPage = 20;
            ChangeNumberOfCardsAPageHandler();
        }

        private void FortyCardPageButtonClickHandler(object sender, RoutedEventArgs e)
        {
            numberOfCardsAPage = 40;
            ChangeNumberOfCardsAPageHandler();
        }

        private void EightyPageButtonClickHandler(object sender, RoutedEventArgs e)
        {
            numberOfCardsAPage = 80;
            ChangeNumberOfCardsAPageHandler();
        }

        private void ChangeNumberOfCardsAPageHandler()
        {
            pageFlyout.Hide();
            ReArrangResultsIntoPages();
        }

        private async void ShowAllResultsButtonClickHandler(object sender, RoutedEventArgs e)
        {
            if(UIHelper.IsMobileDevice())
            {
                if (numberOfSearchedCards > 160)
                {
                    await UIHelper.ShowMessageDialog("Too many cards to display on mobile devices.");
                    return;
                }
            }
            pageFlyout.Hide();

            ShowProgressRing();
            var task = Task.Run(() =>
            {                
                numberOfCardsAPage = 0;
                var cardIdList = new List<long>();
                foreach (var list in searchedCardId.Values)
                    cardIdList.AddRange(list);
                searchedCardId.Clear();
                searchedCardId.Add(0, cardIdList);
                ArrangeResults();
                HideProgressRingAsync();
            });
        }

        private void ReArrangResultsIntoPages()
        {
            ShowProgressRing();
            Task.Run(() =>
            {
                var cardIdList = new List<long>();
                foreach (var list in searchedCardId.Values)
                    cardIdList.AddRange(list);
                ArrangeResultsIntoPages(cardIdList);

                HideProgressRingAsync();
            });
        }

        private void HideProgressRingAsync()
        {
            var task = mainPage.CurrentDispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                progressRing.Visibility = Visibility.Collapsed;
                progressRing.IsActive = false;
            });
        }

        private void ShowProgressRing()
        {
            progressRing.Visibility = Visibility;
            progressRing.IsActive = true;
        }

        private void SearchButtonClickHandler(object sender, RoutedEventArgs e)
        {
            UpdateSeachResultsAsync();
        }

        private void EditMenuFlyoutItemClickHandler(object sender, RoutedEventArgs e)
        {
            if (cardInformationView.CardShowMenuFlyout == null)
                return;

            collection.Deck.Select(cardInformationView.CardShowMenuFlyout.DeckId, false);
            Note note = collection.GetNote(cardInformationView.CardShowMenuFlyout.NoteId);
            NoteEditorPageParameter param = new NoteEditorPageParameter() { CurrentNote = note, Mainpage = mainPage };
            Frame.Navigate(typeof(NoteEditor), param);
        }

        private void SuspendMenuFlyoutItemClickHandler(object sender, RoutedEventArgs e)
        {
            var listCard = GetSelectedCardsInListView();
            if (listCard.Key.Count == 0)
                return;

            collection.Sched.SuspendCards(listCard.Value.ToArray());
            foreach(var card in listCard.Key)            
                SuspendCardInfor(card);
            UnselectAllMenuFlyoutItemClick(null, null);
            collection.SaveAndCommitAsync();
        }

        private void SuspendCardInfor(CardInformation card)
        {
            if (card.Queue > -1)
                card.DueStr = "(" + card.DueStr + ")";
            card.Queue = -1;
        }

        private void UnsuspendMenuFlyoutItemClickHandler(object sender, RoutedEventArgs e)
        {
            var listCard = GetSelectedCardsInListView();
            if (listCard.Key.Count == 0)
                return;

            collection.Sched.UnsuspendCards(listCard.Value.ToArray());
            foreach (var card in listCard.Key)
                UnsuspendCardInfor(card);
            UnselectAllMenuFlyoutItemClick(null, null);
            collection.SaveAndCommitAsync();
        }        

        private void UnsuspendCardInfor(CardInformation card)
        {
            if (card.Queue < 0)
                card.DueStr = card.DueStr.Substring(1, card.DueStr.Length - 2);
            card.Queue = (int)card.Type;
        }

        private async void ResetMenuFlyoutItemClickHandler(object sender, RoutedEventArgs e)
        {
            var listCard = GetSelectedCardsInListView();
            if (listCard.Key.Count == 0)
                return;

            bool isContinue = await UIHelper.AskUserConfirmation(
                "Reset " + listCard.Key.Count + " card(s) and put them back to new cards queue. Continue?");
            if (!isContinue)
                return;

            collection.Sched.ResetCards(listCard.Value.ToArray());
            cardInforViewModel.UpdateCardInformationDueAfterReset(collection, listCard);
            UnselectAllMenuFlyoutItemClick(null, null);
            collection.SaveAndCommitAsync();
        }

        private async void DeleteMenuFlyoutItemClick(object sender, RoutedEventArgs e)
        {
            var listCard = GetSelectedCardsInListView();
            if (listCard.Key.Count == 0)
                return;

            bool isContinue = await UIHelper.AskUserConfirmation(
                                        "Delete " + listCard.Key.Count + " card(s)?.");
            if (!isContinue)
                return;

            collection.RemoveCardsAndNoteIfNoCardsLeft(listCard.Value.ToArray());
            collection.SaveAndCommitAsync();
            UpdateSeachResultsAsync();            
        }

        private KeyValuePair<List<CardInformation>, List<long>> GetSelectedCardsInListView()
        {
            var items = cardInformationView.CardListView.SelectedItems;
            var cardIdList = new List<long>();
            var cardInforList = new List<CardInformation>();
            if (items.Count != 0)
            {                
                foreach (var item in items)
                {
                    var card = item as CardInformation;                    
                    cardIdList.Add(card.Id);
                    cardInforList.Add(card);
                }                
            }

            if (cardInformationView.CardShowMenuFlyout != null)
            {
                var cardIdWithMenu = cardInformationView.CardShowMenuFlyout.Id;
                //User opened context menu on unselected card -> ignored all seletected cards
                //to limit action range and its consequences if this is a mistake from user
                if (!cardIdList.Contains(cardIdWithMenu))
                {
                    if (cardIdList.Count != 0)
                    {
                        cardIdList.Clear();
                        cardInforList.Clear();
                        var task = mainPage.CurrentDispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                        {
                            await UIHelper.ShowMessageDialog("Menu open on card outside selected cards.\n" +
                                                             "Please choose the action again on selected cards or unselect them first.");
                        });
                    }
                    else
                    {
                        cardIdList.Add(cardIdWithMenu);
                        cardInforList.Add(cardInformationView.CardShowMenuFlyout);
                    }
                }
            }
            
            return new KeyValuePair<List<CardInformation>, List<long>>(cardInforList, cardIdList);
        }

        private void SelectAllMenuFlyoutItemClick(object sender, RoutedEventArgs e)
        {
            cardInformationView.CardListView.SelectAll();
        }

        private void UnselectAllMenuFlyoutItemClick(object sender, RoutedEventArgs e)
        {            
            var item = cardInformationView.CardListView.SelectedItem;
            if (item == null)
                return;

            cardInformationView.CardListView.SelectedItem = item;
            ListViewItem listItem = cardInformationView.CardListView.ContainerFromItem(item) as ListViewItem;
            listItem.IsSelected = false;            
        }

        private async void RescheduleMenuFlyoutItemClickHandler(object sender, RoutedEventArgs e)
        {
            listRescheduleCard = GetSelectedCardsInListView();
            if (listRescheduleCard.Key.Count == 0)
                return;

            var isValid = cardInforViewModel.CheckIfAllIsReviewCards(listRescheduleCard.Key);
            if(!isValid)
            {
                await UIHelper.ShowMessageDialog("You can only reschedule review cards. Please uncheck invalid cards and try again.");
                return;
            }

            if (rescheduleFlyout == null)
            {
                rescheduleFlyout = new RescheduleFlyout();
                rescheduleFlyout.OKButtonClickEvent += RescheduleFlyoutOKButtonClickEventHandler;
                rescheduleFlyout.ClosedEvent += RescheduleFlyoutClosedEventHandler;
                
            }
            isSuppressEnterKey = true;
            rescheduleFlyout.Show(cardInformationView.PointToShowFlyout);
        }

        private void RescheduleFlyoutClosedEventHandler(object sender, RoutedEventArgs e)
        {
            isSuppressEnterKey = false;
        }

        private void RescheduleFlyoutOKButtonClickEventHandler(object sender, RoutedEventArgs e)
        {
            var newDue = rescheduleFlyout.Number;
            if (newDue == 0)
                return;
            collection.Sched.RescheduleIntoReviewCards(listRescheduleCard.Value.ToArray(), newDue, newDue);
            cardInforViewModel.UpdateCardDueAfterReschedule(collection, listRescheduleCard);
            listRescheduleCard.Key.Clear();
            listRescheduleCard.Value.Clear();
            UnselectAllMenuFlyoutItemClick(null, null);
            collection.SaveAndCommitAsync();
        }

        private void ViewCardMenuFlyoutClickHandler(object sender, RoutedEventArgs e)
        {
            if (cardInformationView.CardShowMenuFlyout == null)
                return;

            if (cardViewPopup == null)
                InitCardViewPopup();
            else
            {
                cardViewPopup.ChangeCard(cardInformationView.CardShowMenuFlyout.Id);
                cardViewPopup.Show();
            }            
        }

        private void InitCardViewPopup()
        {            
            cardViewPopup = new CardViewPopup(collection, cardInformationView.CardShowMenuFlyout.Id);
            UIHelper.AddToGridInFull(mainGrid, cardViewPopup);            
            if (isNightMode)
                cardViewPopup.ToggleReadMode();
            cardViewPopup.Show();
        }
    }
}
