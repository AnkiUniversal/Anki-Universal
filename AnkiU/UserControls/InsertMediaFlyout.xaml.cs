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
using AnkiU.UIUtilities;
using AnkiU.UserControls;
using AnkiU.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using System.IO.Compression;

namespace AnkiU.UserControls
{
    public sealed partial class InsertMediaFlyout : UserControl
    {
        private const string ALREADY_EXIST_MESSAGE = "A file name {0} already exists in your media folder. Do you want to replace it?";
        private const string PROGESS_LABEL = "Extracting {0}/{1} files";

        private Collection collection;
        private DeckNameViewModel deckNameViewModel;
        private StorageFile mediaZipFile;
        private FrameworkElement placeToShow;
        private bool isNotAksAgain = false;
        private bool isReplace = false;
        private bool isCancel = false;
        ThreeOptionsDialog dialog;        

        ProgressDialog progressDialog = null;
        private bool isProgressDialogClose = false;

        public InsertMediaFlyout(Collection collection)
        {
            this.InitializeComponent();
            this.collection = collection;
            deckNameViewModel = new DeckNameViewModel(collection, false, false);            
            deckNameView.DataContext = deckNameViewModel.Decks;
        }

        public void ShowFlyout(FrameworkElement element, FlyoutPlacementMode place)
        {
            mediaInsertFlyout.Placement = place;
            mediaInsertFlyout.ShowAt(element);
            placeToShow = element;
        }

        private void CancelButtonClickHandler(object sender, RoutedEventArgs e)
        {
            mediaInsertFlyout.Hide();
        }

        private async void OkButtonClickHandler(object sender, RoutedEventArgs e)
        {
            if(mediaZipFile == null)
            {
                await UIHelper.ShowMessageDialog("Please choose a backed up zip file.");
                mediaInsertFlyout.ShowAt(placeToShow);
                return;
            }

            var currentSelectedDeck = deckNameView.CurrentSelectedDeck();
            if (currentSelectedDeck == null)
            {
                await UIHelper.ShowMessageDialog("Please choose the deck you want to insert media files.");
                mediaInsertFlyout.ShowAt(placeToShow);
                return;
            }

            PrepareProgessDialog();
            ResetFlags();

            using (var stream = await mediaZipFile.OpenStreamForReadAsync())
            using (ZipArchive archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                var total = archive.Entries.Count;
                if (total == 0)
                {
                    await UIHelper.ShowMessageDialog("No files found.");
                    mediaInsertFlyout.ShowAt(placeToShow);
                    return;
                }                

                var deckList = await collection.Media.MapDeckIdToDeckIdFolder();
                var deckFolder = deckList[currentSelectedDeck.Id];
                int index = 0;

                try
                {
                    collection.Media.Database.BeginTransaction();
                    foreach (var entry in archive.Entries)
                    {
                        progressDialog.ProgressBarLabel = String.Format(PROGESS_LABEL, index, total);
                        index++;

                        var existFile = await deckFolder.TryGetItemAsync(entry.Name) as StorageFile;
                        if (existFile != null)
                        {
                            if (!isNotAksAgain)
                                await AskUserConfirmationForReplacing(entry.Name);

                            if (isCancel)
                                return;

                            if (!isReplace)
                                continue;
                        }
                        entry.ExtractToFile(deckFolder.Path + "/" + entry.Name, true);
                        collection.Media.MarkFileAddIntoDatabase(entry.Name, currentSelectedDeck.Id);
                    }

                    progressDialog.Hide();
                    await UIHelper.ShowMessageDialog("Finished inserting media files.");
                }
                finally
                {
                    progressDialog.Hide();                    
                    collection.Media.Database.Commit();                    
                }                
            }
        }

        private void ResetFlags()
        {
            isReplace = false;
            isCancel = false;
            isNotAksAgain = false;
        }

        private void PrepareProgessDialog()
        {
            if (progressDialog == null)
            {
                progressDialog = new ProgressDialog();
                progressDialog.ProgressBarLabel = "Extracting...";
                progressDialog.ShowInDeterminateStateNoStopAsync("Insert media files");
                progressDialog.Closed += ProgressDialogClosed;
                progressDialog.Opened += ProgressDialogOpened;
            }
        }

        private void ProgressDialogOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            isProgressDialogClose = false;
        }

        private void ProgressDialogClosed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            isProgressDialogClose = true;
        }

        private async Task AskUserConfirmationForReplacing(string name)
        {
            var message = String.Format(ALREADY_EXIST_MESSAGE, name);
            if (dialog == null)
                InitMessageDialog(message);
            else
                dialog.Message = message;

            await WaitToCloseProgressDialog();
            await GetUserConfirmation();
        }

        private async Task GetUserConfirmation()
        {
            await dialog.ShowAsync();
            if (dialog.IsLeftButtonClick())
            {
                isCancel = false;
                isReplace = true;
            }
            else if (dialog.IsMiddleButtonClick())
            {
                isCancel = false;
                isReplace = false;
            }
            else
                isCancel = true;

            isNotAksAgain = dialog.IsNotAskAgain();            

            await dialog.WaitForDialogClosed();
            progressDialog.ShowInDeterminateStateNoStopAsync("Insert media files");
        }

        private async Task WaitToCloseProgressDialog()
        {
            progressDialog.Hide();
            while (!isProgressDialogClose)
                await Task.Delay(50);
        }

        private void InitMessageDialog(string message)
        {
            dialog = new ThreeOptionsDialog();
            dialog.Message = message;
            dialog.Title = "Duplicate File(s)";
            dialog.LeftButton.Content = "Yes";
            dialog.MiddleButton.Content = "No";
            dialog.RightButton.Content = "Cancel";
            dialog.NotAskAgainVisibility = Visibility.Visible;            
        }

        private async void FolderPickerButtonClick(object sender, RoutedEventArgs e)
        {
            mediaZipFile = await UIHelper.OpenFilePicker(UIConst.MEDIA_BACKUP_TOKEN, ".zip");            
            if(mediaZipFile != null)            
                backupRootFolderTextBox.Text = mediaZipFile.Path;
            mediaInsertFlyout.ShowAt(placeToShow);
        }
    }
}

