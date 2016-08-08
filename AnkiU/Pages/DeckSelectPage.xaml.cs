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
using AnkiU.ViewModels;
using System;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using AnkiU.Models;
using AnkiU.Interfaces;
using Windows.UI.Core;
using Windows.Storage;
using AnkiU.AnkiCore.Exporter;
using Windows.UI.Popups;
using AnkiU.UserControls;
using AnkiU.UIUtilities;
using Windows.UI;
using AnkiU.Views;
using System.Collections.Generic;
using AnkiU.Anki;

namespace AnkiU.Pages
{
    public sealed partial class DeckSelectPage : Page, INightReadMode
    {
        private const int DEFAULT_HELP_POPUP_VERTICAL_OFFSET = 50;
        private const double DEFAULT_OPACITY = 0.8;
        private const double REFRESH_RATE = 1;

        private IAnkiDecksView decksView;
        private DeckListViewModel deckListViewModel;        

        private static bool isNoticeBoardClosed = false;

        private DeckConfigNameViewModel configNameViewModel;

        private MenuFlyout deckMenuFlyout;        
        private MenuFlyout dynamicDeckMenuFlyout;
        private MenuFlyout collectionMenuFlyout;
        private Flyout currentShownFlyout;        
        private NameEnterFlyout renameFlyout;
        private CustomStudyFlyout customStudyFlyout = null;
        private HelpPopup helpPopup = null;
        private ProgressDialog progressDialog;
        private StorageFolder exportFolder = null;

        private DeckInformation deckShowContextMenu;
        public DeckInformation DeckShowContextMenu { get { return deckShowContextMenu; } }                        

        private bool isPointerPressed;
        private bool isNightMode = false;
        private DateTime lastRefreshDate;

        public event CreateNewDeckFlyout.NewDeckCreatedHandler NewDeckCreatedEvent;

        private MainPage mainPage;
        public MainPage MainPage { get { return mainPage; } }

        private Collection collection;
        public Collection Collection { get { return collection; } }

        private long selectedDeckId;
        public long SelectedDeckId { get { return selectedDeckId; } }

        private long selectedConfigId;
        public long SelectedConfigId { get { return selectedConfigId; } }

        public bool IsCreatingNewConfig { get; set; }        

        public DeckSelectPage()
        {
            this.InitializeComponent();
            deckMenuFlyout = Resources["DeckContextMenu"] as MenuFlyout;
            lastRefreshDate = DateTimeOffset.Now.DateTime;

            //Work around to show help text
            DeckConfigName.PointToShowFlyoutStatic = pointToShowFlyout;
            DeckConfigName.ParentFlyoutStatic = configureFlyout;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            mainPage = e.Parameter as MainPage;
            if (mainPage == null)
                throw new Exception("Wrong input parameter!");
            
            collection = mainPage.Collection;
            GetDecksInformation();
            CloseNoticeBoardIfNeeded();
            UpdateNoticeText();
            ShowDayTimeSymbol();

            EnterTutorialModeIfNeeded();

            ShowAllButtonOfThisPage();
            HookAllEvents();            
        }    

        private void ShowDayTimeSymbol()
        {
            TimeSpan start = new TimeSpan(6, 0, 0);
            TimeSpan end = new TimeSpan(18, 0, 0);
            TimeSpan now = DateTime.Now.TimeOfDay;
            if ((now > start) && (now < end))
            {
                moonSymbol.Visibility = Visibility.Collapsed;
                sunSymbol.Visibility = Visibility.Visible;
            }
            else
            {
                moonSymbol.Visibility = Visibility.Visible;
                sunSymbol.Visibility = Visibility.Collapsed;
            }
        }

        private void HookAllEvents()
        {
            mainPage.EnableChangingReadMode(this);
            ChangeBackgroundColor();
            mainPage.DeckImageChangedEvent += MainPageDeckImageChangedHandler;
            Window.Current.VisibilityChanged += CurrentWindowVisibilityChangedHandler;
            mainPage.GridViewButton.Click += GridViewButtonClickHandler;
            mainPage.ListViewButton.Click += ListViewButtonClickHandler;
            mainPage.AddButton.Click += AddButtonClickHandler;
            mainPage.DragAndDropButton.Click += DragAndDropButtonClick;
        }

        private void DragAndDropButtonClick(object sender, RoutedEventArgs e)
        {
            if (!decksView.IsDragAndDropEnable)
            {
                ChangeToDragAndDropMode();
            }
            else
            {
                ReturnToDeckSelection();
            }

        }

