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
using Windows.UI.StartScreen;
using Shared;
using Windows.ApplicationModel.Background;

namespace AnkiU.Pages
{
    public sealed partial class DeckSelectPage : Page, INightReadMode
    {
        private const int DEFAULT_HELP_POPUP_VERTICAL_OFFSET = 50;
        private const double DEFAULT_OPACITY = 0.8;
        private const double REFRESH_RATE = 1;

        private App app;

        private IAnkiDecksView decksView;
        private DeckListViewModel deckListViewModel;

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
            app = App.Current as App;

            collection = mainPage.Collection;
            GetDecksInformation();
            EnterTutorialModeIfNeeded();

            HookAllEvents();
            ShowAllButtonOfThisPage();

            var task = deckListViewModel.UpdateAllSecondaryTilesIfHas();

        }

        private void HookAllEvents()
        {
            mainPage.EnableChangingReadMode(this);
            ChangeBackgroundColor();
            mainPage.DeckImageChangedEvent += OnDeckImageChanged;
            Window.Current.VisibilityChanged += CurrentWindowVisibilityChangedHandler;
            mainPage.GridViewButton.Click += GridViewButtonClickHandler;
            mainPage.ListViewButton.Click += ListViewButtonClickHandler;
            mainPage.AddButton.Click += AddButtonClickHandler;
            mainPage.DragAndDropButton.Click += DragAndDropButtonClick;
            mainPage.CommanBar.Opening += OnCommanBarOpening;
            mainPage.DeckChanged += OnMainPageDeckChanged;
            if (app != null)
                app.AppLaunchFromtTile += OnAppLaunchFromtTile;
        }

        private async Task DelayNavigateToOtherPage()
        {
            await Task.Delay(50);
            HandleLiveTileInteraction(app);
        }

        private void OnAppLaunchFromtTile(object sender, RoutedEventArgs e)
        {
            HandleLiveTileInteraction(app);
        }

        private void HandleLiveTileInteraction(App app)
        {
            if (app != null && app.TileId != null)
            {
                var deck = deckListViewModel.GetDeck((long)app.TileId);
                app.ClearTileId();
                if (deck != null)
                {
                    if (deck.NewCards + deck.DueCards > 0)
                        NavigateToReviewPage(deck);
                    else
                        NavigateToNoteEditorPage(deck.Id);
                }
            }
        }

        private void OnCommanBarOpening(object sender, object e)
        {
            if (customStudyFlyout != null && customStudyFlyout.IsOpen)
                customStudyFlyout.Hide();
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

        private async void OnDeckDragAnDrop(DeckInformation parent, DeckInformation child)
        {
            try
            {
                deckListViewModel.DragAnDrop(parent, child, decksView);
            }
            catch (DeckRenameException ex)
            {
                if (ex.Error == DeckRenameException.ErrorCode.ALREADY_EXISTS)
                {
                    await UIHelper.ShowMessageDialog("A deck with the same name already exists.");
                    return;
                }
                if (ex.Error == DeckRenameException.ErrorCode.FILTERED_NOSUBDEKCS)
                {
                    await UIHelper.ShowMessageDialog("A \"Custom Study Deck\" can't become a parent deck.");
                    return;
                }
            }
            catch
            {
                await UIHelper.ShowMessageDialog("Unexpected error.");
                return;
            }
        }

        private async void OnDeckImageChanged(StorageFile fileToChange, long deckId, long modifiedTime)
        {
            try
            {
                var deckInfor = deckListViewModel.GetDeck(deckId);
                await deckInfor.ChangeImage(fileToChange, modifiedTime);
            }
            catch
            {
                await UIHelper.ShowMessageDialog("Unable to change deck image!\n");
            }
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
                var deckInfor = deckListViewModel.GetDeck(deckId);
                deckListViewModel.ResortNonSubdeck(deckInfor);
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
                var isModified = collection.IsModified();
                deckListViewModel.UpdateCardCountAllDecks();
                lastRefreshDate = currentDate;

                //Make sure we don't accidentally bump this up
                if (!isModified)
                    collection.ClearIsModified();
            }
        }

        private void SaveSession()
        {
            collection.SaveAndCommit();
        }

