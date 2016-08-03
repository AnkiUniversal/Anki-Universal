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
    public sealed partial class FourOptionsDialog : ContentDialog
    {
        public event RoutedEventHandler OpenButtonClicked;

        public string Message
        {
            get { return messageTextBlock.Text; }
            set { messageTextBlock.Text = value; }
        }

        public Button FirstButton { get { return firstButton; } }
        public Button SecondButton { get { return secondButton; } }
        public Button ThirdButton { get { return thirdButton; } }
        public Button FourthButton { get { return fourthButton; } }
        
        public enum ButtonIndex
        {
            first,
            second,
            third,
            fourth
        }

        private ButtonIndex index;
        public ButtonIndex ButtonClicked { get { return index; } }

        public Visibility NotAskAgainVisibility
        {
            get { return notAskAgainCheckBox.Visibility; }
            set { notAskAgainCheckBox.Visibility = value; }
        }

        public bool? ThreeStateChoose { get; set; } = null;

        private bool isClosed = false;

        public FourOptionsDialog()
        {
            this.InitializeComponent();
            FullSizeDesired = false;
            IsPrimaryButtonEnabled = false;
            IsSecondaryButtonEnabled = false;
            notAskAgainCheckBox.Visibility = Visibility.Collapsed;
            Opened += DialogOpened;
            Closed += DialogClosed;
        }

        private void DialogOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            isClosed = false;
        }

        private void DialogClosed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            isClosed = true;
        }

        public async Task WaitForDialogClosed()
        {
            while (!isClosed)
                await Task.Delay(50);
        }

        private void FirstButtonClick(object sender, RoutedEventArgs e)
        {
            index = ButtonIndex.first;
            this.Hide();
        }

        private void SecondButtonClick(object sender, RoutedEventArgs e)
        {
            index = ButtonIndex.second;
            this.Hide();
        }

        private void ThirdButtonClick(object sender, RoutedEventArgs e)
        {
            index = ButtonIndex.third;
            this.Hide();
        }

        private void FourthButtonClick(object sender, RoutedEventArgs e)
        {
            index = ButtonIndex.fourth;
            this.Hide();
        }

        public bool IsFirstButtonClick()
        {
            if (index == ButtonIndex.first)
                return true;
            return false;
        }

        public bool IsSecondButtonClick()
        {
            if (index == ButtonIndex.second)
                return true;

            return false;
        }

        public bool IsThirdButtonClick()
        {
            if (index == ButtonIndex.third)
                return true;

            return false;
        }

        public bool IsFourthButtonClick()
        {
            if (index == ButtonIndex.fourth)
                return true;

            return false;
        }

        public bool IsNotAskAgain()
        {
            if (notAskAgainCheckBox.IsChecked == true)
                return true;

            return false;
        }

        private void OpenButtonClick(object sender, RoutedEventArgs e)
        {
            OpenButtonClicked?.Invoke(sender, e);
        }
    }
}
