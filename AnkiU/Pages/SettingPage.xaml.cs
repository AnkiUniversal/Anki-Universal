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
using AnkiU.UIUtilities;
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

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace AnkiU.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class SettingPage : Page, INightReadMode
    {
        private bool isNightMode;

        private MainPage mainPage;        

        private CollectionOptionViewModel collectionOptionViewModel;

        public SettingPage()
        {
            this.InitializeComponent();
            numberOfBackup.NumberChanged += BackupNumberChanged;
        }

        private async void BackupNumberChanged(object sender, TextChangedEventArgs e)
        {
            if (numberOfBackup.Number < 5)
            {
                bool isOk = await UIHelper.AskUserConfirmation
                                  ("Are you sure you only want to keep " + numberOfBackup.Number + " backup(s)?\n" +
                                   "(With a collection of 5000 notes, one backup will normally cost 4MB)");
                if (!isOk)
                {
                    numberOfBackup.Number = 10;
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
            syncMediaCheckBox.IsChecked = MainPage.UserPrefs.IsSyncMedia;
            if (!UIHelper.IsDeskTop())
                openBackUpFolderButton.Visibility = Visibility.Collapsed;
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
            MainPage.UserPrefs.NumberOfBackups = numberOfBackup.Number;
            MainPage.UserPrefs.BackupsMinTime = backupTime.Number;
            MainPage.UserPrefs.IsSyncMedia = (bool)syncMediaCheckBox.IsChecked;
            MainPage.UserPrefs.IsSyncOnOpen = (bool)syncOnOpenCheckBox.IsChecked;
            mainPage.UpdateUserPreference();

            collectionOptionViewModel.SaveOptionsToJsonConfig();
            mainPage.Collection.SaveAndCommitAsync();
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
            backupFlyout.ShowFlyout(restoreButton, FlyoutPlacementMode.Full);
        }

        private async void BackupRestoreFinishHandler(object sender, RoutedEventArgs e)
        {
            mainPage.Collection = await Storage.OpenOrCreateCollection(Storage.AppLocalFolder, Constant.COLLECTION_NAME);
            Frame.GoBack();
        }
    }
}