        private void ChangeToDragAndDropMode()
        {
            mainPage.ListViewButton.IsEnabled = false;
            mainPage.GridViewButton.IsEnabled = false;
            mainPage.DragAndDropButton.Background = UIHelper.IndioBrush;
            mainPage.DragAndDropButton.Foreground = new SolidColorBrush(Windows.UI.Colors.White);
            decksView.EnableDragAndDropMode();
        }

        private void ReturnToDeckSelection()
        {
            mainPage.ListViewButton.IsEnabled = true;
            mainPage.GridViewButton.IsEnabled = true;
            mainPage.BindToCommandBarForeGround(mainPage.DragAndDropButton);
            mainPage.DragAndDropButton.Background = new SolidColorBrush(Windows.UI.Colors.Transparent);
            decksView.DisableDragAndDropMode();
        }

        private void OnDeckDragAnDrop(DeckInformation parent, DeckInformation child)
        {
            if (collection.Deck.IsParent(parent.Name, child.Name))
                //Remove from children if child deck is dragged on parent deck
                collection.Deck.RenameForDragAndDrop(child.Id, null);
            else
                collection.Deck.RenameForDragAndDrop(child.Id, parent.Id);

            deckListViewModel.UpdateDeckName(child);
            UpdateChildrenName(child);

            deckListViewModel.UpdateCardCountAllDecks();
            UpdateNoticeText();
            collection.SaveAndCommitAsync();
        }

        private void UpdateChildrenName(DeckInformation child)
        {
            Dictionary<string, long> children = collection.Deck.Children(child.Id);
            foreach (var deck in children)
            {
                var deckInfor = deckListViewModel.GetDeck(deck.Value);
                deckListViewModel.UpdateDeckName(deckInfor);
            }            
        }

        private async void MainPageDeckImageChangedHandler(StorageFile fileToChange, long deckId, long modifiedTime)
        {
            var deckInfor = deckListViewModel.GetDeck(deckId);
            await deckInfor.ChangeImage(fileToChange, modifiedTime);
        }

        private void AddButtonClickHandler(object sender, RoutedEventArgs e)
        {
            CreateNewDeckFlyout newDeckFlyout = new CreateNewDeckFlyout(collection, mainPage.CurrentDispatcher);
            newDeckFlyout.ShowFlyout(mainPage.AddButton, FlyoutPlacementMode.Bottom);
            newDeckFlyout.NewDeckCreatedEvent += NewDeckCreatedEventHandler;
            newDeckFlyout.ClosedWithoutCreatingDeckEvent += NewDeckFlyoutClosedWithoutCreatingDeckHandler;
        }        

