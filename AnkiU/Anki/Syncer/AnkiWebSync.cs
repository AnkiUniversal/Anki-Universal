using AnkiU.AnkiCore;
using AnkiU.AnkiCore.Sync;
using AnkiU.UIUtilities;
using AnkiU.UserControls;
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
        private const string VAULT_RESOURCE = "AnkiUniversal";
        private const string VAULT_USERNAME = "AnkiWeb";

        private MainPage mainPage;

        private AnkiCore.Sync.Syncer client;
        private RemoteServer server;

        private string hostKey = null;

        private SyncDialog syncStateDialog;
        private bool isSyncStateDialogClose;

        public AnkiWebSync(MainPage mainPage)
        {
            this.mainPage = mainPage;

            syncStateDialog = new SyncDialog(mainPage.CurrentDispatcher);
            syncStateDialog.Opened += SyncStateDialogOpened;
            syncStateDialog.Closed += SyncStateDialogClosed;
        }

        public static async Task<bool> TryGetHostKeyFromUsernameAndPassword()
        {
            string hostKey = null;
            var loginForm = new AnkiWebLogin();
            while (hostKey == null)
            {
                try
                {                    
                    await loginForm.ShowAsync();
                    if (loginForm.IsValidInput)
                    {
                        var server = new RemoteServer(null);
                        hostKey = await server.HostKey(loginForm.UserName, loginForm.PassWord);
                        if (hostKey != null)
                        {
                            var vault = new Windows.Security.Credentials.PasswordVault();
                            vault.Add(new Windows.Security.Credentials.PasswordCredential(VAULT_RESOURCE, VAULT_USERNAME, hostKey));
                            return true;
                        }
                    }

                    if (loginForm.IsUserCancel)
                        return false;

                }
                catch (Exception ex)
                {
                    await UIHelper.ShowMessageDialog(ex.Message);
                }
            }
            return true;
        }

        public async Task StartSync()
        {
            try
            {
                syncStateDialog.Label = "Sync collection to AnkiWeb..."
                syncStateDialog.Show(MainPage.UserPrefs.IsReadNightMode);
                GetHostKeyFromVault();

                server = new RemoteServer(hostKey);
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
                else if(results[0] == "clockOff")
                {
                    await UIHelper.ShowMessageDialog("Syncing requires the clock on your computer to be set correctly. Please fix the clock and try again.");
                    return;
                }
                else if (results[0] == "clockOff")
                {
                    await UIHelper.ShowMessageDialog("Syncing requires the clock on your computer to be set correctly. Please fix the clock and try again.");
                    return;
                }
                else if(results[0] == "basicCheckFailed" || results[0] == "sanityCheckFailed")
                {
                    await UIHelper.ShowMessageDialog("Your collection is in an inconsistent state. Please run Tools Check Database in Anki Dekstop, then sync again.");
                    return;
                }
                else if (results[0] == "fullSync")
                {
                    await ConfirmAndStartFullSync();
                    return;
                }
                else if (results[0] == "noChanges" || results[0] == "success")
                {
                    syncStateDialog.Label = "Finished.";
                    syncStateDialog.Show(MainPage.UserPrefs.IsReadNightMode);
                    if (results[0] == "success")                    
                        await ReOpenAndNavigateToDeckSelectPage();
                    else
                        await Task.Delay(250);

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
                await UIHelper.ShowMessageDialog(ex.Message);
            }            
            catch(PasswordVaulException ex)
            {
                await UIHelper.ShowMessageDialog(ex.Message);
            }
            catch(Exception ex)
            {
                await UIHelper.ShowMessageDialog(ex.Message + "\n" + ex.StackTrace);
            }
            finally
            {
                syncStateDialog.Close();
            }
        }

        private async Task ConfirmAndStartFullSync()
        {
            
            var fullSyncclient = new FullSyncer(mainPage.Collection, hostKey);
            ThreeOptionsDialog dialog = new ThreeOptionsDialog();
            dialog.Title = "Full Sync Direction";
            dialog.Message = "Your collection has been modified in a way that the app needs to override the whole collection.\n"                            
                            + "\"Download\" will download the collection from the sever and replace your current one. Unsynced changes on your current collection will be lost.\n"
                            + "\"Upload\" will upload your current collection to the server. Unsynced changes on OTHER devices will be lost.";
            dialog.LeftButton.Content = "Download";
            dialog.MiddleButton.Content = "Upload";
            await dialog.ShowAsync();
            await dialog.WaitForDialogClosed();
            if (dialog.IsLeftButtonClick())
            {
                syncStateDialog.Label = "Downloading full collection database...";
                syncStateDialog.Show(MainPage.UserPrefs.IsReadNightMode);
                await fullSyncclient.Download();
                await ReOpenAndNavigateToDeckSelectPage();
            }
            else if (dialog.IsMiddleButtonClick())
            {
                syncStateDialog.Label = "Uploading full collection database...";
                syncStateDialog.Show(MainPage.UserPrefs.IsReadNightMode);
                await fullSyncclient.Upload();
            }

            syncStateDialog.Label = "Finished.";
            await Task.Delay(250);
            await WaitForCloseSyncStateDialog();
        }

        private async Task ReOpenAndNavigateToDeckSelectPage()
        {
            mainPage.Collection = await Storage.OpenOrCreateCollection(Storage.AppLocalFolder, Constant.COLLECTION_NAME);
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
                hostKey = vault.Retrieve(VAULT_RESOURCE, VAULT_USERNAME).Password;
            }
            catch
            {
                throw new PasswordVaulException("No hostkeys!");
            }
        }

    }
}
