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

using AnkiU.AnkiCore.Sync;
using AnkiU.UIUtilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace AnkiU.UserControls
{
    public sealed partial class AnkiWebLogin : ContentDialog
    {
        public const string VAULT_RESOURCE = "AnkiUniversal";
        public const string VAULT_USERNAME = "AnkiWeb";

        private string userName;
        private string passWord;

        private bool isLoginSuccess;
        private bool isValidInput;
        private bool isUserCancel;       

        public AnkiWebLogin()
        {
            this.InitializeComponent();
        }

        public async Task<bool> TryGetHostKeyFromUsernameAndPassword()
        {
            while (true)
            {
                EnableInput();
                await ShowAsync();
                if (isLoginSuccess)
                    return true;

                if (isUserCancel)
                    return false;
            }            
        }

        private async void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            try
            {
                DisableInput();
                VerifyInput();
                if (isValidInput)
                {
                    ShowProgressBar();                    
                    var server = new RemoteServer(null);
                    var hostKey = await server.HostKey(userName, passWord);
                    if (hostKey != null)
                    {
                        var vault = new Windows.Security.Credentials.PasswordVault();
                        vault.Add(new Windows.Security.Credentials.PasswordCredential(VAULT_RESOURCE, VAULT_USERNAME, hostKey));                        
                        isLoginSuccess = true;
                        Close();
                    }
                }
            }
            catch (Exception ex)
            {
                isLoginSuccess = false;
                Close();
                await UIHelper.ShowMessageDialog(ex.Message);
            }            
        }

        private void VerifyInput()
        {
            if (!String.IsNullOrWhiteSpace(ankiWebIdTextBox.Text))
                userName = ankiWebIdTextBox.Text;
            else
                userName = null;

            if (!String.IsNullOrWhiteSpace(passwordBox.Password))
                passWord = passwordBox.Password;
            else
                passWord = null;

            if (userName == null || passWord == null)
                isValidInput = false;
            else
                isValidInput = true;

            isUserCancel = false;
        }

        private void ShowProgressBar()
        {
            progressBar.IsEnabled = true;
            progressBar.IsIndeterminate = true;
            progressBar.Visibility = Visibility.Visible;
        }

        private void DisableInput()
        {
            ankiWebIdTextBox.IsEnabled = false;
            passwordBox.IsEnabled = false;
        }

        private void OnSecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            isValidInput = false;
            isUserCancel = true;
            Close();
        }

        private async void OnPasswordBoxKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                e.Handled = true;
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    OnPrimaryButtonClick(null, null);
                });
            }
        }

        private void Close()
        {
            HideProgressBar();
            this.Hide();
        }

        private void EnableInput()
        {
            ankiWebIdTextBox.IsEnabled = true;
            passwordBox.IsEnabled = true;
        }

        private void HideProgressBar()
        {
            progressBar.IsIndeterminate = false;
            progressBar.IsEnabled = false;
            progressBar.Visibility = Visibility.Collapsed;
        }
    }
}