        private void ShowAllButtonOfThisPage()
        {
            mainPage.DragAndDropButton.Visibility = Visibility.Visible;
            mainPage.RootSplitView.IsPaneToggleButtonVisible = true;
            mainPage.HelpSplitView.Visibility = Visibility.Visible;
            mainPage.AddButton.Visibility = Visibility.Visible;
            mainPage.SyncButton.Visibility = Visibility.Visible;

            if (MainPage.UserPrefs.IsDeckListView)
                SwitchToListView();
            else
                SwitchToGridView();
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            if (decksView.IsDragAndDropEnable)
                ReturnToDeckSelection();

            HideAllButtonOfThisPage();
            UnHookAllEvents();

            base.OnNavigatingFrom(e);
        }

        private void HideAllButtonOfThisPage()
        {
            mainPage.DragAndDropButton.Visibility = Visibility.Collapsed;
            mainPage.RootSplitView.IsPaneToggleButtonVisible = false;
            mainPage.HelpSplitView.Visibility = Visibility.Collapsed;
            mainPage.AddButton.Visibility = Visibility.Collapsed;
            mainPage.SyncButton.Visibility = Visibility.Collapsed;
            if (MainPage.UserPrefs.IsDeckListView)
                mainPage.GridViewButton.Visibility = Visibility.Collapsed;
            else
                mainPage.ListViewButton.Visibility = Visibility.Collapsed;
        }

        private void UnHookAllEvents()
        {
            mainPage.DisableChangingReadMode();
            mainPage.DeckImageChangedEvent -= OnDeckImageChanged;
            Window.Current.VisibilityChanged -= CurrentWindowVisibilityChangedHandler;
            mainPage.GridViewButton.Click -= GridViewButtonClickHandler;
            mainPage.ListViewButton.Click -= ListViewButtonClickHandler;
            mainPage.AddButton.Click -= AddButtonClickHandler;
            mainPage.DragAndDropButton.Click -= DragAndDropButtonClick;
            mainPage.CommanBar.Opening -= OnCommanBarOpening;
            mainPage.DeckChanged -= OnMainPageDeckChanged;

            if (app != null)
                app.AppLaunchFromtTile -= OnAppLaunchFromtTile;
        }

        private void ListViewButtonClickHandler(object sender, RoutedEventArgs e)
        {
            SwitchToListView();
        }

        private void GridViewButtonClickHandler(object sender, RoutedEventArgs e)
        {
            SwitchToGridView();
        }

        private void SwitchToGridView()
        {
            MainPage.UserPrefs.IsDeckListView = false;
            if (deckListView != null)
            {
                deckListView.Visibility = Visibility.Collapsed;
                deckListView.DataContext = null;
            }
            if (deckGridView == null)
            {
                this.FindName("deckGridView");
                decksView = deckGridView as IAnkiDecksView;
                HookDeckItemEvent();
            }
            decksView = deckGridView;
            decksView.DataContext = deckListViewModel.Decks;
            mainPage.ListViewButton.Visibility = Visibility.Visible;
            mainPage.GridViewButton.Visibility = Visibility.Collapsed;

            deckListViewModel.ShowAllDecks(decksView);
            deckGridView.Visibility = Visibility.Visible;
        }

        private void SwitchToListView()
        {
            MainPage.UserPrefs.IsDeckListView = true;
            if (deckGridView != null)
            {
                deckGridView.Visibility = Visibility.Collapsed;
                deckGridView.DataContext = null;
            }
            if (deckListView == null)
            {
                this.FindName("deckListView");
                decksView = deckListView as IAnkiDecksView;
                HookDeckItemEvent();
            }
            decksView = deckListView;
            decksView.DataContext = deckListViewModel.Decks;
            mainPage.ListViewButton.Visibility = Visibility.Collapsed;
            mainPage.GridViewButton.Visibility = Visibility.Visible;

            deckListViewModel.ShowAllDecks(decksView);
            deckListView.Visibility = Visibility.Visible;

            FoldAllSubDecksOnListView();
        }

        private void HookDeckItemEvent()
        {
            decksView.DeckItemClickEvent += DeckListViewItemClickEventHandler;
            decksView.DragAnDropEvent += OnDeckDragAnDrop;
            decksView.ExpandChildrenClickEvent += OnExpandChildrenClick;
            decksView.ContextMenuClickEvent += OnDecksViewContextMenuClickEvent;
        }

        private void DeckListViewItemClickEventHandler(DeckInformation deck)
        {
            NavigateToReviewPage(deck);
        }

