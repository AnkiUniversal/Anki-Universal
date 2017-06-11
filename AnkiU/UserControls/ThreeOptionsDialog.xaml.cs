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
    public sealed partial class ThreeOptionsDialog : ContentDialog
    {
        public string Message { get { return messageTextBlock.Text; }
                                set { messageTextBlock.Text = value; } }

        public Button LeftButton { get { return leftButton; } }
        public Button MiddleButton { get { return middleButton; } }
        public Button RightButton { get { return rightButton; } }

        public Visibility NotAskAgainVisibility { get { return notAskAgainCheckBox.Visibility; }
                                                  set { notAskAgainCheckBox.Visibility = value; } }

        public bool? ThreeStateChoose { get; set; } = null;

        private bool isClosed = true;

        public ThreeOptionsDialog()
        {
            this.InitializeComponent();
            FullSizeDesired = false;
            IsPrimaryButtonEnabled = false;
            IsSecondaryButtonEnabled = false;
            notAskAgainCheckBox.Visibility = Visibility.Collapsed;
            Opened += ThreeOptionsDialogOpened;
            Closed += ThreeOptionsDialogClosed;
        }

        private void ThreeOptionsDialogOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            isClosed = false;
        }

        private void ThreeOptionsDialogClosed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            isClosed = true;
        }
        
        public async Task WaitForDialogClosed()
        {
            while (!isClosed)
                await Task.Delay(50);
        }

        private void LeftButtonClick(object sender, RoutedEventArgs e)
        {
            ThreeStateChoose = true;
            this.Hide();
        }

        private void MiddleButtonClick(object sender, RoutedEventArgs e)
        {
            ThreeStateChoose = false;
            this.Hide();
        }

        private void RightButtonClick(object sender, RoutedEventArgs e)
        {
            ThreeStateChoose = null;
            this.Hide();
        }

        public bool IsLeftButtonClick()
        {
            if (ThreeStateChoose == true)
                return true;
            return false;
        }

        public bool IsMiddleButtonClick()
        {
            if (ThreeStateChoose == false)
                return true;

            return false;
        }

        public bool IsRightButtonClick()
        {
            if (ThreeStateChoose == null)
                return true;

            return false;
        }

        public bool IsNotAskAgain()
        {
            if (notAskAgainCheckBox.IsChecked == true)
                return true;

            return false;
        }

    }
}
