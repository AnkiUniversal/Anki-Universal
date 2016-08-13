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
using AnkiU.UserControls;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace AnkiU.Views
{
    public sealed partial class MediaBackupFlyout : UserControl
    {
        private const string PROGRESS_LABEL = "{0}: {1}/{2} files";

        private Collection collection;
        private StorageFolder rootFolder;
        private List<long> backupDecksId = new List<long>();
        private List<DeckInformation> allDecks = new List<DeckInformation>();
        private FrameworkElement placeToShow;
        private CoreDispatcher dispatcher;
        private ProgressDialog progressDialog;

        public MediaBackupFlyout(Collection collection)
        {
            this.InitializeComponent();
            this.collection = collection;
            InitDeckList(collection);
            dispatcher = CoreWindow.GetForCurrentThread().Dispatcher;
        }

        private void InitDeckList(Collection collection)
        {
            var deckId = collection.Deck.AllIds();
            foreach (var id in deckId)
            {
                if (id == Constant.DEFAULTDECK_ID)
                    continue;
                if (collection.Deck.IsDyn(id))
                    continue;

                string name = collection.Deck.GetDeckName(id);
                allDecks.Add(new DeckInformation(name, 0, 0, id, false));
            }
            allDecks.Sort((x, y) => { return x.BaseName.CompareTo(y.BaseName); });
            deckListView.DataContext = allDecks;
        }

        public void ShowFlyout(FrameworkElement element, FlyoutPlacementMode placement)
        {
            mediaBackupFlyout.Placement = placement;
            mediaBackupFlyout.ShowAt(element);
            placeToShow = element;
        }

        private void CancelButtonClickHandler(object sender, RoutedEventArgs e)
        {
            mediaBackupFlyout.Hide();
        }

        private async void FolderPickerButtonClick(object sender, RoutedEventArgs e)
        {
            rootFolder = await UIHelper.OpenFolderPicker(UIConst.MEDIA_BACKUP_TOKEN);
            if(rootFolder!=null)            
                backupRootFolderTextBox.Text = rootFolder.Path;
            mediaBackupFlyout.ShowAt(placeToShow);
        }

        private void CheckBoxCheckedHandler(object sender, RoutedEventArgs e)
        {
            var data = (sender as CheckBox).DataContext as DeckInformation;
            backupDecksId.Add(data.Id);
        }

        private void CheckBoxUncheckedHandler(object sender, RoutedEventArgs e)
        {
            var data = (sender as CheckBox).DataContext as DeckInformation;
            backupDecksId.Remove(data.Id);
        }

        private void OkButtonClickHandler(object sender, RoutedEventArgs e)
        {
            if (rootFolder == null)
                return;

            mediaBackupFlyout.Hide();
            PrepareProgessDialog();

            StartBackupMedias();            
        }

        private void StartBackupMedias()
        {
            Task.Run(async () =>
            {
                var fileName = UIHelper.GetDateTimeStringForName();
                fileName.Insert(0, "AnkiBackupMedia ");
                var backUpFolder = await rootFolder.CreateFolderAsync(fileName.ToString(), CreationCollisionOption.GenerateUniqueName);
                var deckIDFolders = await collection.Media.MapDeckIdToDeckIdFolder();

                foreach (var folder in deckIDFolders)
                {
                    if (!backupDecksId.Contains(folder.Key))
                        continue;

                    var files = await folder.Value.GetFilesAsync();
                    int total = files.Count;
                    if (total == 0)
                        continue;

                    string deckName = collection.Deck.GetDeckName(folder.Key).Replace(Constant.SUBDECK_SEPERATE, "_");
                    await UpdateProgessDialog(deckName);

                    string zipFileName = deckName + "_" + folder.Value.Name + ".zip";
                    ZipFile.CreateFromDirectory(folder.Value.Path, backUpFolder.Path + "/" + zipFileName);
                }

                await ShowFinishMessage();
            });
        }

        private async Task UpdateProgessDialog(string deckName)
        {
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {                
                progressDialog.ProgressBarLabel = String.Format("Compressing deck: " + deckName);                
            });
        }

        private async Task ShowFinishMessage()
        {
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                progressDialog.Hide();
                await UIHelper.ShowMessageDialog("To restore media files from backups please use \"Insert media files\" "
                                                + "and choose a backed up zip file.", "Backup Finished");
            });
        }

        private void PrepareProgessDialog()
        {
            progressDialog = new ProgressDialog();
            progressDialog.ProgressBarLabel = "Preparing folders...";
            progressDialog.ShowInDeterminateStateNoStopAsync("Back up media folders");            
        }
    }
}
