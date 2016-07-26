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
        private Collection collection;
        private List<BackupFilesInformation> backUpFiles = new List<BackupFilesInformation>();
        private StorageFolder backupFolder;

        public event RoutedEventHandler BackupRestoreFinish;

        public BackupFlyout(Collection collection)
        {
            this.InitializeComponent();
            this.collection = collection;            
        }

        public async void ShowFlyout(FrameworkElement showAt, FlyoutPlacementMode placeAt)
        {
           var files = await GetBackUpFiles();
           if (files == null)
                return;

           foreach(var file in files)            
                backUpFiles.Add(new BackupFilesInformation()
                { DateCreate = file.DateCreated.LocalDateTime.ToString(),
                  DateCreatInLong = file.DateCreated.ToUnixTimeSeconds(),
                  Name = file.Name
                });

            backUpFiles.Sort((x, y) => { return -x.DateCreatInLong.CompareTo(y.DateCreatInLong); });
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
            databaseBackupFlyout.Hide();
        }

        private async void RestoreFromBackupClick(object sender, RoutedEventArgs e)
        {
            databaseBackupFlyout.Hide();

            var backup = (sender as FrameworkElement).DataContext as BackupFilesInformation;
            var filePath = backupFolder.Path + "\\" + backup.Name;

            bool isContinue = await UIHelper.AskUserConfirmation
                ("Backed up point: " + backup.DateCreate + "\n" +
                 "This will permanently revert all your data (except media files) to the chosen backup file. Continue?\n" +
                 "(A backup will also be created automatically before restoring)");
            if (!isContinue)
                return;
            ProgressDialog progressDialog = ShowDialog();

            var tempFolder = await backupFolder.CreateFolderAsync("temp", CreationCollisionOption.ReplaceExisting);
            try
            {
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
                    collection.Close();
                    await collectionFile.CopyAsync(Storage.AppLocalFolder, collectionFile.Name, NameCollisionOption.ReplaceExisting);
                    await RestoreMediaDB(tempFolder);

                    collectionFile = null;
                    BackupRestoreFinish?.Invoke(null, null);
                    progressDialog.Hide();
                    await UIHelper.ShowMessageDialog("Restoring finished.", "Success");
                }
            }
            catch
            {
                progressDialog.Hide();
                await UIHelper.ShowMessageDialog("Unexpected error!");
                collection.ReOpen();
            }
            finally
            {
                await tempFolder.DeleteAsync();
            }
        }

        private static async Task RestoreMediaDB(StorageFolder tempFolder)
        {
            StorageFile mediaDBFile = await tempFolder.TryGetItemAsync(Constant.MEDIA_DB_NAME) as StorageFile;
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
    }
}
