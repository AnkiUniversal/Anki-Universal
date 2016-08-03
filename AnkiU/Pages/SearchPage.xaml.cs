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
using AnkiU.Views;
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
        private int numberOfCardsAPage;        

        private bool isNightMode = false;

        private MainPage mainPage;
        private Collection collection;
        private long currentDeckId;

        private CardInformationViewModel cardInforViewModel = null;
        private Dictionary<int, List<long>> searchedCardId = new Dictionary<int, List<long>>();        
        private int currentPage = 0;
        private long numberOfSearchedCards;

        private bool isSuppressEnterKey = false;
        private RescheduleFlyout rescheduleFlyout = null;
        private KeyValuePair<List<CardInformation>, List<long>> listRescheduleCard;

        private KeyValuePair<SearchSortColumn, bool> currentSortColumn;

        private CardViewPopup cardViewPopup;

        private AdvancedSearchPopup advancedSearch;

        private long editNoteId;

        public SearchPage()
        {
            this.InitializeComponent();                        
        }

        public void ToggleReadMode()
        {            
            ChangeReadMode(!isNightMode);
        }

        private void ChangeReadMode(bool isNightMode)
        {
            this.isNightMode = isNightMode;
            ChangeBackgroundColor();
            if (cardViewPopup != null)
                cardViewPopup.ChangeReadMode(isNightMode);
            advancedSearch.ChangeReadMode(isNightMode);
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
            SetupAdvanceSearch();
            HookAllEvents();
        }

        private void SetupAdvanceSearch()
        {
            advancedSearch = new AdvancedSearchPopup(collection, -5, 35);
            advancedSearch.VerticalAlignment =  VerticalAlignment.Stretch;
            advancedSearch.HorizontalAlignment = HorizontalAlignment.Stretch;
            Grid.SetRow(advancedSearch, 1);            
            mainGrid.Children.Add(advancedSearch);
            
            advancedSearch.SearchClick += AdvancedSearchOkHandler;
            advancedSearch.CloseClick += AdvancedSearchCloseClick;
            advancedSearch.ShowCommandCheckClick += AdvancedSearchShowCommandClick;

            advancedSearch.InitDeckSelected(currentDeckId);
            searchTextBox.Text = "\"deck:" + collection.Deck.GetDeckName(currentDeckId) + "\"";
        }

        /// <summary>
        /// This function should be used to provide immediate feedback to user
        /// when check/uncheck ShowCommandCheckBox
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void AdvancedSearchShowCommandClick(object sender, RoutedEventArgs e)
        {
            if(advancedSearch.IsShowCommands == false)
                searchTextBox.Text = "";
            else
                searchTextBox.Text = advancedSearch.GetSearchString();
        }

        private void AdvancedSearchOkHandler(object sender, RoutedEventArgs e)
        {
            //Re-check again to avoid any errors
            if(advancedSearch.IsShowCommands)
                searchTextBox.Text = advancedSearch.GetSearchString();

            UpdateSeachResultsAsync();
        }

        private void AdvancedSearchCloseClick(object sender, RoutedEventArgs e)
        {
            if (advancedSearch.IsShowCommands)
                searchTextBox.Text = advancedSearch.GetSearchString();
        }

        private void SetupCardListView()
        {            
            currentSortColumn = new KeyValuePair<SearchSortColumn, bool>(cardInformationView.CurrentSortColumn, false);
            cardInformationView.SortColumnChangedEvent += CardInformationViewSorttColumnChangedHandler;
            cardInformationView.CardListViewMenuFlyout = Resources["CardListViewContextMenu"] as MenuFlyout;
            cardInformationView.CardListViewMenuFlyout.Closed += (s, args) => { cardInformationView.CardShowMenuFlyout = null; };

            pageButtonRoot.Visibility = Visibility.Collapsed;
        }

        private void SetupDefaultCardsAPage()
        {
            if (UIHelper.GetDeviceFamily() == "Windows.Mobile")
                numberOfCardsAPage = 10;
            else
                numberOfCardsAPage = 20;
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

        private void HookAllEvents()
        {
            mainPage.HookZooming(cardInformationView);
            mainPage.IsAutoSwitchZoomButtonToSecondary = false;
            mainPage.ZoomButtonsSeparator.Visibility = Visibility.Collapsed;
            if (mainPage.WindowSizeState == WindowSizeState.narrow)                            
                mainPage.MoveZoomButtonToPrimary();            

            mainPage.EnableChangingReadMode(this);
            ChangeBackgroundColor();

            mainPage.CommanBar.Opening += CommanBarOpening;
            CoreWindow.GetForCurrentThread().KeyUp -= SearchPageKeyUpHandler;
            CoreWindow.GetForCurrentThread().KeyUp += SearchPageKeyUpHandler;
        }

        private void CommanBarOpening(object sender, object e)
        {
            if (cardViewPopup != null)
                cardViewPopup.Hide();
        }

        private void UnHookAllEvents()
        {
            noteEditorControl.ClosedEvent -= NoteEditorClosed;

            mainPage.UnhookZooming();            
            if (mainPage.WindowSizeState == WindowSizeState.narrow)                            
                mainPage.MoveZoomButtonToSecondary();
            
            mainPage.IsAutoSwitchZoomButtonToSecondary = true;            
            mainPage.DisableChangingReadMode();

            mainPage.CommanBar.Opening -= CommanBarOpening;
            CoreWindow.GetForCurrentThread().KeyUp -= SearchPageKeyUpHandler;
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            UnHookAllEvents();

            if (cardViewPopup != null)
                cardViewPopup.Close();
            if (noteEditorControl.IsNavigated)
                noteEditorControl.Close();

            base.OnNavigatingFrom(e);
        }

        private void FilterExpandToggleButtonClickHandler(object sender, RoutedEventArgs e)
        {            
            advancedSearch.Toggle(searchTextBox.ActualWidth);
        }

        private async void SearchPageKeyUpHandler(CoreWindow sender, KeyEventArgs args)
        {
            await mainPage.CurrentDispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (args.VirtualKey == Windows.System.VirtualKey.Enter)
                    if (!isSuppressEnterKey)
                    {
                        EnterKeyHandler();
                    }
                if (advancedSearch.IsOpen == false)
                {
                    if (args.VirtualKey == Windows.System.VirtualKey.Left)
                        PreviousPageButtonClickHandler(null, null);
                    if (args.VirtualKey == Windows.System.VirtualKey.Right)
                        NextButtonClickHandler(null, null);
                }
            });            
        }

        private void EnterKeyHandler()
        {
            if (advancedSearch.IsOpen && advancedSearch.IsShowCommands)
            {
                searchTextBox.Text = advancedSearch.GetSearchString();
                advancedSearch.Hide();
            }
            UpdateSeachResultsAsync();
        }

        private void UpdateSeachResultsAsync()
        {
            ShowProgressRing();
            string text = GetSearchString();
            Task.Run(() =>
            {
                var cardIdlist = collection.FindCards(text, false);
                numberOfSearchedCards = cardIdlist.Count;
                ArrangeResultsIntoPages(cardIdlist);
                HideProgressRingAsync();
            });
        }

        private string GetSearchString()
        {
            if(advancedSearch.IsShowCommands)
                return searchTextBox.Text;
            else
                return advancedSearch.GetSearchString() + " " + searchTextBox.Text;
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
            if (!noteEditorControl.IsNavigated)
                InitEditNotePopup(param);
        }

        private void InitEditNotePopup(NoteEditorPageParameter param)
        {
            UnHookAllEvents();
            noteEditorControl.NavigateToNoteEditor(param);
            noteEditorControl.ClosedEvent += NoteEditorClosed;
            noteEditorControl.Visibility = Visibility.Visible;
        }

        private void NoteEditorClosed(object sender, RoutedEventArgs e)
        {            
            //Edited note ID can be different with the initial note we show the UI
            //so always get the current edit note 
            editNoteId = noteEditorControl.EditNoteId;            
            cardInforViewModel.UpdateCardContentWithSameNoteId(editNoteId);
            HookAllEvents();
            ChangeReadMode(MainPage.UserPrefs.IsReadNightMode);
            noteEditorControl.Visibility = Visibility.Collapsed;
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
            cardInformationView.CardListView.SelectedItems.Clear();
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
            cardViewPopup.ChangeReadMode(isNightMode);
            cardViewPopup.Show();
        }

        private void SearchTextBoxGotFocus(object sender, RoutedEventArgs e)
        {
            CoreWindow.GetForCurrentThread().KeyUp -= SearchPageKeyUpHandler;
        }

        private void SearchTextBoxLostFocus(object sender, RoutedEventArgs e)
        {
            //Always do this to make sure we won't accidentailly hook this event twice
            CoreWindow.GetForCurrentThread().KeyUp -= SearchPageKeyUpHandler;
            CoreWindow.GetForCurrentThread().KeyUp += SearchPageKeyUpHandler;
        }

        private async void SearchTextBoxKeyUp(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                await mainPage.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    EnterKeyHandler();
                });
            }
        }
    }
}
