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
    public sealed partial class CreditsPopup : UserControl
    {
        private Grid rootGrid;

        public CreditsPopup(Grid rootGrid)
        {
            this.InitializeComponent();
            this.rootGrid = rootGrid;
        }

        public void Show()
        {
            creditPopup.Visibility = Visibility.Visible;
            userControl.Visibility = Visibility.Visible;

            CalculateSize();
            creditPopup.IsOpen = true;
        }

        private void CalculateSize()
        {
            var newWidth = rootGrid.ActualWidth - 10;
            creditRoot.Width = newWidth;
            creditRoot.Height = rootGrid.ActualHeight - 30;

            if (newWidth > creditRoot.MaxWidth)
                creditPopup.HorizontalOffset = (rootGrid.ActualWidth / 2) - creditRoot.ActualWidth / 2;
            else
                creditPopup.HorizontalOffset = 5;
        }

        public void Hide()
        {
            creditPopup.IsOpen = false;
            creditPopup.Visibility = Visibility.Collapsed;
            userControl.Visibility = Visibility.Collapsed;
        }

        public void ChangeReadMode(bool isNightMode)
        {
            if (isNightMode)
            {
                userControl.Background = new SolidColorBrush(Windows.UI.Colors.Black);
                userControl.Foreground = new SolidColorBrush(Windows.UI.Colors.White);
            }
            else
            {
                userControl.Foreground = new SolidColorBrush(Windows.UI.Colors.Black);
                userControl.Background = new SolidColorBrush(Windows.UI.Colors.White);
            }
        }

        private void CreditClose(object sender, RoutedEventArgs e)
        {
            Hide();
        }

        private void WindowSizeChanged(object sender, SizeChangedEventArgs e)
        {
            CalculateSize();
        }

    }
}
