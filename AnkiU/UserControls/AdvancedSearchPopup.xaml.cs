using AnkiU.AnkiCore;
using AnkiU.UIUtilities;
using AnkiU.ViewModels;
using AnkiU.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
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

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

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

        private double HorizontalOffset;

        private TagInformationViewModel tagInforViewModel;
        private string searchTag = "";

        private Dictionary<string, bool> cardState = new Dictionary<string, bool>();
        private string searchCardState = "";        

        public event RoutedEventHandler Closed;

        public bool IsOpen { get { return popup.IsOpen; } }

        public AdvancedSearchPopup(Collection collection, double verticalOffset, double horizontalOffset)
        {
            this.InitializeComponent();
            this.collection = collection;
            HorizontalOffset = horizontalOffset;
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

        public void ChangeReadMode(bool isNightMode)
        {            
            if(isNightMode)
            {
                userControl.Background = Application.Current.Resources["DarkerGray"] as SolidColorBrush;
                userControl.Foreground = Application.Current.Resources["ForeGroundLight"] as SolidColorBrush;
            }
            else
            {
                userControl.Background = new SolidColorBrush(Windows.UI.Colors.White);
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
                    popup.HorizontalOffset = DEFAULT_WIDTH_MARGIN;                
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

        public StringBuilder GetSearchString()
        {
            StringBuilder builder = new StringBuilder();       
                 
            AppendWithSpaceIfNeeded(builder, searchDeck.ToString());

            AppendWithSpaceIfNeeded(builder, searchTag);

            builder.Append(searchCardState);

            return builder;
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
        }
    }
}
