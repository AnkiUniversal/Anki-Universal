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
using AnkiU.Pages;
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
using Windows.Data.Json;
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

namespace AnkiU.UserControls
{
    public sealed partial class CustomStudyFlyout : UserControl
    {
        private const int MAX_NUMBER = 2000;
        private const string DEFAULT_DYN_DECKNAME = "Custom Study Session";        
        private const string FORGOTTEN_SEARCH = "(deck:\"{0}\" rated:{1}:1) -is:suspended -is:buried -deck:filtered";

        private Collection collection;
        private long totalNewCards;
        private long totalDueCards;
        private long currentDeckId;

        private TagInformationViewModel includeTagsViewModel;
        private TagInformationViewModel excludeTagsViewModel;

        private double VERTICAL_OFFSET = -30;
        private double DEFAULT_HEIGHT_MARGIN = 40;
        private double DEFAULT_WIDTH_MARGIN = 15;
        private double DEFAULT_SCROLLVIEWER_MARGIN = 140;

        public enum CustomStudyOption
        {
            IncreaseNewToDay,
            IncreaseReviewToDay,
            ReviewForgotten,
            ReviewAhead,
            PreviewNew,
            CramMode
        }

        private CustomStudyOption studyOption;
        private CoreDispatcher dispatcher;        

        public delegate void CustomStudyCreateHandler(CustomStudyOption studyOption, long deckID);
        public event CustomStudyCreateHandler CustomStudyCreateEvent;

        public bool IsOpen { get { return customStudyFlyout.IsOpen; } }

        public CustomStudyFlyout(CoreDispatcher dispatcher)
        {
            this.InitializeComponent();
            this.dispatcher = dispatcher;

            InitFlyout();
        }

        private void InitFlyout()
        {
            includeTags.LabelVisibility = Visibility.Collapsed;
            excludeTags.LabelVisibility = Visibility.Collapsed;
            ResetSelection();
            numberBox.NumberChanged += NumberChangedHandler;
            numberBox.KeyUp += NumberBoxKeyDown;
            cramNumberBox.KeyUp += NumberBoxKeyDown;
        }