        private async void NewDeckCreatedEventHandler(long deckId)
        {
            await mainPage.CurrentDispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                deckListViewModel.AddNewDeck(deckId);
                NewDeckCreatedEvent?.Invoke(deckId);
            });
        }

        private void CurrentWindowVisibilityChangedHandler(object sender, VisibilityChangedEventArgs e)
        {
            if (!e.Visible)
                SaveSession();

            RefreshCardCountsIfNeeded();
        }

        private void RefreshCardCountsIfNeeded()
        {
            var currentDate = DateTimeOffset.Now.DateTime;
            var deltaTime = (currentDate - lastRefreshDate).TotalHours;
            if (deltaTime > REFRESH_RATE)
            {
                deckListViewModel.UpdateCardCountAllDecks();
                lastRefreshDate = currentDate;
            }
        }

        private void SaveSession()
        {
            collection.SaveAndCommit();
        }

        private void ShowAllButtonOfThisPage()
        {
            mainPage.DragAndDropButton.Visibility = Visibility.Visible;
            mainPage.RootSplitView.Pane.Visibility = Visibility.Visible;
            mainPage.HelpSplitView.Visibility = Visibility.Visible;
            mainPage.AddButton.Visibility = Visibility.Visible;
            mainPage.SplitViewToggleButton.Visibility = Visibility.Visible;
            mainPage.SyncButton.Visibility = Visibility.Visible;
            if (MainPage.UserPrefs.IsDeckListView)
                SwitchToListView();
            else
                SwitchToGridView();
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            if(decksView.IsDragAndDropEnable)
                ReturnToDeckSelection();

            HideAllButtonOfThisPage();
            UnHookAllEvents();

            base.OnNavigatingFrom(e);
        }

        private void HideAllButtonOfThisPage()
        {
            mainPage.DragAndDropButton.Visibility = Visibility.Collapsed;
            mainPage.RootSplitView.Pane.Visibility = Visibility.Collapsed;
            mainPage.HelpSplitView.Visibility = Visibility.Collapsed;
            mainPage.AddButton.Visibility = Visibility.Collapsed;
            mainPage.SplitViewToggleButton.Visibility = Visibility.Collapsed;
            mainPage.SyncButton.Visibility = Visibility.Collapsed;
            if (MainPage.UserPrefs.IsDeckListView)
                mainPage.GridViewButton.Visibility = Visibility.Collapsed;
            else
                mainPage.ListViewButton.Visibility = Visibility.Collapsed;
        }

        private void UnHookAllEvents()
        {
            mainPage.DisableChangingReadMode();
            mainPage.DeckImageChangedEvent -= MainPageDeckImageChangedHandler;
            Window.Current.VisibilityChanged -= CurrentWindowVisibilityChangedHandler;
            mainPage.GridViewButton.Click -= GridViewButtonClickHandler;
            mainPage.ListViewButton.Click -= ListViewButtonClickHandler;
            mainPage.AddButton.Click -= AddButtonClickHandler;
            mainPage.DragAndDropButton.Click -= DragAndDropButtonClick;
        }

        private void ListViewButtonClickHandler(object sender, RoutedEventArgs e)
        {
            SwitchToListView();
        }

        private void GridViewButtonClickHandler(object sender, RoutedEventArgs e)
        {
            SwitchToGridView();
        }

        private void UpdateNoticeText()
        {
            totalCardsText.Text = "Today's cards: " + (deckListViewModel.TotalNewCards + deckListViewModel.TotalDueCards);
            newCardsText.Text = "New: " + deckListViewModel.TotalNewCards;
            dueCardsText.Text = "Due: " + deckListViewModel.TotalDueCards;
        }

        private void SwitchToGridView()
        {
            MainPage.UserPrefs.IsDeckListView = false;
            if (deckListView != null)
                deckListView.Visibility = Visibility.Collapsed;
            if (deckGridView == null)
            {
                this.FindName("deckGridView");
                decksView = deckGridView as IAnkiDecksView;
                decksView.DataContext = deckListViewModel.Decks;
                HookDeckItemEvent();
            }
            decksView = deckGridView;
            mainPage.ListViewButton.Visibility = Visibility.Visible;
            mainPage.GridViewButton.Visibility = Visibility.Collapsed;
            deckGridView.Visibility = Visibility.Visible;
        }

        private void SwitchToListView()
        {
            MainPage.UserPrefs.IsDeckListView = true;
            if (deckGridView != null)
                deckGridView.Visibility = Visibility.Collapsed;
            if (deckListView == null)
            {
                this.FindName("deckListView");
                decksView = deckListView as IAnkiDecksView;
                decksView.DataContext = deckListViewModel.Decks;
                HookDeckItemEvent();
            }
            decksView = deckListView;
            mainPage.ListViewButton.Visibility = Visibility.Collapsed;
            mainPage.GridViewButton.Visibility = Visibility.Visible;
            deckListView.Visibility = Visibility.Visible;
        }

        private void HookDeckItemEvent()
        {
            decksView.DeckItemClickEvent += DeckListViewItemClickEventHandler;
            decksView.DragAnDropEvent += OnDeckDragAnDrop;
        }

        private void DeckListViewItemClickEventHandler(long deckId)
        {
            mainPage.Collection.Deck.Select(deckId, false);
            var deck = deckListViewModel.GetDeck(deckId);
            if (deck.NewCards > 0 || deck.DueCards > 0)
            {
                MakesureCleanState();
                mainPage.Collection.Sched.Reset();
                Frame.Navigate(typeof(ReviewPage), mainPage);
            }
            else if (!collection.Deck.IsDyn(deckId))
            {
                if (customStudyFlyout == null)
                    InitCustomStudyFlyout();

                customStudyFlyout.InitDeckValue(collection, isNightMode);
                customStudyFlyout.Show();
            }
            else
            {
                var task = mainPage.CurrentDispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    await UIHelper.ShowMessageDialog("This \"Custom Study\" deck has no cards to learn or review. Please delete it.");
                });
            }
        }

        private void InitCustomStudyFlyout()
        {
            customStudyFlyout = new CustomStudyFlyout(mainPage.CurrentDispatcher);
            customStudyFlyout.CustomStudyCreateEvent += CustomStudyCreateEventHandler;
            UIHelper.AddToGridInFull(mainGrid, customStudyFlyout);            
        }

        private void CustomStudyCreateEventHandler(CustomStudyFlyout.CustomStudyOption studyOption, long deckId)
        {
            if (studyOption == CustomStudyFlyout.CustomStudyOption.IncreaseNewToDay ||
                studyOption == CustomStudyFlyout.CustomStudyOption.IncreaseReviewToDay)
            {
                deckListViewModel.UpdateCardCountForDeck(deckId);
            }
            else
            {
                deckListViewModel.AddOrUpdateDeckCardCount(deckId);
            }

            UpdateNoticeText();
        }

        private void MakesureCleanState()
        {
            mainPage.Collection.ClearUndo();
            ReviewPage.ClearReviewUndo();
        }

        private void GetDecksInformation()
        {
            deckListViewModel = new DeckListViewModel(mainPage.Collection);
            deckListViewModel.GetAllDeckInformation();
        }

        private void PointerPressedHandler(object sender, PointerRoutedEventArgs e)
        {
            isPointerPressed = true;
        }

        private void DeckViewRightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            if (isPointerPressed)
            {
                var pointerPosition = e.GetPosition(mainGrid);
                pointToShowFlyout.Margin = new Thickness(pointerPosition.X, pointerPosition.Y, 0, 0);

                var deck = (e.OriginalSource as FrameworkElement).DataContext as DeckInformation;                
                ShowContextMenu(deck, null, e.GetPosition(null));                
                    
                e.Handled = true;
            }
        }

        private void DeckItemHolding(object sender, HoldingRoutedEventArgs e)
        {
            if (e.HoldingState == Windows.UI.Input.HoldingState.Started)
            {
                var pointerPosition = e.GetPosition(mainGrid);
                pointToShowFlyout.Margin = new Thickness(pointerPosition.X, pointerPosition.Y, 0, 0);

                var deck = (e.OriginalSource as FrameworkElement).DataContext as DeckInformation;                
                ShowContextMenu(deck, null, e.GetPosition(null));
                e.Handled = true;

                // This, combined with a check in OnRightTapped prevents the firing of RightTapped from
                // launching another context menu
                isPointerPressed = false;
            }
        }

        private void ShowContextMenu(DeckInformation deck, UIElement target, Point offset)
        {
            if (decksView.IsDragAndDropEnable)
                return;

            if (deck != null)
            {
                MakeSureNoFlyoutsOpen();
                ShowDeckContextMenu(deck, target, offset);
                deckShowContextMenu = deck;
            }
            else
            {
                ShowCollectionContextMenu(target, offset);
            }
        }

        private void MakeSureNoFlyoutsOpen()
        {
            if (customStudyFlyout != null)
                customStudyFlyout.Hide();
        }

        private void ShowDeckContextMenu(DeckInformation deck, UIElement target, Point offset)
        {
            if (deck.IsDynamic)
            {
                if (dynamicDeckMenuFlyout == null)
                    dynamicDeckMenuFlyout = Resources["DynamicDeckContextMenu"] as MenuFlyout;
                dynamicDeckMenuFlyout.ShowAt(target, offset);
            }
            else
                deckMenuFlyout.ShowAt(target, offset);
        }

        private void ShowCollectionContextMenu(UIElement target, Point offset)
        {
            if(collectionMenuFlyout == null)
                collectionMenuFlyout = Resources["CollectionContextMenu"] as MenuFlyout;

            collectionMenuFlyout.ShowAt(target, offset);
        }

        private void RenameMenuFlyoutItemClickHandler(object sender, RoutedEventArgs e)
        {
            if (!IsAppearOnDeckItem())
                return;

            if (renameFlyout == null)
            {
                renameFlyout = new NameEnterFlyout();
                renameFlyout.OkButtonClickEvent += RenameFlyoutOKButtonClickHandler;
            }

            renameFlyout.Show(pointToShowFlyout, deckShowContextMenu.Name);
        }

        private void ShowFlyout(Flyout flyout)
        {
            currentShownFlyout = flyout;
            flyout.ShowAt(pointToShowFlyout);
        }

        private bool IsAppearOnDeckItem()
        {
            if (deckShowContextMenu == null)
                return false;
            return true;
        }

        private async void RenameFlyoutOKButtonClickHandler(object sender, RoutedEventArgs e)
        {
            await mainPage.CurrentDispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                var text = renameFlyout.NewName;
                if (String.IsNullOrWhiteSpace(text))
                    return;

                try
                {
                    var deck = collection.Deck.Get(deckShowContextMenu.Id);
                    collection.Deck.Rename(deck, text);
                    deckListViewModel.GetDeck(deckShowContextMenu.Id).Name = text;

                    collection.Deck.Save(deck);
                    collection.SaveAndCommitAsync();
                }
                catch (DeckRenameException ex)
                {
                    if (ex.Error == DeckRenameException.ErrorCode.ALREADY_EXISTS)
                    {
                        await UIHelper.ShowMessageDialog("A deck with the same name already exists!");                        
                        renameFlyout.Show(pointToShowFlyout);
                    }
                    else
                    {
                        await UIHelper.ShowMessageDialog("You cannot rename this deck!"); 
                    }
                }
                catch
                {
                    await UIHelper.ShowMessageDialog("Unexpected error!");                    
                }
            });
        }

        private void ExportMenuFlyoutItemClickHanlder(object sender, RoutedEventArgs e)
        {
            if (!IsAppearOnDeckItem())
                return;

            ShowFlyout(exportFlyout);
        }

        private async void ExportFolderPickerButtonClickHandler(object sender, RoutedEventArgs e)
        {
            await ChooseExportFolder();
        }

        private async Task ChooseExportFolder()
        {
            try
            {
                var folderPicker = new Windows.Storage.Pickers.FolderPicker();
                folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                folderPicker.FileTypeFilter.Add(".apkg");
                exportFolder = await folderPicker.PickSingleFolderAsync();
                if (exportFolder != null)
                {
                    Windows.Storage.AccessCache.StorageApplicationPermissions.
                    FutureAccessList.AddOrReplace("ExportFolderToken", exportFolder);
                    exportFlyoutTextBox.Text = exportFolder.Path;
                }
                ShowFlyout(exportFlyout);
            }
            catch
            {
                await UIHelper.ShowMessageDialog("Unable to open the specified folder. Please choose another folder or run in administrator mode.");
            }
        }

        private async void ExportOkButtonClickHandler(object sender, RoutedEventArgs e)
        {
            if (exportFolder == null)
                return;

            mainPage.IsCanNavigateBack = false;
            exportFlyout.Hide();

            ShowExportProgessDialogAsync();

            await ExportPackageAsync();
        }

        private void FlyoutCancelButtonClickHandler(object sender, RoutedEventArgs e)
        {
            currentShownFlyout.Hide();
        }

        private void ShowExportProgessDialogAsync()
        {
            progressDialog = new ProgressDialog();
            progressDialog.ProgressBarLabel = "This may take a while...";
            progressDialog.ShowInDeterminateStateNoStopAsync("Exporting");
        }

        public Task ExportPackageAsync()
        {
            var exporter = new AnkiPackageExporter(collection, deckShowContextMenu.Id);
            if (exportMediaCheckBox.IsChecked == null || exportMediaCheckBox.IsChecked == false)
                exporter.IncludeMedia = false;
            else
                exporter.IncludeMedia = true;

            if (exportScheduleCheckBox.IsChecked == null || exportScheduleCheckBox.IsChecked == false)
                exporter.IncludeSched = false;
            else
                exporter.IncludeSched = true;
            exporter.ExportFinishedEvent += ExporterExportFinishedEventHandler;

            var split = deckShowContextMenu.Name.Split(UIHelper.ILLEGAL_NAME_CHAR, StringSplitOptions.RemoveEmptyEntries);
            var fileName = String.Join("_", split) + ".apkg";            

            Task task = Task.Run(async () =>
            {
                await exporter.ExportInto(exportFolder, fileName);
            });
            return task;
        }

        private async void ExporterExportFinishedEventHandler(string message)
        {
            await mainPage.CurrentDispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                if (progressDialog != null)
                    progressDialog.Hide();
                MessageDialog dialog;
                if (message == "Successed")
                    dialog = new MessageDialog("Your deck has been exported successfully.", "Successed!");
                else
                    dialog = new MessageDialog(UIConst.EXPORT_FAILED, "Error!");

                mainPage.IsCanNavigateBack = true;
                await dialog.ShowAsync();                
            });
        }

        private void MenuFlyoutConfigureClickHandler(object sender, RoutedEventArgs e)
        {
            selectedDeckId = deckShowContextMenu.Id;
            configNameViewModel = new DeckConfigNameViewModel(collection, selectedDeckId);
            deckAllConfigsView.DataContext = configNameViewModel.Configs;
            if (MainPage.UserPrefs.IsHelpAlreadyShown(AllHelps.HELP_DECK_OPTION))            
                ShowFlyout(configureFlyout);
            else            
                ShowDeckOpTionTutorial();            
        }

        private void ShowDeckOpTionTutorial()
        {
            mainPage.InitAllHelpsIfNeeded();
            mainPage.AllHelps.HelpClose += AllHelpsClosedEventHandler;
            mainPage.AllHelps.DeckOptionHelpShown(null, null);                        
        }

        private void AllHelpsClosedEventHandler()
        {
            ShowFlyout(configureFlyout);
            mainPage.AllHelps.HelpClose -= AllHelpsClosedEventHandler;
        }

        private void ConfigureFlyoutEditButtonClick(object sender, RoutedEventArgs e)
        {
            IsCreatingNewConfig = false;
            PrepareAndNavigateToOptionsPage(e);
        }

        private void ConfigureFlyoutNewClickHandler(object sender, RoutedEventArgs e)
        {
            IsCreatingNewConfig = true;
            PrepareAndNavigateToOptionsPage(e);
        }

        private void PrepareAndNavigateToOptionsPage(RoutedEventArgs e)
        {
            var config = (e.OriginalSource as FrameworkElement).DataContext as DeckConfigName;
            selectedConfigId = config.Id;
            Frame.Navigate(typeof(DeckOptionsPage), this);
        }

        private void ConfigureFlyoutOkButtonClickHandler(object sender, RoutedEventArgs e)
        {
            configNameViewModel.SetDeckConfigToSelected();
            configureFlyout.Hide();
            
            //Need to update cardcount because user may have changed card limits
            deckListViewModel.UpdateCardCountForDeck(selectedDeckId);
            var children = collection.Deck.Children(selectedDeckId);
            deckListViewModel.UpdateCardCountMultiDecks(children.Values);
            var parent = collection.Deck.Parents(selectedDeckId);
            UpdateParentsCardCountIfNeeded(parent);

            UpdateNoticeText();
            collection.SaveAndCommitAsync();
        }        

        private async void ConfigureFlyoutDeleteButtonClick(object sender, RoutedEventArgs e)
        {
            string content = "Delete this preset will revert all decks use it to Default.\n" +
                             "Are you sure you want to continue?";
            bool isDelete = await ShowYesNoMessageDialog(content);

            if (isDelete)
            {
                var config = (e.OriginalSource as FrameworkElement).DataContext as DeckConfigName;                
                collection.Deck.RemoveConfiguration(config.Id);

                deckListViewModel.UpdateCardCountAllDecks();
                UpdateNoticeText();                
                collection.SaveAndCommit();
            }
        }

        private async void MenuFlyoutDeleteClickHandler(object sender, RoutedEventArgs e)
        {
            var deckId = deckShowContextMenu.Id;
            var cardCount = collection.CardCount(deckId);
            string content;
            Dictionary<string, long> childs = null;
            if (collection.Deck.IsDyn(deckId))
                content = "Delete this deck will return all its cards to the orignal deck.\n" +
                          "Continue?";
            else
            {
                childs = Collection.Deck.Children(deckId);
                if (childs.Count > 0)
                {
                    content = $"WARNING: This deck has sub-decks, delete it will also permanently remove all its child decks.\n" 
                              + "Are you sure you want to continue?";
                }
                else
                {
                    content = $"Delete this deck will permanently remove all its cards ({cardCount}), notes, and media files.\n" +
                              "Are you sure you want to continue?";
                }
            }

            bool isDelete = await ShowYesNoMessageDialog(content);
            if (isDelete)
            {
                if (!collection.Deck.IsDyn(deckId))
                    await MainPage.BackupDatabase();

                string savePoint = collection.Database.SaveTransactionPoint();
                try
                {
                    await DeleteDeckAsync(deckId, childs);
                }
                catch
                {
                    await UIHelper.ShowMessageDialog("Cannot delete this deck!", "Error!");
                    collection.Database.RollbackTo(savePoint);
                }
            }
        }

        private async Task DeleteDeckAsync(long deckId, Dictionary<string, long> childs = null)
        {
            var parents = collection.Deck.Parents(deckId);
            var originalDeckId = collection.Deck.TryGetOriginalDeckId(deckId);

            ProgressDialog dialog = new ProgressDialog();
            dialog.ProgressBarLabel = "This may take a while...";
            dialog.ShowInDeterminateStateNoStopAsync("Deleting deck");
            collection.Deck.Remove(deckId, true, true);
            await RemoveMediaAndView(deckId);

            await DeleteChildrenIfNeeded(childs);            
            UpdateOriginalDeckCardCountIfNeeded(originalDeckId);
            UpdateParentsCardCountIfNeeded(parents);

            UpdateNoticeText();
            dialog.Hide();
            collection.Save();
            collection.Database.Commit();

            MainPage.RemoveDeckInPrefsIfNeeded(deckId);
        }

        private void UpdateParentsCardCountIfNeeded(List<Windows.Data.Json.JsonObject> parents)
        {
            if (parents != null)
            {
                foreach (var deck in parents)
                {
                    long id = (long)deck.GetNamedNumber("id");
                    deckListViewModel.UpdateCardCountForDeck(id);
                }
            }
        }

        public void UpdateOriginalDeckCardCountIfNeeded(long? originalId)
        {            
            if (originalId == null)
                return;

            var deckId = (long)originalId;

            deckListViewModel.UpdateCardCountForDeck(deckId);
            var parents = collection.Deck.Parents(deckId);
            UpdateParentsCardCountIfNeeded(parents);            
        }

        private async Task DeleteChildrenIfNeeded(Dictionary<string, long> childs)
        {
            if (childs != null)
                foreach (var deck in childs)
                {
                    await RemoveMediaAndView(deck.Value);
                    MainPage.RemoveDeckInPrefsIfNeeded(deck.Value);
                }
        }

        private async Task RemoveMediaAndView(long deckId)
        {
            await collection.Media.RemoveDeckMediaFolderAsync(deckId);
            await deckListViewModel.RemoveDeck(deckId);
        }

        private static async Task<bool> ShowYesNoMessageDialog(string content)
        {
            var messageDialog = new MessageDialog(content);

            bool isDelete = false;
            messageDialog.Commands.Add(new UICommand("Yes", (command) =>
            {
                isDelete = true;
            }));
            messageDialog.Commands.Add(new UICommand("No", (command) =>
            {
                isDelete = false;
            }));
            messageDialog.DefaultCommandIndex = 1;
            await messageDialog.ShowAsync();
            return isDelete;
        }

        //NOTUSED
        private async void MenuFlyoutDefaultImageItemClickHandler(object sender, RoutedEventArgs e)
        {
            await deckShowContextMenu.ChangeBackToDefaultImage();
        }

        private async void MenuFlyoutChangeImageItemClickHandler(object sender, RoutedEventArgs e)
        {
            var image = await UIHelper.OpenFilePicker("ImageFolderToken", ".png", ".jpg", ".bmp", ".jpeg");
            if (image != null)
            {
                await deckShowContextMenu.ChangeImage(image);
            }
        }

        private void AddNoteMenuFlyoutItemClickHandler(object sender, RoutedEventArgs e)
        {
            collection.Deck.Select(deckShowContextMenu.Id, false);
            NoteEditorPageParameter param = new NoteEditorPageParameter() { CurrentNote = null, Mainpage = mainPage };
            Frame.Navigate(typeof(NoteEditor), param);
        }

        private void SeachCardMenuClickHandler(object sender, RoutedEventArgs e)
        {
            mainPage.Collection.Deck.Select(deckShowContextMenu.Id, false);
            Frame.Navigate(typeof(SearchPage), mainPage);
        }

        public void ToggleReadMode()
        {
            isNightMode = !isNightMode;
            UIHelper.ToggleNightLight(isNightMode, this);
            ChangeBackgroundColor();
        }

        private void ChangeBackgroundColor()
        {
            if (isNightMode)
            {
                mainGrid.Background = new SolidColorBrush(Colors.Black);
            }
            else
            {
                mainGrid.Background = new SolidColorBrush(Windows.UI.Colors.White);
            }
            if (helpPopup != null)
                helpPopup.ChangeReadMode(isNightMode);
            if (customStudyFlyout != null)
                customStudyFlyout.ChangeReadMode(isNightMode);
        }

        private void StatsMenuFlyoutItemClick(object sender, RoutedEventArgs e)
        {
            collection.Deck.Select(deckShowContextMenu.Id, false);
            Stats.IsWholeCollection = false;
            Frame.Navigate(typeof(StatsPage), mainPage);
        }

        private void NoticeBoardExpandButtonClick(object sender, RoutedEventArgs e)
        {
            if (noticeBoardRoot.Visibility == Visibility.Collapsed)
            {
                OpenNoticeBoard();
            }
            else
            {
                CloseNoticeBoard();
            }
        }

        private void CloseNoticeBoard()
        {
            noticeBoardRoot.Visibility = Visibility.Collapsed;
            expandSymbolRotation.Rotation = 0;
            isNoticeBoardClosed = true;
        }

        private void OpenNoticeBoard()
        {
            noticeBoardRoot.Visibility = Visibility.Visible;
            expandSymbolRotation.Rotation = 180;
            isNoticeBoardClosed = false;
        }

        private void SortByDateAddedClick(object sender, RoutedEventArgs e)
        {
            deckListViewModel.SortByDateAdded();
        }

        private void SortByNameClick(object sender, RoutedEventArgs e)
        {
            deckListViewModel.SortByName();
        }

        private void EnterTutorialModeIfNeeded()
        {
            if (AllHelps.Tutorial == AllHelps.TutorialState.NotShow)
                return;

            if (AllHelps.Tutorial == AllHelps.TutorialState.DeckCreation)
            {
                DeckCreationTutorialSetup();
            }
            else if(AllHelps.Tutorial == AllHelps.TutorialState.ViewCard)
            {
                CardViewTutorialSetup();
            }
            else if(AllHelps.Tutorial == AllHelps.TutorialState.SharedDeck)
            {
                SharedDeckTutorialSetup();
            }  
        }

        private void DeckCreationTutorialSetup()
        {
            CloseNoticeBoard();
            mainPage.SplitViewToggleButton.IsEnabled = false;
            mainPage.AddButton.Click += TutorialAddButtonClickHandler;
            NewDeckCreatedEvent += TutorialNewDeckCreatedEvent;
            helpPopup = new HelpPopup();
            UIHelper.AddToGridInFull(mainGrid, helpPopup);
            helpPopup.Title = "Create a Deck";
            helpPopup.SubTitle = "(A place to store your cards)";
            helpPopup.Text = "To create a deck please press on the \"Add\" icon."
                             + " Choose any names you like and use \"Basic\" type.";
            helpPopup.SetOffSet(0, DEFAULT_HELP_POPUP_VERTICAL_OFFSET);
            helpPopup.Show();
            mainPage.NoticeMe.Begin();
        }

        private void CardViewTutorialSetup()
        {
            mainPage.SplitViewToggleButton.IsEnabled = false;
            helpPopup = new HelpPopup();
            UIHelper.AddToGridInFull(mainGrid, helpPopup);
            helpPopup.Title = "View your card";
            helpPopup.SubTitle = "(or \"learning\" for short)";
            helpPopup.Text = "Please press on your deck to start viewing your cards.";
            helpPopup.SetOffSet(0, DEFAULT_HELP_POPUP_VERTICAL_OFFSET);
            helpPopup.ShowWithClose();
        }

        private void SharedDeckTutorialSetup()
        {
            mainPage.SplitViewToggleButton.IsEnabled = true;
            mainPage.SplitViewToggleButton.Click += TutorialSplitViewToggleButtonClick;
            helpPopup = new HelpPopup();
            UIHelper.AddToGridInFull(mainGrid, helpPopup);
            helpPopup.Title = "Shared Decks";
            helpPopup.SubTitle = "(Sharing is caring)";
            helpPopup.Text = "To download and import decks created by others, please press on the button at the top-left corner.\n"
                             + "(we are not responsible for these contents.)";
            helpPopup.SetOffSet(0, DEFAULT_HELP_POPUP_VERTICAL_OFFSET);
            mainPage.NoticeMe.Stop();
            UIHelper.SetStoryBoardTarget(mainPage.BlinkingBlue, mainPage.SplitViewToggleButton.Name);
            mainPage.NoticeMe.Begin();
            helpPopup.Show();
        }         

        private void TutorialSplitViewToggleButtonClick(object sender, RoutedEventArgs e)
        {
            helpPopup.Hide();            
            mainPage.SplitViewToggleButton.Click -= TutorialSplitViewToggleButtonClick;
            mainPage.NoticeMe.Stop();
            
            helpPopup.Title = "Finished";
            helpPopup.SubtitleVisibility = Visibility.Collapsed;
            helpPopup.Text = "This concludes \"Add Decks & Notes\" tutorial. You can try out other note types to see how they work.\n"
                             + "When you are ready (and in a good mood), please view \"Note Types & Templates\" tutorial.";
            helpPopup.ShowWithClose();

            AllHelps.Tutorial = AllHelps.TutorialState.NotShow;
            MainPage.UserPrefs.SetHelpShown(AllHelps.HELP_DECK_NOTE, true);
        }

        private void TutorialNewDeckCreatedEvent(long deckId)
        {
            mainPage.AddButton.Click -= TutorialAddButtonClickHandler;
            NewDeckCreatedEvent -= TutorialNewDeckCreatedEvent;
            AllHelps.Tutorial = AllHelps.TutorialState.AddNote;            
            helpPopup.Title = "Add a Note";
            helpPopup.SubTitle = "(Start adding contents to your cards.)";
            helpPopup.Text = "To add a note please right-click (or touch & hold) on your deck then choose \"Add Notes\".";            
            helpPopup.ShowWithClose();
        }

        private void TutorialAddButtonClickHandler(object sender, RoutedEventArgs e)
        {
            mainPage.NoticeMe.Stop();
            helpPopup.Hide();
        }

        private void CloseNoticeBoardIfNeeded()
        {
            if (isNoticeBoardClosed)
                CloseNoticeBoard();
        }

        private void NewDeckFlyoutClosedWithoutCreatingDeckHandler()
        {
            if(helpPopup != null && AllHelps.Tutorial == AllHelps.TutorialState.DeckCreation)
            {
                helpPopup.Show();
            }
        }
    }
}
