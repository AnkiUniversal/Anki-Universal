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
using AnkiU.Models;
using AnkiU.UIUtilities;
using AnkiU.ViewModels;
using AnkiU.Views;
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
    public sealed partial class AdvancedSearchPopup : UserControl
    {
        private const string IS_DUE = "is:due";
        private const string IS_NEW = "is:new";
        private const string IS_LEARN = "is:learn";
        private const string IS_REVIEW = "is:review";
        private const string IS_SUSPEND = "is:suspended";

        private const int DEFAULT_WIDTH_MARGIN = 5;

        private Collection collection;              

        private DeckNameViewModel deckNameSearchViewModel;        
        private DeckMultiSelectFlyout deckSelectFlyout;
        private StringBuilder searchDeck = new StringBuilder();
        private bool isDeckSelectsModified = true;

        private MultiNoteFieldsSelectViewModel noteFieldsViewModel;
        private FieldMultiSelect noteFieldsViewFlyout;

        private double HorizontalOffset;
        private double VerticalOffset;

        private TagInformationViewModel tagInforViewModel;
        private string searchTag = "";

        private Dictionary<string, bool> cardState = new Dictionary<string, bool>();
        private string searchCardState = "";        

        public event RoutedEventHandler Closed;
        public event RoutedEventHandler SearchClick;
        public event RoutedEventHandler CloseClick;
        public event RoutedEventHandler ShowCommandCheckClick;

        public bool IsOpen { get { return popup.IsOpen; } }
        public bool IsShowCommands { get { return (bool)showCommandCheckBox.IsChecked; } set { showCommandCheckBox.IsChecked = value; } }        

        public AdvancedSearchPopup(Collection collection, double verticalOffset, double horizontalOffset)
        {
            this.InitializeComponent();
            this.collection = collection;
            HorizontalOffset = horizontalOffset;
            VerticalOffset = verticalOffset;
            popup.HorizontalOffset = horizontalOffset;
            popup.VerticalOffset = verticalOffset;

            SetupDeckSelection();
            SetupTagSelection();
            SetupCartStateSelection();
        }

        public void Toggle(double width)
        {
            if (popup.IsOpen)
                Hide();
            else
                Show(width);
        }

        private bool isNightMode = false;
        public void ChangeReadMode(bool isNightMode)
        {
            if (this.isNightMode == isNightMode)
                return;
            this.isNightMode = isNightMode;
            if(isNightMode)
            {
                userControl.Background = UIHelper.BackgroundAcrylicDarkBrush;
                userControl.Foreground = Application.Current.Resources["ForeGroundLight"] as SolidColorBrush;
            }
            else
            {
                userControl.Background = UIHelper.BackgroundAcrylicLightBrush;
                userControl.Foreground = new SolidColorBrush(Windows.UI.Colors.Black);
            }
        }

        public void Show(double width)
        {            
            popup.IsOpen = true;
            FadeIn.Begin();

            if (width < popup.MinWidth)
            {                
                popup.Width = popup.MinWidth;
                rootGrid.Width = popup.MinWidth;

                if (rootGrid.Width + HorizontalOffset > userControl.ActualWidth)
                {
                    if (rootGrid.Width + DEFAULT_WIDTH_MARGIN < userControl.ActualWidth)
                        popup.HorizontalOffset = DEFAULT_WIDTH_MARGIN;
                    else
                        popup.HorizontalOffset = -(popup.MinWidth - userControl.ActualWidth) / 2;
                }
                else
                    popup.HorizontalOffset = HorizontalOffset;

            }
            else
            {
                popup.HorizontalOffset = HorizontalOffset;
                popup.Width = width;
                rootGrid.Width = width;
            }
        }

        public void Hide()
        {
            popup.IsOpen = false;
        }

        public void InitDeckSelected(long deckId)
        {
            foreach (var deck in deckNameSearchViewModel.Decks)
            {
                if (deck.Id == deckId)
                {
                    deck.IsChecked = true;
                    searchDeck.Clear();
                    searchDeck.Append("\"deck:" + deck.Name + "\"");
                    deckSelectTextBox.Text = searchDeck.ToString();
                    deckSelectFlyout.SelectedDecks.Clear();
                    deckSelectFlyout.SelectedDecks.Add(deck);
                    isDeckSelectsModified = true;
                }
            }
        }

        public string GetSearchString()
        {
            StringBuilder builder = new StringBuilder();       
                 
            AppendWithSpaceIfNeeded(builder, searchDeck.ToString());
            AppendWithSpaceIfNeeded(builder, searchTag);
            AppendWithSpaceIfNeeded(builder, GetFieldSearchString());
            AppendWithSpaceIfNeeded(builder, searchCardState);

            if (addedCheckBox.IsChecked == true)
                AppendWithSpaceIfNeeded(builder, "added:" + addedNumberBox.Number);
            
            return builder.ToString().Trim();
        }

        private string GetFieldSearchString()
        {
            if (noteFieldsViewFlyout == null || noteFieldsViewFlyout.SelectedFields.Count == 0)
                return "";

            if (String.IsNullOrWhiteSpace(fieldContentTextBox.Text))            
                return "";            

            StringBuilder text = new StringBuilder();
            var fields = noteFieldsViewFlyout.SelectedFields;
            int lastPos = fields.Count - 1;

            bool isAddedDivWrap = false;
            for (int i = 0; i <= lastPos; i++)
            {
                AddFieldContent(text, fields[i], fieldContentTextBox.Text);
                isAddedDivWrap = AddDivWrapIfNeeded(text, fields[i], isAddedDivWrap);

                if (i < lastPos)
                    text.Append(" or ");
            }

            if (noteFieldsViewFlyout.SelectedFields.Count > 1 || isAddedDivWrap)
            {
                text.Insert(0, "(");
                text.Insert(text.Length, ")");
            }

            return text.ToString();
        }

        private bool AddDivWrapIfNeeded(StringBuilder text, string field, bool isAddedDivWrap)
        {
            if (!fieldContentTextBox.Text.StartsWith("*") && !fieldContentTextBox.Text.EndsWith("*"))
            {
                text.Append(" or ");
                AddFieldContent(text, field, "<div>" + fieldContentTextBox.Text + "</div>");
                return true;
            }
            else if (!fieldContentTextBox.Text.StartsWith("*"))
            {
                text.Append(" or ");
                AddFieldContent(text, field, "<div>" + fieldContentTextBox.Text);
                return true;
            }
            else if (!fieldContentTextBox.Text.EndsWith("*"))
            {
                text.Append(" or ");
                AddFieldContent(text, field, fieldContentTextBox.Text + "</div>");
                return true;
            }

            return isAddedDivWrap;
        }

        private void AddFieldContent(StringBuilder text, string field, string content)
        {
            text.Append("\"");
            text.Append(field);
            text.Append("\":\"");
            text.Append(content);
            text.Append("\"");
        }

        private static void AppendWithSpaceIfNeeded(StringBuilder builder, string text)
        {
            if (text.Length > 0)
            {
                builder.Append(text);
                builder.Append(" ");
            }
        }

        private void SetupDeckSelection()
        {
            deckNameSearchViewModel = new DeckNameViewModel(collection, false, true);
            deckSelectFlyout = new DeckMultiSelectFlyout(deckNameSearchViewModel);
            deckSelectFlyout.FlyoutClosed += DeckSelectFlyoutClosed;
        }

        private void DeckSelectFlyoutClosed(object sender, RoutedEventArgs e)
        {
            searchDeck.Clear();
            var decks = deckSelectFlyout.SelectedDecks;
            var length = decks.Count;            

            for (int i = 0; i < length; i++)
            {
                searchDeck.Append("\"deck:" + decks[i].Name + "\"");
                if(i < length - 1)
                    searchDeck.Append(" or ");
            }
            if(length > 1)
            {
                searchDeck.Insert(0, '(');
                searchDeck.Insert(searchDeck.Length, ')');
            }            

            deckSelectTextBox.Text = searchDeck.ToString();
            isDeckSelectsModified = true;
        }

        private void SetupTagSelection()
        {
            tagInforViewModel = new TagInformationViewModel(collection, collection.NewNote());
            includeTags.ViewModel = tagInforViewModel;
            includeTags.LabelVisibility = Visibility.Collapsed;
            includeTags.Placement = FlyoutPlacementMode.Bottom;
            includeTags.TagFlyoutClosedEvent += TagFlyoutClosedEventHandler;
        }

        private void TagFlyoutClosedEventHandler(object sender, EventArgs e)
        {
            StringBuilder builder = new StringBuilder();
            AppendTags(builder, "tag:", tagInforViewModel);
            searchTag = builder.ToString();         
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

        private void SetupCartStateSelection()
        {
            cardState.Add(IS_DUE, false);
            cardState.Add(IS_NEW, false);
            cardState.Add(IS_LEARN, false);
            cardState.Add(IS_REVIEW, false);
            cardState.Add(IS_SUSPEND, false);
        }

        private async void CheckBoxCheckedHandler(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            if (checkBox != null)
                ChangeCardStateString(checkBox, true);
            await WarningInvalidStatesSelection();
        }

        private async Task WarningInvalidStatesSelection()
        {
            if ((bool)reviewCardStateCheckBox.IsChecked && (bool)newCardStateCheckBox.IsChecked)
                await UIHelper.ShowMessageDialog("A card can't have both \"New\" and \"Review\" state. This is an invalid search");
        }

        private void ChangeCardStateString(CheckBox checkBox, bool state)
        {
            switch (checkBox.Content.ToString().ToLowerInvariant())
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
            foreach (var key in cardState)
            {
                if (key.Value)
                {
                    builder.Append(key.Key);
                    builder.Append(" ");
                }
            }
            searchCardState = builder.ToString();
        }

        private void CheckBoxUncheckedHandler(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;
            ChangeCardStateString(checkBox, false);
        }

        private void PopupClosed(object sender, object e)
        {
            Closed?.Invoke(sender, null);
        }

        private void DeckSelectButtonClick(object sender, RoutedEventArgs e)
        {
            deckSelectFlyout.ShowFlyout(deckSelectButton, FlyoutPlacementMode.Bottom);
        }

        private void WindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (popup.IsOpen)
                Hide();
            popup.MaxHeight = userControl.ActualHeight;
            rootGrid.MaxHeight = popup.MaxHeight;

            if(popup.MinWidth > userControl.ActualWidth)
            {
                var scale = userControl.ActualWidth / popup.MinWidth;
                contentScale.ScaleX = scale;
                contentScale.ScaleY = scale;
                popup.VerticalOffset = VerticalOffset - (popup.ActualHeight*(1 - scale)/2);
            }
            else
            {
                contentScale.ScaleX = 1;
                contentScale.ScaleY = 1;
                popup.VerticalOffset = VerticalOffset;
            }
        }

        private void SearchButtonClick(object sender, RoutedEventArgs e)
        {
            Hide();
            SearchClick?.Invoke(sender, e);
        }

        private void CloseButtonClick(object sender, RoutedEventArgs e)
        {
            Hide();
            CloseClick?.Invoke(sender, e);
        }

        private void ShowCommandCheckBoxClick(object sender, RoutedEventArgs e)
        {
            ShowCommandCheckClick?.Invoke(sender, e);
        }

        private async void OnFieldListViewButtonClick(object sender, RoutedEventArgs e)
        {
            if (!(await InitFieldFlyoutIfNeeded()))
                return;

            noteFieldsViewFlyout.ShowFlyout(fieldListViewButton, FlyoutPlacementMode.Bottom);
        }

        private async Task<bool> InitFieldFlyoutIfNeeded()
        {
            if (isDeckSelectsModified)
            {
                if (deckSelectFlyout.SelectedDecks.Count == 0)
                {
                    await UIHelper.ShowMessageDialog("Please choose the decks you want to search first.");
                    return false;
                }

                List<JsonObject> models = new List<JsonObject>();
                foreach (var deck in deckSelectFlyout.SelectedDecks)
                {
                    var model = collection.Models.TryGetModel(deck.Id);
                    if (model != null)
                        models.Add(model);
                }

                if (models.Count == 0)
                {
                    await UIHelper.ShowMessageDialog("The selected decks do not have any cards.");
                    return false;
                }

                noteFieldsViewModel = new MultiNoteFieldsSelectViewModel(models);
                noteFieldsViewFlyout = new FieldMultiSelect(noteFieldsViewModel);
                noteFieldsViewFlyout.FlyoutClosed += NoteFieldsViewFlyoutClosed;
                isDeckSelectsModified = false;
            }

            return true;
        }

        private void NoteFieldsViewFlyoutClosed(object sender, RoutedEventArgs e)
        {
            fieldListTextBox.Text = String.Join("; ", noteFieldsViewFlyout.SelectedFields);
        }

        private async void OnFieldContentTextBoxTextChanged(object sender, TextChangedEventArgs e)
        {
            if(noteFieldsViewFlyout == null || noteFieldsViewFlyout.SelectedFields.Count == 0)
            {
                await UIHelper.ShowMessageDialog("Please select a field name (the space on the left) first!");
            }
        }
    }
}