        private void NavigateToReviewPage(DeckInformation deck)
        {
            mainPage.Collection.Deck.Select(deck.Id, false);
            if (deck.NewCards > 0 || deck.DueCards > 0)
            {
                MakesureCleanState();
                mainPage.Collection.Sched.Reset();
                Frame.Navigate(typeof(ReviewPage), mainPage);
            }
            else if (!collection.Deck.IsDyn(deck.Id))
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

        private void OnExpandChildrenClick(DeckInformation parent)
        {
            deckListViewModel.ToggleChildrenVisibility(parent, decksView);
        }

        private void InitCustomStudyFlyout()
        {
            customStudyFlyout = new CustomStudyFlyout(mainPage.CurrentDispatcher, mainGrid);
            customStudyFlyout.CustomStudyCreateEvent += CustomStudyCreateEventHandler;
            UIHelper.AddToGridInFull(mainGrid, customStudyFlyout);
        }

        private void CustomStudyCreateEventHandler(CustomStudyFlyout.CustomStudyOption studyOption, long originalDeckID, long dynamicDeckId)
        {
            if (studyOption == CustomStudyFlyout.CustomStudyOption.IncreaseNewToDay ||
                studyOption == CustomStudyFlyout.CustomStudyOption.IncreaseReviewToDay)
            {
                deckListViewModel.UpdateCardCountForDeck(originalDeckID);
            }
            else
            {
                deckListViewModel.UpdateCardCountForDeck(originalDeckID);
                deckListViewModel.AddOrUpdateDeckCardCount(dynamicDeckId);
                var deckInfor = deckListViewModel.GetDeck(dynamicDeckId);
                deckListViewModel.ResortNonSubdeck(deckInfor);
            }
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
            FoldAllSubDecksOnListView();
        }

        private void FoldAllSubDecksOnListView()
        {
            try
            {
                if (decksView is DeckListView)
                    deckListViewModel.FoldAllChildrenDecks(decksView);
            }
            catch
            {

            }
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
            if (collectionMenuFlyout == null)
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

            renameFlyout.Show(pointToShowFlyout, deckShowContextMenu.BaseName);
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
                var baseName = renameFlyout.NewName;
                if (String.IsNullOrWhiteSpace(baseName))
                    return;

                if (baseName.Equals("Default", StringComparison.OrdinalIgnoreCase) && deckShowContextMenu.Id != Constant.DEFAULTDECK_ID)
                {
                    await UIHelper.ShowMessageDialog("You can't name a non-default deck \"Default\"");
                    return;
                }

                var existingName = deckListViewModel.GetAllDeckBaseName();
                if (existingName.Contains(baseName))
                {
                    await NotifyNameAlreadyExist();
                    return;
                }

                try
                {
                    var newName = deckListViewModel.GetNewFullName(deckShowContextMenu, baseName);
                    var deck = collection.Deck.Get(deckShowContextMenu.Id);
                    collection.Deck.Rename(deck, newName);
                    deckListViewModel.UpdateDeckName(deckShowContextMenu);

                    collection.Deck.Save(deck);
                    collection.SaveAndCommitAsync();

                    var tile = await TilesHelper.FindExisting(deckShowContextMenu.Id.ToString());
                    if (tile != null)
                    {
                        tile.DisplayName = newName;
                        await tile.UpdateAsync();
                    }

                }
                catch (DeckRenameException ex)
                {
                    if (ex.Error == DeckRenameException.ErrorCode.ALREADY_EXISTS)
                    {
                        await NotifyNameAlreadyExist();
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

        private async Task NotifyNameAlreadyExist()
        {
            await UIHelper.ShowMessageDialog("A deck with the same name already exists!");
            renameFlyout.Show(pointToShowFlyout);
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
                    dialog = new MessageDialog(UIConst.EXPORT_FAILED + "\n" + message, "Error!");

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
            mainPage.AllHelps.HelpClose += AllHelpsClosedEventHandler;
            mainPage.AllHelps.ShowDeckOptionHelp(null, null);
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

            collection.SaveAndCommitAsync();
        }

        private async void ConfigureFlyoutDeleteButtonClick(object sender, RoutedEventArgs e)
        {
            var isContinue = await MainPage.WarnFullSyncIfNeeded();
            if (!isContinue)
                return;

            string content = "Delete this preset will revert all decks using it to Default.\n" +
                             "Are you sure you want to continue?";
            bool isDelete = await UIHelper.AskUserConfirmation(content);

            if (isDelete)
            {
                var config = (e.OriginalSource as FrameworkElement).DataContext as DeckConfigName;
                ReorderCardsIfRandom(config);
                collection.Deck.RemoveConfiguration(config.Id);
                deckListViewModel.UpdateCardCountAllDecks();
                collection.SaveAndCommit();
            }
        }

        private void ReorderCardsIfRandom(DeckConfigName config)
        {
            var newConf = collection.Deck.GetConf(config.Id).GetNamedObject("new");
            var order = (int)JsonHelper.GetNameNumber(newConf, "order");
            if (order == (int)NewCardInsertOrder.RANDOM)
            {
                var deckIds = collection.Deck.DeckIdsForConf(config.Id);
                foreach (var id in deckIds)
                    collection.Sched.OrderCards(id);
            }
        }

        private async void MenuFlyoutDeleteClickHandler(object sender, RoutedEventArgs e)
        {
            var deckId = deckShowContextMenu.Id;
            if (deckId == Constant.DEFAULTDECK_ID)
            {
                await UIHelper.ShowMessageDialog("Default deck cannot be deleted.");
                return;
            }

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

            bool isDelete = await UIHelper.AskUserConfirmation(content);
            if (isDelete)
            {
                if (!collection.Deck.IsDyn(deckId))
                    await MainPage.BackupDatabase();

                string savePoint = collection.Database.SaveTransactionPoint();
                try
                {
                    DeleteDeckAsync(deckId, childs);
                }
                catch
                {
                    await UIHelper.ShowMessageDialog("Cannot delete this deck!", "Error!");
                    collection.Database.RollbackTo(savePoint);
                }
            }
        }

        private void DeleteDeckAsync(long deckId, Dictionary<string, long> childs = null)
        {
            ProgressDialog dialog = new ProgressDialog();
            dialog.ProgressBarLabel = "This may take a while...";
            dialog.ShowInDeterminateStateNoStopAsync("Deleting deck");

            Task.Run(async () =>
           {//Run these in async to avoid blocking UI
                var parents = collection.Deck.Parents(deckId);
               var originalDeckId = collection.Deck.TryGetOriginalDeckId(deckId);

               collection.Deck.Remove(deckId, true, true);

               await mainPage.CurrentDispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
               {
                   await RemoveMediaAndView(deckId);

                   await DeleteChildrenIfNeeded(childs);
                   UpdateOriginalDeckCardCountIfNeeded(originalDeckId);
                   UpdateParentsCardCountIfNeeded(parents);

                   dialog.Hide();
                   collection.Save();
                   collection.Database.Commit();

                   MainPage.RemoveDeckPrefsIfNeeded(deckId);
               });
           });
        }

        private void UpdateParentsCardCountIfNeeded(List<Windows.Data.Json.JsonObject> parents)
        {
            if (parents != null)
            {
                foreach (var deck in parents)
                {
                    long id = (long)JsonHelper.GetNameNumber(deck, "id");
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
                    if (deck.Value == Constant.DEFAULTDECK_ID)
                    {
                        var defaultDeck = deckListViewModel.GetDeck(Constant.DEFAULTDECK_ID);
                        deckListViewModel.UpdateDeckName(defaultDeck);
                        continue;
                    }
                    await RemoveMediaAndView(deck.Value);
                    MainPage.RemoveDeckPrefsIfNeeded(deck.Value);
                }
        }

        private async Task RemoveMediaAndView(long deckId)
        {
            await collection.Media.RemoveDeckMediaFolderAsync(deckId);
            await deckListViewModel.RemoveDeck(deckId);
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
            NavigateToNoteEditorPage(deckShowContextMenu.Id);
        }

        private void NavigateToNoteEditorPage(long id)
        {
            collection.Deck.Select(id, false);
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
                mainGrid.Background = new SolidColorBrush(Colors.White);
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

        private void SortByDateAddedClick(object sender, RoutedEventArgs e)
        {            
            deckListViewModel.UnFoldAllChildrenDecks(decksView);
            deckListViewModel.SortByDateAdded();
        }

        private void SortByNameClick(object sender, RoutedEventArgs e)
        {
            deckListViewModel.UnFoldAllChildrenDecks(decksView);
            deckListViewModel.SortByName();
        }

        private void OnMainPageDeckChanged(long deckId)
        {
            deckListViewModel.AddOrUpdateDeckCardCount(deckId);
            var parent = collection.Deck.Parents(deckId);
            UpdateParentsCardCountIfNeeded(parent);
        }

        private void EnterTutorialModeIfNeeded()
        {
            if (AllHelps.Tutorial == AllHelps.TutorialState.NotShow)
                return;

            if (AllHelps.Tutorial == AllHelps.TutorialState.DeckCreation)
            {
                DeckCreationTutorialSetup();
            }
            else if (AllHelps.Tutorial == AllHelps.TutorialState.ViewCard)
            {
                CardViewTutorialSetup();
            }
            else if (AllHelps.Tutorial == AllHelps.TutorialState.SharedDeck)
            {
                SharedDeckTutorialSetup();
            }
        }

        private void DeckCreationTutorialSetup()
        {
            mainPage.AddButton.Click += TutorialAddButtonClickHandler;
            NewDeckCreatedEvent += TutorialNewDeckCreatedEvent;
            helpPopup = new HelpPopup();
            UIHelper.AddToGridInFull(mainGrid, helpPopup);
            helpPopup.Title = "Create a Deck";
            helpPopup.SubTitle = "(A place to store your cards)";
            helpPopup.Text = "To create a deck please press on the \"Add\" icon."
                             + " Choose any names you like and use \"Basic\" type." 
                             + " Leave note type name blank if you want to reuse an old note type.";
            helpPopup.SetOffSet(0, DEFAULT_HELP_POPUP_VERTICAL_OFFSET);
            helpPopup.Show();
            mainPage.NoticeMe.Begin();
        }

        private void CardViewTutorialSetup()
        {
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
            helpPopup = new HelpPopup();
            UIHelper.AddToGridInFull(mainGrid, helpPopup);
            helpPopup.Title = "Shared Decks";
            helpPopup.SubtitleVisibility = Visibility.Collapsed;
            helpPopup.Text = "To download and import decks created by others, please press on the button at the top-left corner.\n"
                             + "(we are not responsible for these contents.)";
            helpPopup.SetOffSet(0, DEFAULT_HELP_POPUP_VERTICAL_OFFSET);
            mainPage.NoticeMe.Stop();
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

        private void NewDeckFlyoutClosedWithoutCreatingDeckHandler()
        {
            if (helpPopup != null && AllHelps.Tutorial == AllHelps.TutorialState.DeckCreation)
            {
                helpPopup.Show();
            }
        }

        private async void OnMenuFlyoutCreateDeckTileClick(object sender, RoutedEventArgs e)
        {
            base.IsEnabled = false;

            var tileId = deckShowContextMenu.Id.ToString();

            SecondaryTile tile = TilesHelper.GenerateSecondaryTile(tileId, deckShowContextMenu.BaseName);
            tile.VisualElements.ShowNameOnSquare150x150Logo = true;
            tile.VisualElements.ShowNameOnSquare310x310Logo = true;
            tile.VisualElements.ShowNameOnWide310x150Logo = true;
            tile.VisualElements.BackgroundColor = DeckListViewModel.GetColors(deckShowContextMenu);

            var isSuccess = await tile.RequestCreateAsync();
            base.IsEnabled = true;
            if (!isSuccess)
                return;


            TilesHelper.SendSecondaryTileNotification(tileId, deckShowContextMenu.NewCards.ToString(), deckShowContextMenu.DueCards.ToString());
        }

        private void OnDecksViewContextMenuClickEvent(object sender, RoutedEventArgs e)
        {            
            FrameworkElement button = sender as FrameworkElement;
            if (button == null)
                return;
            var deckInfor = button.DataContext as DeckInformation;
            if (deckInfor == null)
                return;

            ShowContextMenu(deckInfor, button, new Point(0, 0));
        }

        private void OnCustomStudyMenuFlyoutItemClick(object sender, RoutedEventArgs e)
        {
            if (!collection.Deck.IsDyn(deckShowContextMenu.Id))
            {
                mainPage.Collection.Deck.Select(deckShowContextMenu.Id, false);
                if (customStudyFlyout == null)
                    InitCustomStudyFlyout();

                customStudyFlyout.InitDeckValue(collection, isNightMode);
                customStudyFlyout.Show();
            }
        }

        private void OnMainGridTapped(object sender, TappedRoutedEventArgs e)
        {
            var pointerPosition = e.GetPosition(mainGrid);
            pointToShowFlyout.Margin = new Thickness(pointerPosition.X, pointerPosition.Y, 0, 0);
        }
    }
}
