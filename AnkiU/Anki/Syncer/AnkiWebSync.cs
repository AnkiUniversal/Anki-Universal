using AnkiU.AnkiCore;
using AnkiU.AnkiCore.Sync;
using AnkiU.UIUtilities;
using AnkiU.UserControls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;

namespace AnkiU.Anki.Syncer
{
    public class PasswordVaulException : Exception
    {
        public PasswordVaulException(string ex) : base(ex)
        { }
    }

    public class AnkiWebSync : ISync
    {
        private const string SYNC_Progress = "Received: {0} KB \n Sent: {1} KB";
        private MainPage mainPage;

        private AnkiCore.Sync.Syncer client;
        private RemoteServer server;

        private string hostKey = null;

        private SyncDialog syncStateDialog;
        private bool isSyncStateDialogClose;

        private string label = null;

        public AnkiWebSync(MainPage mainPage)
        {
            this.mainPage = mainPage;

            syncStateDialog = new SyncDialog(mainPage.CurrentDispatcher);
            syncStateDialog.Opened += SyncStateDialogOpened;
            syncStateDialog.Closed += SyncStateDialogClosed;
        }

        public async Task StartSync()
        {
            try
            {
                SetSyncLabel("Sync collection to AnkiWeb...");
                syncStateDialog.Show();
                GetHostKeyFromVault();

                server = new RemoteServer(hostKey);
                server.OnHttpProgressEvent += OnServerHttpProgressEvent;
                client = new AnkiCore.Sync.Syncer(mainPage.Collection, server);
                var results = await client.Sync();
                await WaitForCloseSyncStateDialog();
                if (results == null)
                {
                    await UIHelper.ShowMessageDialog("No respone from the server! Either your connection or the server did not work properly.");
                    return;
                }
                else if (results[0] == "badAuth")
                {
                    //Include for completeness purpose.
                    //We should not run into this, as the app only logins when user changed sync service                    
                    await UIHelper.ShowMessageDialog("AnkiWeb ID or password was incorrect. Please try to log in again.");
                    return;
                }
                else if (results[0] == "clockOff")
                {
                    await UIHelper.ShowMessageDialog("Syncing requires the clock on your computer to be set correctly. Please fix the clock and try again.");
                    return;
                }
                else if (results[0] == "basicCheckFailed" || results[0] == "sanityCheckFailed" || results[0] == "sanityCheckError")
                {
                    await UIHelper.ShowMessageDialog("Your collection is in an inconsistent state.\n" + 
                                                    "Please run \"Check Collection\" or \"Force Full Sync\", then try again.");
                    return;
                }
                else if (results[0] == "fullSync")
                {
                    await ConfirmAndStartFullSync();
                    MainPage.UserPrefs.IsFullSyncRequire = false;
                    return;
                }
                else if (results[0] == "noChanges" || results[0] == "success")
                {
                    SetSyncLabel("Finished.");
                    syncStateDialog.Show();
                    if (results[0] == "success")
                    {
                        await NavigateToDeckSelectPage();
                    }
                    else
                        await Task.Delay(250);

                    MainPage.UserPrefs.IsFullSyncRequire = false;
                    await WaitForCloseSyncStateDialog();
                    return;
                }
                else if (results[0] == "serverAbort")
                {
                    await UIHelper.ShowMessageDialog("Server aborted.");
                    return;
                }
                else
                {
                    await UIHelper.ShowMessageDialog("Unknown sync return code.");
                    return;
                }
            }
            catch (HttpSyncerException ex)
            {
                await WaitForCloseSyncStateDialog();
                await UIHelper.ShowMessageDialog("AnkiWeb Sync: " + ex.Message);
            }            
            catch(PasswordVaulException ex)
            {
                await WaitForCloseSyncStateDialog();
                await UIHelper.ShowMessageDialog("AnkiWeb Sync: " + ex.Message);
            }
            catch(FileLoadException ex)
            {
                await WaitForCloseSyncStateDialog();
                await UIHelper.ShowMessageDialog("AnkiWeb Sync: " + ex.Message);
            }
            catch(Exception ex)
            {
                await WaitForCloseSyncStateDialog();
                await UIHelper.ShowMessageDialog("AnkiWeb Sync: " + ex.Message + "\n" + ex.StackTrace);
            }
            finally
            {
                syncStateDialog.Close();
            }
        }

