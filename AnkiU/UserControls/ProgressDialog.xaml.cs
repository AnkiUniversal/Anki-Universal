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
    public sealed partial class ProgressDialog : ContentDialog
    {
        private bool isClosed = false;

        public event TypedEventHandler<ContentDialog, ContentDialogButtonClickEventArgs> StopButtonClickEvent;
        public event TypedEventHandler<ContentDialog, ContentDialogButtonClickEventArgs> CloseButtonClickEvent;        

        public string ProgressBarLabel
        {
            get { return progressBarLabel.Text; }
            set
            {
                //Disable progress bar to avoid error
                var currentState = progressBar.IsIndeterminate;
                progressBar.IsIndeterminate = false;

                progressBarLabel.Text = value;
                progressBar.IsIndeterminate = currentState;
            }
        }

        public ProgressBar ProgressBar
        {
            get { return progressBar; }
            set { progressBar = value; }
        }

        public ProgressDialog()
        {
            this.InitializeComponent();
            var objsect = PrimaryButtonCommandProperty;
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            StopButtonClickEvent?.Invoke(sender, args);
        }

        private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            CloseButtonClickEvent?.Invoke(sender, args);
        }

        public async void ShowInDeterminateStateNoStopAsync(string title)
        {
            Title = title;
            progressBar.IsIndeterminate = true;
            PrimaryButtonText = "";
            SecondaryButtonText = "";

            isClosed = false;
            while (!isClosed)
                await this.ShowAsync();
        }

        public async void ShowDeterminateStateWithStop(string title)
        {
            Title = title;
            progressBar.IsIndeterminate = false;
            PrimaryButtonText = "Stop";
            SecondaryButtonText = "";

            isClosed = false;
            while (!isClosed)
                await this.ShowAsync();
        }

        public void ShowCloseState(string title, string label)
        {
            Title = title;
            if (String.IsNullOrEmpty(label))
                progressBarLabel.Visibility = Visibility.Collapsed;
            else
                progressBarLabel.Text = label;
            //Always set this to false to avoid progressBar keep running in background
            progressBar.IsIndeterminate = false;            
            progressBar.Visibility = Visibility.Collapsed;
            PrimaryButtonText = "";
            SecondaryButtonText = "Close";

            isClosed = true;
        }

        public void ShowErrorState(string title, string label)
        {
            Title = title;
            if (String.IsNullOrEmpty(label))
                progressBarLabel.Visibility = Visibility.Collapsed;
            else
                progressBarLabel.Text = label;

            progressBar.IsIndeterminate = false;
            progressBar.Visibility = Visibility.Collapsed;
            PrimaryButtonText = "";
            SecondaryButtonText = "Close";
        }

        /// <summary>
        /// Do not allow user to call base.Hide() directly as
        /// we need to put ProgressBar to full stop first to avoid memory leakage
        /// </summary>
        public new void Hide()
        {
            progressBar.IsIndeterminate = false;
            isClosed = true;
            base.Hide();
        }

    }
}
