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

using AnkiU.Anki.Syncer;
using AnkiU.AnkiCore;
using AnkiU.Interfaces;
using AnkiU.UIUtilities;
using AnkiU.UserControls;
using AnkiU.ViewModels;
using AnkiU.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.System;
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
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingPage : Page, INightReadMode
    {
        public const int SYNC_ONEDRIVE = 0;
        public const int SYNC_ANKIWEB = 1;

        private bool isNightMode;        

        private MainPage mainPage;        

        private CollectionOptionViewModel collectionOptionViewModel;

        public SettingPage()
        {
            this.InitializeComponent();
            numberOfBackup.LostFocus += NumberOfBackupLostFocus;
        }

        private async void NumberOfBackupLostFocus(object sender, RoutedEventArgs e)
        {
            if (numberOfBackup.Number < 5)
            {
                bool isOk = await UIHelper.AskUserConfirmation
                                  ("Are you sure you only want to keep " + numberOfBackup.Number + " backup(s)?\n" +
                                   "(With a collection of 5000 cards and 2 years of learning, one backup will normally cost 4MB)");
                if (!isOk)
                {
                    numberOfBackup.Number = 15;
                }
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            mainPage = e.Parameter as MainPage;            

            collectionOptionViewModel = new CollectionOptionViewModel(mainPage.Collection.Conf);
            collectionOptionView.ViewModel = collectionOptionViewModel;            
            numberOfBackup.Number = MainPage.UserPrefs.NumberOfBackups;
            backupTime.Number = MainPage.UserPrefs.BackupsMinTime;
            syncOnOpenCheckBox.IsChecked = MainPage.UserPrefs.IsSyncOnOpen;
            syncOnCloseCheckBox.IsChecked = MainPage.UserPrefs.IsSyncOnClose;
            syncMediaCheckBox.IsChecked = MainPage.UserPrefs.IsSyncMedia;
            if (!UIHelper.IsDeskTop())
                openBackUpFolderButton.Visibility = Visibility.Collapsed;

            syncServiceCombobox.SelectedIndex = MainPage.UserPrefs.SyncService;
            if (MainPage.UserPrefs.SyncService == SYNC_ANKIWEB)
            {
                DisableMediaSync();
                ChangeAnkiWebButtonVisibility(Visibility.Visible);                
            }
            syncServiceCombobox.SelectionChanged += OnSyncServiceSelectionChanged; //Hook here to avoid start up problems     
            saveShortcutCheckBox.IsChecked = MainPage.UserPrefs.IsChangedSaveShortcutOpen;
            HookAllEvents();
        }

        private void HookAllEvents()
        {
            mainPage.SaveButton.Visibility = Visibility.Visible;
            mainPage.SaveButton.Click += SaveButtonClickHandler;
            mainPage.EnableChangingReadMode(this);
            ChangeBackgroundColor();
            CoreWindow.GetForCurrentThread().KeyDown += SettingPageKeyDownHandler;
        }

        private async void SettingPageKeyDownHandler(CoreWindow sender, KeyEventArgs args)
        {
            await mainPage.CurrentDispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                var control = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control);
                if (control.HasFlag(CoreVirtualKeyStates.Down) && args.VirtualKey == VirtualKey.S)
                {
                    mainPage.SaveButtonClickAnimateAsync();
                    SaveButtonClickHandler(null, null);                    
                }
            });
        }

        private void SaveButtonClickHandler(object sender, RoutedEventArgs e)
        {
            SaveGeneralUserPref();
            SaveCollectionPrefs();
        }

        private void SaveGeneralUserPref()
        {
            MainPage.UserPrefs.NumberOfBackups = numberOfBackup.Number;
            MainPage.UserPrefs.BackupsMinTime = backupTime.Number;
            MainPage.UserPrefs.IsSyncMedia = (bool)syncMediaCheckBox.IsChecked;
            MainPage.UserPrefs.IsSyncOnOpen = (bool)syncOnOpenCheckBox.IsChecked;
            MainPage.UserPrefs.IsSyncOnClose = (bool)syncOnCloseCheckBox.IsChecked;
            MainPage.UserPrefs.SyncService = syncServiceCombobox.SelectedIndex;
            MainPage.UserPrefs.IsChangedSaveShortcutOpen = (bool)saveShortcutCheckBox.IsChecked;
            mainPage.UpdateUserPreference();
        }

        private void SaveCollectionPrefs()
        {
            if (collectionOptionViewModel.IsModified())
            {
                collectionOptionViewModel.SaveOptions();
                mainPage.Collection.SetIsModified();
                mainPage.Collection.SaveAndCommitAsync();                
            }
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);
            SaveButtonClickHandler(null, null);
            UnHookAllEvents();
        }

        private void UnHookAllEvents()
        {
            mainPage.SaveButton.Visibility = Visibility.Collapsed;
            mainPage.SaveButton.Click -= SaveButtonClickHandler;
            mainPage.DisableChangingReadMode();
            CoreWindow.GetForCurrentThread().KeyDown -= SettingPageKeyDownHandler;
        }

        public void ToggleReadMode()
        {
            isNightMode = !isNightMode;
            ChangeBackgroundColor();
        }

        private void ChangeBackgroundColor()
        {
            UIHelper.ToggleNightLight(isNightMode, this);
            if (isNightMode)
            {
                learningTab.Background = new SolidColorBrush(Windows.UI.Colors.Black);
            }
            else
            {
                learningTab.Background = new SolidColorBrush(Windows.UI.Colors.White);
            }
        }

        private async void OpenBackUpFolderClick(object sender, RoutedEventArgs e)
        {
            StorageFolder folder = await Storage.AppLocalFolder.TryGetItemAsync(Constant.BACKUP_FOLDER_NAME) as StorageFolder;
            if (folder == null)
                folder = await Storage.AppLocalFolder.CreateFolderAsync(Constant.BACKUP_FOLDER_NAME, CreationCollisionOption.OpenIfExists);

            await Launcher.LaunchFolderAsync(folder);
        }

        private void WindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            double width = e.NewSize.Width;
            double height = e.NewSize.Height;
            DeckOptionsPage.ScaleWithWindow(width, height, rootGridScale);
        }

        private void RestoreFromBackup(object sender, RoutedEventArgs e)
        {
            BackupFlyout backupFlyout = new BackupFlyout(mainPage.Collection);
            backupFlyout.BackupRestoreFinish += BackupRestoreFinishHandler;
            backupFlyout.ShowFlyout(restoreButton, FlyoutPlacementMode.Top);
        }

        private async void BackupRestoreFinishHandler(object sender, RoutedEventArgs e)
        {
            mainPage.Collection = await Storage.OpenOrCreateCollection(Storage.AppLocalFolder, Constant.COLLECTION_NAME);
            mainPage.Collection.SetIsModified();
            Frame.GoBack();
        }

        private async void OnSyncServiceSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = syncServiceCombobox.SelectedIndex;            
            if(selected == SYNC_ANKIWEB)
            {
                DisableMediaSync();
                await UIHelper.ShowMessageDialog("Currently, Anki Universal does not support syncing MEDIA FILES with AnkiWeb.");
                var ankiwebLogin = new AnkiWebLogin();
                var isSuccess = await ankiwebLogin.TryGetHostKeyFromUsernameAndPassword();
                if (!isSuccess)
                    syncServiceCombobox.SelectedIndex = 0;
                else
                {
                    MainPage.UserPrefs.IsFullSyncRequire = false; //set this to false to warn when editing note types
                    ChangeAnkiWebButtonVisibility(Visibility.Visible);
                }
            }
            else
            {
                AllowMediaSyncCheck();
                MainPage.UserPrefs.IsFullSyncRequire = true;                
                ChangeAnkiWebButtonVisibility(Visibility.Collapsed);
            }
        }

        private void AllowMediaSyncCheck()
        {
            syncMediaCheckBox.IsEnabled = true;
            syncMediaText.Text = "Also sync media files";
            syncMediaCheckBox.Visibility = Visibility.Visible;
        }

        private void DisableMediaSync()
        {
            syncMediaCheckBox.IsChecked = false;
            syncMediaCheckBox.IsEnabled = false;
            syncMediaCheckBox.Visibility = Visibility.Collapsed;
            syncMediaText.Text = "Can't sync media files yet";
        }

        private void ChangeAnkiWebButtonVisibility(Visibility visibility)
        {
            ankiWebLogoutButton.Visibility = visibility;
            forceFullSyncButton.Visibility = visibility;
        }

        private void OnAnkiWebLogoutButtonClick(object sender, RoutedEventArgs e)
        {
            syncServiceCombobox.SelectedIndex = 0;
        }

        private async void OnForceFullSyncButtonClick(object sender, RoutedEventArgs e)
        {
            bool isContinue = await UIHelper.AskUserConfirmation("On next sync, your collection will either be \"Uploaded\" or \"Downloaded\". Continue?");
            if(isContinue)
                mainPage.Collection.ModSchemaNoCheck();
        }
    }
}