        private void SetSyncLabel(string message)
        {
            label = message;
            syncStateDialog.Label = message;
        }

        private async Task ConfirmAndStartFullSync()
        {
            
            var fullSyncclient = new FullSyncer(mainPage.Collection, hostKey);
            fullSyncclient.OnHttpProgressEvent += OnServerHttpProgressEvent;
            ThreeOptionsDialog dialog = new ThreeOptionsDialog();
            dialog.Title = "Full Sync Direction";
            dialog.Message = "Your collection has been modified in a way that a full sync is required.\n"                            
                            + "\"Download\" will download the collection from the sever and replace your current one. Unsynced changes will be lost.\n"
                            + "\"Upload\" will upload your current collection to the server. Unsynced changes on OTHER devices will be lost.";
            dialog.LeftButton.Content = "Download";
            dialog.MiddleButton.Content = "Upload";
            await dialog.ShowAsync();
            await dialog.WaitForDialogClosed();
            if (dialog.IsLeftButtonClick())
            {
                await MainPage.BackupDatabase();
                await DownloadFullDatabase(fullSyncclient);
            }
            else if (dialog.IsMiddleButtonClick())
            {
                var isContinue = await UIHelper.AskUserConfirmation("UPLOAD your collection to the server?");
                if (isContinue)                    
                    await UploadFullDatabase(fullSyncclient);
            }

            SetSyncLabel("Finished.");            
            await Task.Delay(250);
            await WaitForCloseSyncStateDialog();
        }

        private async Task DownloadFullDatabase(FullSyncer fullSyncclient)
        {
            SetSyncLabel("Downloading full database...");
            syncStateDialog.Show();
            await fullSyncclient.Download();
            await ReOpenAndNavigateToDeckSelectPage();
        }

        private async Task UploadFullDatabase(FullSyncer fullSyncclient)
        {
            SetSyncLabel("Uploading full database...");
            syncStateDialog.Show();
            await fullSyncclient.Upload();
        }

        private async Task ReOpenAndNavigateToDeckSelectPage()
        {
            mainPage.Collection = await Storage.OpenOrCreateCollection(Storage.AppLocalFolder, Constant.COLLECTION_NAME);
            await NavigateToDeckSelectPage();
        }

        private async Task NavigateToDeckSelectPage()
        {
            await mainPage.NavigateToDeckSelectPage();
            mainPage.ContentFrame.BackStack.RemoveAt(0);
        }

        private void SyncStateDialogOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            isSyncStateDialogClose = false;
        }

        private void SyncStateDialogClosed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            isSyncStateDialogClose = true;
        }

        private async Task WaitForCloseSyncStateDialog()
        {
            syncStateDialog.Close();
            while (!isSyncStateDialogClose)
                await Task.Delay(50);
        }

        private void GetHostKeyFromVault()
        {
            try
            {
                var vault = new Windows.Security.Credentials.PasswordVault();
                hostKey = vault.Retrieve(AnkiWebLogin.VAULT_RESOURCE, AnkiWebLogin.VAULT_USERNAME).Password;
            }
            catch
            {
                throw new PasswordVaulException("No hostkeys!");
            }
        }
        
        private async void OnServerHttpProgressEvent(Windows.Web.Http.HttpProgress progress)
        {
            await mainPage.CurrentDispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                ulong totalToReceive = 0;
                if (progress.TotalBytesToReceive != null)
                    totalToReceive = (ulong)progress.TotalBytesToReceive / 1024;

                ulong totalToSend = 0;
                if (progress.TotalBytesToSend != null)
                    totalToSend = (ulong)progress.TotalBytesToSend / 1024;
                
                syncStateDialog.Label = label + "\n"
                                        + String.Format(SYNC_Progress, progress.BytesReceived/1024 + "/" + totalToReceive,
                                                                       progress.BytesSent/1024 + "/" + totalToSend);
            });
        }
    }
}
