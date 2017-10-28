using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using AnkiU.ViewModels;
using AnkiU.Models;
using AnkiU.AnkiCore;
using AnkiU.Interfaces;
using AnkiU.UIUtilities;
using AnkiU.UserControls;
using System.Threading.Tasks;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace AnkiU.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class TagManager : Page, INightReadMode
    {
        private const string REMOVE_TAGS_MESSAGE = "Remove \"{0}\" tag from {1} notes and collection?";
        private TagInformationViewModel ViewModel { get; set; }
        private TagInformation selectedTag;
        private MainPage mainPage;
        private Collection collection;

        private ThreeOptionsDialog confirmDialog;
        private NameEnterFlyout nameEnterFlyout;

        private bool isNightMode;        

        public TagManager()
        {
            this.InitializeComponent();
            InitConfirmDialog();
            InitNameEnterFlyout();
        }

        private void InitConfirmDialog()
        {
            confirmDialog = new ThreeOptionsDialog();
            confirmDialog.LeftButton.Visibility = Visibility.Collapsed;
            confirmDialog.MiddleButton.Content = "Yes";
            confirmDialog.RightButton.Content = "No";
            confirmDialog.NotAskAgainVisibility = Visibility.Visible;
            confirmDialog.Title = "Delete Tag";
        }

        private void InitNameEnterFlyout()
        {
            nameEnterFlyout = new NameEnterFlyout();
            nameEnterFlyout.OkButtonClickEvent += OnNameEnterFlyoutOkButtonClickEvent;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            mainPage = e.Parameter as MainPage;
            if (mainPage == null)
                throw new Exception("Wrong parameter type!");
            collection = mainPage.Collection;
            ViewModel = new TagInformationViewModel(collection, collection.NewNote());

            HookAllEvents();
        }

        private void HookAllEvents()
        {
            mainPage.CommanBar.ClosedDisplayMode = AppBarClosedDisplayMode.Minimal;
            mainPage.EnableChangingReadMode(this);
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            UnHookAllEvents();
            base.OnNavigatingFrom(e);
        }

        private void UnHookAllEvents()
        {
            mainPage.CommanBar.ClosedDisplayMode = AppBarClosedDisplayMode.Compact;
            mainPage.DisableChangingReadMode();
        }

        private void SearchTextBoxTextChangedHandler(object sender, TextChangedEventArgs e)
        {
            if (String.IsNullOrEmpty(searchTextBox.Text))
            {
                foreach (var item in allTagsView.Items)
                {
                    var tag = item as TagInformation;
                    tag.Visibility = Visibility.Visible;
                }
                return;
            }

            foreach (var item in allTagsView.Items)
            {
                var tag = item as TagInformation;
                if (tag.Name.ToUpperInvariant().Contains(searchTextBox.Text.ToUpperInvariant()))
                    tag.Visibility = Visibility.Visible;
                else
                    tag.Visibility = Visibility.Collapsed;
            }

        }

        public void ToggleReadMode()
        {
            isNightMode = !isNightMode;
            if (isNightMode)
                userControl.Background = new SolidColorBrush(Windows.UI.Colors.Black);
            else
                userControl.Background = new SolidColorBrush(Windows.UI.Colors.White);            
        }

        private async void OnDeleteMenuFlyoutItemClick(object sender, RoutedEventArgs e)
        {
            var isContinue = await MainPage.WarnFullSyncIfNeeded();
            if (!isContinue)
                return;
            collection.ModSchema();

            if (selectedTag == null)
                return;

            if (CheckIfSystemTag(selectedTag.Name))
            {
                await ShowInvaildActionMessage();
                return;
            }

            var noteList = collection.FindNotes("tag:" + selectedTag.Name);

            if (!confirmDialog.IsNotAskAgain())
            {
                confirmDialog.Message = String.Format(REMOVE_TAGS_MESSAGE, selectedTag.Name, noteList.Count);
                await confirmDialog.ShowAsync();
                if (confirmDialog.IsRightButtonClick())
                    return;
            }

            collection.Tags.RemoveTagFromNotesAndCollection(noteList, selectedTag.Name);
            selectedTag.Visibility = Visibility.Collapsed;
            ViewModel.Tags.Remove(selectedTag);
            selectedTag = null;
        }

        private async void OnRenameMenuFlyoutItemClick(object sender, RoutedEventArgs e)
        {
            var isContinue = await MainPage.WarnFullSyncIfNeeded();
            if (!isContinue)
                return;

            if (selectedTag == null)
                return;

            if (CheckIfSystemTag(selectedTag.Name))
            {
                await ShowInvaildActionMessage();
                return;
            }
            nameEnterFlyout.Show(pointToShowFlyout, selectedTag.Name);
        }

        private async void OnNameEnterFlyoutOkButtonClickEvent(object sender, RoutedEventArgs e)
        {
            collection.ModSchema();
            var newName = nameEnterFlyout.NewName.Trim();
            if (!IsValidTagName(newName))
            {
                await UIHelper.ShowMessageDialog("Invalid tag name! Please enter a different name.");
                nameEnterFlyout.Show(pointToShowFlyout, newName);
                return;
            }

            var noteList = collection.FindNotes("tag:" + selectedTag.Name);
            collection.Tags.RenameTag(noteList, selectedTag.Name, newName);
            selectedTag.Name = newName;
            selectedTag = null;           
        }

        private bool IsValidTagName(string tagName)
        {
            if (String.IsNullOrWhiteSpace(tagName))            
                return false;

            if (CheckIfSystemTag(tagName))
                return false;

            if (tagName.Contains(" "))
                return false;

            if (collection.Tags.GetTags().ContainsKey(tagName))
                return false;

            return true;
        }

        private void OnTagButtonTapped(object sender, TappedRoutedEventArgs e)
        {
            var button = sender as FrameworkElement;
            if (button == null)            
                selectedTag = null;                
            else
                selectedTag = button.DataContext as TagInformation;

            if (selectedTag == null)
                return;

            var pointerPosition = e.GetPosition(tagListRoot);
            pointToShowFlyout.Margin = new Thickness(pointerPosition.X, pointerPosition.Y, 0, 0);
            tagsMenuFlyout.ShowAt(pointToShowFlyout);
        }        

        private bool CheckIfSystemTag(string tag)
        {
            if (tag.Equals("suspend", StringComparison.OrdinalIgnoreCase))
                return true;

            if (tag.Equals("leech", StringComparison.OrdinalIgnoreCase))
                return true;

            if (tag.Equals("marked", StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private async Task ShowInvaildActionMessage()
        {
            await UIHelper.ShowMessageDialog("Invalid action for the selected tag!");
        }
    }
}
