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
                syncStateDialog.Show(MainPage.UserPrefs.IsReadNightMode);
                GetHostKeyFromVault();

                server = new RemoteServer(hostKey);
                client = new AnkiCore.Sync.Syncer(mainPage.Collection, server);

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
                await UIHelper.ShowMessageDialog(ex.StackTrace);
            }
            finally
            {
                syncStateDialog.Close();
            }
        }

        private void SyncStateDialogOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            isSyncStateDialogClose = false;
        }

        private void SyncStateDialogClosed(ContentDialog sender, Windows.UI.Xaml.Controls.ContentDialogClosedEventArgs args)
        {
            isSyncStateDialogClose = true;
        }

        private async Task CloseSyncStateDialog()
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
