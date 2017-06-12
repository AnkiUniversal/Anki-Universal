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

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
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
        public string UserName { get; private set; } = null;
        public string PassWord { get; private set; } = null;

        public bool IsValidInput { get; private set; }
        public bool IsUserCancel { get; private set; }

        public AnkiWebLogin()
        {
            this.InitializeComponent();
        }

        private void OnPrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            if(!String.IsNullOrWhiteSpace(ankiWebIdTextBox.Text))
                UserName = ankiWebIdTextBox.Text;

            if (!String.IsNullOrWhiteSpace(passwordBox.Password))
                PassWord = passwordBox.Password;

            if (UserName == null || PassWord == null)
                IsValidInput = false;
            else
                IsValidInput = true;

            IsUserCancel = false;
            this.Hide();
        }

        private void OnSecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            IsValidInput = false;
            IsUserCancel = true;
            this.Hide();
        }

        private async void OnPasswordBoxKeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    OnPrimaryButtonClick(null, null);
                });
            }
        }
    }
}
