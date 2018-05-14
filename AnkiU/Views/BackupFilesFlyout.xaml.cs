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
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace AnkiU.Views
{
    public sealed partial class BackupFlyout : UserControl
    {        
        public bool IsRestoreFinished { get; private set; }
        public bool IsFlyoutClosed { get; private set; }
        public bool IsBackupBeforeRestore { get; set; }
        public bool IsUserCancel { get; private set; }
        public event RoutedEventHandler BackupRestoreFinish;

        private Collection collection;
        private List<BackupFilesInformation> backUpFiles = new List<BackupFilesInformation>();
        private StorageFolder backupFolder;        

        public BackupFlyout(Collection collection, bool isBackupBeforeRestore = true)
        {
            this.InitializeComponent();
            this.collection = collection;
            this.IsBackupBeforeRestore = isBackupBeforeRestore;

            IsRestoreFinished = false;
            IsFlyoutClosed = true;
            databaseBackupFlyout.Closed += OnFlyoutClosed;
            databaseBackupFlyout.Opened += OnFlyoutOpened;     
        }

        public async void ShowFlyout(FrameworkElement showAt, FlyoutPlacementMode placeAt)
        {
           var files = await GetBackUpFiles();
           if (files == null)
                return;

            foreach (var file in files)
            {
                var fileProperties = await file.GetBasicPropertiesAsync();
                backUpFiles.Add(new BackupFilesInformation()
                {
                    DateModified = fileProperties.DateModified.LocalDateTime.ToString(),
                    DateModifiedInLong = fileProperties.DateModified.ToUnixTimeSeconds(),
                    Name = file.Name
                });
            }

            backUpFiles.Sort((x, y) => { return -x.DateModifiedInLong.CompareTo(y.DateModifiedInLong); });
            fileListView.DataContext = backUpFiles;
            databaseBackupFlyout.Placement = placeAt;
            databaseBackupFlyout.ShowAt(showAt);
        }

        private async Task<IReadOnlyList<StorageFile>> GetBackUpFiles()
        {
            backupFolder = await Storage.AppLocalFolder.TryGetItemAsync(Constant.BACKUP_FOLDER_NAME) as StorageFolder;
            if (backupFolder == null)
            {
                await UIHelper.ShowMessageDialog("No backups found.");
                return null;
            }

            var files = await backupFolder.GetFilesAsync();
            if (files.Count == 0)
            {
                await UIHelper.ShowMessageDialog("No backups found.");
                return null;
            }

            return files;
        }

        private void CancelButtonClickHandler(object sender, RoutedEventArgs e)
        {
            IsUserCancel = true;
            HideFlyout();
        }

        public void HideFlyout()
        {
            databaseBackupFlyout.Hide();
        }

        private async void RestoreFromBackupClick(object sender, RoutedEventArgs e)
        {
            databaseBackupFlyout.Closed -= OnFlyoutClosed;
            databaseBackupFlyout.Hide();

            var backup = (sender as FrameworkElement).DataContext as BackupFilesInformation;
            var filePath = backupFolder.Path + "\\" + backup.Name;

            string message = "Backed up point: " + backup.DateModified + "\n" +
                 "This will permanently revert all your data (except media files) to the chosen backup file. Continue?";
            if (IsBackupBeforeRestore)
                message += "\n(A backup will also be created automatically before restoring)";
            bool isContinue = await UIHelper.AskUserConfirmation(message);            
                
            if (!isContinue)
                return;
            ProgressDialog progressDialog = ShowDialog();

            var tempFolder = await backupFolder.CreateFolderAsync("temp", CreationCollisionOption.ReplaceExisting);
            try
            {
                if(IsBackupBeforeRestore)
                    await MainPage.BackupDatabase();

                using (var fileStream = File.Open(filePath, FileMode.Open, FileAccess.Read))
                using (var zipFile = new ZipArchive(fileStream, ZipArchiveMode.Read))
                {
                    zipFile.ExtractToDirectory(tempFolder.Path);
                    StorageFile collectionFile = await tempFolder.TryGetItemAsync(Constant.COLLECTION_NAME) as StorageFile;
                    if (collectionFile == null)
                    {
                        progressDialog.Hide();
                        await UIHelper.ShowMessageDialog("The chosen backup file is corrupted. Please choose a different one.");                        
                        return;
                    }

                    progressDialog.ProgressBarLabel = "Start restoring data...";
                    if(collection != null)
                        collection.Close();
                    await collectionFile.CopyAsync(Storage.AppLocalFolder, collectionFile.Name, NameCollisionOption.ReplaceExisting);
                    await RestoreMediaDB(tempFolder);

                    collectionFile = null;
                    BackupRestoreFinish?.Invoke(null, null);
                    IsRestoreFinished = true;
                    progressDialog.Hide();
                    await UIHelper.ShowMessageDialog("Restoring finished.", "Success");                    
                }
            }
            catch
            {
                progressDialog.Hide();
                await UIHelper.ShowMessageDialog("Unexpected error!");
                if(collection != null)
#pragma warning disable CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
                    collection.ReOpen();                
#pragma warning restore CS4014 // Because this call is not awaited, execution of the current method continues before the call is completed
            }
            finally
            {
                RegisterCloseEvent();
                await tempFolder.DeleteAsync();
            }
        }

        private void RegisterCloseEvent()
        {
            IsFlyoutClosed = true;
            databaseBackupFlyout.Closed -= OnFlyoutClosed; //Make sure we only hook this one
            databaseBackupFlyout.Closed += OnFlyoutClosed;
        }

        private static async Task RestoreMediaDB(StorageFolder tempFolder)
        {
            StorageFile mediaDBFile = await tempFolder.TryGetItemAsync(Constant.MEDIA_DB_NAME_ANKI_U) as StorageFile;
            if (mediaDBFile != null)
                await mediaDBFile.CopyAsync(Storage.AppLocalFolder, mediaDBFile.Name, NameCollisionOption.ReplaceExisting);
        }

        private static ProgressDialog ShowDialog()
        {
            var progressDialog = new ProgressDialog();
            progressDialog.ProgressBarLabel = "Cheking backup file...";
            progressDialog.ShowInDeterminateStateNoStopAsync("Restore");
            return progressDialog;
        }

        private void OnFlyoutOpened(object sender, object e)
        {
            IsFlyoutClosed = false;
        }

        private void OnFlyoutClosed(object sender, object e)
        {
            IsFlyoutClosed = true;
        }
    }
}