        private async void NumberBoxKeyDown(object sender, KeyRoutedEventArgs e)
        {
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                if (e.Key == Windows.System.VirtualKey.Enter)
                {
                    await CreateCustomStudy();
                }
            });
        }

        private void ResetSelection()
        {
            numberBox.Number = numberBox.MinNumber;
            cramNumberBox.Number = cramNumberBox.MinNumber;
            UnCheckAll();
            studyOption = CustomStudyOption.IncreaseNewToDay;
            increaseNewRadioButton.IsChecked = true;            
        }

        /// <summary>
        /// Win phone sometime bugs out and won't uncheck if we set 
        /// checked state in code, leading to two radio button in Checked state.
        /// So we have to make sure all are in unchecked state manually
        /// </summary>
        private void UnCheckAll()
        {
            increaseNewRadioButton.IsChecked = false;
            increaseReviewRadioButton.IsChecked = false;
            forgottenRadioButton.IsChecked = false;
            reviewAheadRadioButton.IsChecked = false;
            previewNewRadioButton.IsChecked = false;
            allCardsRadioButton.IsChecked = false;
        }

        public void InitDeckValue(Collection collection, bool isNightMode)
        {
            this.collection = collection;
            currentDeckId = collection.Deck.Selected();
            totalNewCards = collection.Sched.TotalNewCountForCurrentDecks();
            totalDueCards = collection.Sched.TotalReviewForCurrentDecks();

            ResetSelection();
            ChangeReadMode(isNightMode);

            CalculateSizeAndPosition();
        }

        public void ChangeReadMode(bool isNightMode)
        {
            if (isNightMode)
                ChangeToNightMode();
            else
                ChangeToDayMode();
        }

        private void CalculateSizeAndPosition()
        {
            var winWidth = CoreWindow.GetForCurrentThread().Bounds.Width;
            var winHeight = CoreWindow.GetForCurrentThread().Bounds.Height;
            var maxWidth = winWidth - DEFAULT_WIDTH_MARGIN;
            var maxHeight = winHeight - DEFAULT_HEIGHT_MARGIN;
            customStudyFlyout.MaxWidth = maxWidth;
            scrollViewer.MaxWidth = MaxWidth;
            customStudyFlyout.MaxHeight = maxHeight;
            scrollViewer.MaxHeight = maxHeight - DEFAULT_SCROLLVIEWER_MARGIN;

            customStudyFlyout.VerticalOffset = VERTICAL_OFFSET;
        }

        private void NumberChangedHandler(object sender, TextChangedEventArgs e)
        {
            if (studyOption == CustomStudyOption.ReviewForgotten)
            {
                chosenLabelValue.Text = GetTotalForgottenCards(numberBox.Number).ToString();
            }
            else if (studyOption == CustomStudyOption.ReviewAhead)
            {
                chosenLabelValue.Text = GetReviewCardsForecast(numberBox.Number).ToString();
            }
            else if (studyOption == CustomStudyOption.PreviewNew)
            {
                chosenLabelValue.Text = GetNewCardsAdded(numberBox.Number).ToString();
            }
        }

        public void ChangeToDayMode()
        {
            userControl.Background = new SolidColorBrush(Windows.UI.Colors.White);
            userControl.Foreground = new SolidColorBrush(Windows.UI.Colors.Black);
        }

        public void ChangeToNightMode()
        {
            userControl.Background = new SolidColorBrush(Windows.UI.Colors.Black);
            userControl.Foreground = new SolidColorBrush(Windows.UI.Colors.White);
        }

        public void Show()
        {                   
            customStudyFlyout.IsOpen = true;
        }

        private void CancleButtonClickHandler(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        public void Hide()
        {
            customStudyFlyout.IsOpen = false;
        }

        private void IncreaseTodayNewCheckedHandler(object sender, RoutedEventArgs e)
        {
            chosenLabel.Text = "New cards:";
            chosenLabelValue.Text = totalNewCards.ToString();
            numberBoxFirstLabel.Text = "Increase by:";
            numberBox.MinNumber = 1;
            SetMaxNumberBoxValue(totalNewCards);
            numberBoxSecondLabel.Text = "card(s)";
            studyOption = CustomStudyOption.IncreaseNewToDay;
            numberBox.Number = 1;
        }

        private void SetMaxNumberBoxValue(long totalCards)
        {
            if (totalCards <= MAX_NUMBER)
                numberBox.MaxNumber = (int)totalCards;
            else
                numberBox.MaxNumber = MAX_NUMBER;
        }

        private void IncreaseTodayReviewCheckedHandler(object sender, RoutedEventArgs e)
        {
            chosenLabel.Text = "Remain today's review cards:";
            chosenLabelValue.Text = totalDueCards.ToString();
            numberBoxFirstLabel.Text = "Increase by:";
            numberBox.MinNumber = 1;
            SetMaxNumberBoxValue(totalDueCards);
            numberBoxSecondLabel.Text = "card(s)";
            studyOption = CustomStudyOption.IncreaseReviewToDay;
            numberBox.Number = 1;
        }

        private void ReviewForgottenCardsChecked(object sender, RoutedEventArgs e)
        {
            chosenLabel.Text = "Forgotten cards:";
            chosenLabelValue.Text = GetTotalForgottenCards(1).ToString();
            numberBoxFirstLabel.Text = "During the last:";
            numberBox.MinNumber = 1;
            SetMaxNumberBoxValue(MAX_NUMBER);
            numberBoxSecondLabel.Text = "day(s)";
            studyOption = CustomStudyOption.ReviewForgotten;
            numberBox.Number = 1;
        }

        private long GetTotalForgottenCards(long end)
        {            
            string search = String.Format(FORGOTTEN_SEARCH, collection.Deck.GetDeckName(currentDeckId), end);
            return collection.FindCards(search).Count;
        }

        private void ReviewAheadCheckedHandler(object sender, RoutedEventArgs e)
        {
            chosenLabel.Text = "Review cards:";
            chosenLabelValue.Text = GetReviewCardsForecast(1).ToString();
            numberBoxFirstLabel.Text = "During the next:";
            numberBox.MinNumber = 1;
            SetMaxNumberBoxValue(MAX_NUMBER);
            numberBoxSecondLabel.Text = "day(s)";
            studyOption = CustomStudyOption.ReviewAhead;
            numberBox.Number = 1;
        }

        private long GetReviewCardsForecast(long end)
        {
            string lim = " and day < " + (end + 1);
            string deckIdStr = GetParentAndChildrenDeckIdsStr();
            string query = String.Format("select count(), (due - {0}) as day " +
                                         "from cards where did in {1} and queue in (2,3) {2}",
                                         collection.Sched.Today, deckIdStr, lim);

            return collection.Database.QueryScalar<long>(query);
        }

        private string GetParentAndChildrenDeckIdsStr()
        {
            var deckIdList = collection.Deck.Children(currentDeckId).Values.ToList();
            deckIdList.Add(currentDeckId);
            var deckIdStr = Utils.Ids2str(deckIdList);
            return deckIdStr;
        }

        private void PreviewCardsCheckedhandler(object sender, RoutedEventArgs e)
        {
            chosenLabel.Text = "New cards:";
            chosenLabelValue.Text = GetNewCardsAdded(1).ToString();
            numberBoxFirstLabel.Text = "Added in the last:";
            numberBox.MinNumber = 1;
            SetMaxNumberBoxValue(MAX_NUMBER);
            numberBoxSecondLabel.Text = "day(s)";
            studyOption = CustomStudyOption.PreviewNew;
            numberBox.Number = 1;
        }

        private long GetNewCardsAdded(int days)
        {
            long cutoff = (collection.Sched.DayCutoff - 86400 * days) * 1000;
            var deckidstr = GetParentAndChildrenDeckIdsStr();
            string query = String.Format("select count() from cards where did in {0} and (type = 0 and queue = 0 and id > {1}) ",
                                          deckidstr, cutoff);
            return collection.Database.QueryScalar<long>(query);
        }

        private void CramModeCheckedHandler(object sender, RoutedEventArgs e)
        {
            cramNumberBox.Number = 1;
            cramNumberBox.MinNumber = 1;
            var deckIds = collection.Deck.Children(currentDeckId).Values.ToList();            
            deckIds.Add(currentDeckId);
            cramNumberBox.MaxNumber = collection.CardCount(deckIds);
            standardOptionsRoot.Visibility = Visibility.Collapsed;
            cramModeRoot.Visibility = Visibility.Visible;
            if (includeTagsViewModel == null)
            {
                includeTagsViewModel = new TagInformationViewModel(collection, collection.NewNote());
                excludeTagsViewModel = new TagInformationViewModel(collection, collection.NewNote());
                includeTags.ViewModel = includeTagsViewModel;
                excludeTags.ViewModel = excludeTagsViewModel;
            }
            studyOption = CustomStudyOption.CramMode;
        }

        private void CramModeUnCheckedHandler(object sender, RoutedEventArgs e)
        {
            standardOptionsRoot.Visibility = Visibility.Visible;
            cramModeRoot.Visibility = Visibility.Collapsed;
        }       

        private async void OkButtonClick(object sender, RoutedEventArgs e)
        {
            await CreateCustomStudy();
        }

        private async Task CreateCustomStudy()
        {
            var deck = collection.Deck.Current();
            if (HandleNotCreatingDynamicDeckCases(deck))
                return;

            //Not in java and python ver, we do nothing if no cards are available
            if (studyOption != CustomStudyOption.CramMode)
            {
                var max = int.Parse(chosenLabelValue.Text);
                if (max == 0)
                    return;
            }

            customStudyFlyout.IsOpen = false;
            JsonObject dynamicDeck;
            long deckId;
            var currentCustomDeck = collection.Deck.GetDeckByName(DEFAULT_DYN_DECKNAME);
            if (currentCustomDeck != null)
            {
                deckId = (long)currentCustomDeck.GetNamedNumber("id");
                bool isDyn = collection.Deck.IsDyn(deckId);
                if (!isDyn)
                {
                    await UIHelper.ShowMessageDialog("Please rename the deck named \"" + DEFAULT_DYN_DECKNAME + "\" to another name first.");
                    return;
                }
                else
                {
                    collection.Sched.EmptyDyn(deckId);
                    dynamicDeck = currentCustomDeck;
                    collection.Deck.Select(deckId);
                }
            }
            else
            {
                deckId = collection.Deck.NewDynamicDeck(DEFAULT_DYN_DECKNAME);
                dynamicDeck = collection.Deck.Get(deckId);
            }
            ProgressDialog dialog = new ProgressDialog();
            dialog.ProgressBarLabel = "Buidling custom study deck...";
            dialog.ShowInDeterminateStateNoStopAsync("Custom Study");

            CreateDynamicDeckConfigs(dynamicDeck, deck);

            var task = Task.Run(async () =>
            {
                var cards = collection.Sched.RebuildDyn();
                collection.SaveAndCommitAsync();

                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {

                    if (cards == null || cards.Count == 0)
                        await UIHelper.ShowMessageDialog(UIConst.WARN_CUSTOM_STUDY_NOCARDS_MATCH);

                    CustomStudyCreateEvent?.Invoke(studyOption, deckId);
                    dialog.Hide();
                });
            });
        }

        private bool HandleNotCreatingDynamicDeckCases(JsonObject deck)
        {
            switch (studyOption)
            {
                case CustomStudyOption.IncreaseNewToDay:
                    extendNewLimit(deck);
                    customStudyFlyout.IsOpen = false;
                    return true;
                case CustomStudyOption.IncreaseReviewToDay:
                    extendReview(deck);
                    customStudyFlyout.IsOpen = false;
                    return true;
                default:
                    return false;
            }
        }

        private void CreateDynamicDeckConfigs(JsonObject dynamicDeck, JsonObject deck)
        {            
            switch (studyOption)
            {
                case CustomStudyOption.ReviewForgotten:
                    dynamicDeck["delays"] = CreateNumberArray(1);
                    dynamicDeck.GetNamedArray("terms")[0] = CreateTermArray(
                                                            "rated:" + numberBox.Number + ":1",
                                                            MAX_NUMBER,
                                                            (int)DynamicDeckOrder.RANDOM);
                    dynamicDeck["resched"] = JsonValue.CreateBooleanValue(false);
                    break;

                case CustomStudyOption.ReviewAhead:
                    dynamicDeck["delays"] = JsonValue.CreateNullValue();
                    dynamicDeck.GetNamedArray("terms")[0] = CreateTermArray(
                                                            "prop:due<=" + numberBox.Number,
                                                            MAX_NUMBER,
                                                            (int)DynamicDeckOrder.DUE);
                    dynamicDeck["resched"] = JsonValue.CreateBooleanValue(true);
                    break;

                case CustomStudyOption.PreviewNew:
                    dynamicDeck["delays"] = JsonValue.CreateNullValue();
                    dynamicDeck.GetNamedArray("terms")[0] = CreateTermArray(
                                                            "is:new added:" + numberBox.Number,
                                                            MAX_NUMBER,
                                                            (int)DynamicDeckOrder.OLDEST);
                    dynamicDeck["resched"] = JsonValue.CreateBooleanValue(false);
                    break;

                case CustomStudyOption.CramMode:
                    StringBuilder tags = new StringBuilder();
                    AdvancedSearchPopup.AppendTags(tags, "tag:", includeTagsViewModel);
                    AdvancedSearchPopup.AppendTags(tags, "-tag:", excludeTagsViewModel);

                    dynamicDeck["delays"] = JsonValue.CreateNullValue();
                    dynamicDeck.GetNamedArray("terms")[0] = CreateTermArray(
                                                            tags.ToString().Trim(),
                                                            cramNumberBox.Number,
                                                            (int)DynamicDeckOrder.RANDOM);
                    if(rescheduleCheckBox.IsChecked == true)
                        dynamicDeck["resched"] = JsonValue.CreateBooleanValue(true);
                    else
                        dynamicDeck["resched"] = JsonValue.CreateBooleanValue(false);
                    break;
                default:
                    break;
            }
            string term = dynamicDeck.GetNamedArray("terms").GetArrayAt(0).GetStringAt(0);
            term = "deck:\"" + deck.GetNamedString("name") + "\" " + term;
            dynamicDeck.GetNamedArray("terms").GetArrayAt(0)[0] = JsonValue.CreateStringValue(term);
        }

        private void extendReview(JsonObject deck)
        {
            deck["extendRev"] = JsonValue.CreateNumberValue(numberBox.Number);
            collection.Deck.Save(deck);
            collection.Sched.ExtendLimits(0, numberBox.Number);
            CustomStudyCreateEvent?.Invoke(studyOption, currentDeckId);
        }

        private void extendNewLimit(JsonObject deck)
        {
            deck["extendNew"] = JsonValue.CreateNumberValue(numberBox.Number);
            collection.Deck.Save(deck);
            collection.Sched.ExtendLimits(numberBox.Number, 0);
            CustomStudyCreateEvent?.Invoke(studyOption, currentDeckId);
        }

        private JsonArray CreateTermArray(string value, int max, int order)
        {
            var jsonArray = new JsonArray();
            jsonArray.Add(JsonValue.CreateStringValue(value));
            jsonArray.Add(JsonValue.CreateNumberValue(max));
            jsonArray.Add(JsonValue.CreateNumberValue(order));
            return jsonArray;
        }

        private JsonArray CreateNumberArray(params double[] value)
        {
            var jsonArray = new JsonArray();
            foreach(var v in  value)            
                jsonArray.Add(JsonValue.CreateNumberValue(v));
            return jsonArray;
        }

        private void WindowSizeChanged(object sender, SizeChangedEventArgs e)
        {            
            CalculateSizeAndPosition();
        }
    }
}
