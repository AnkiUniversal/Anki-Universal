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
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace AnkiU.UserControls
{
    public sealed partial class RescheduleFlyout : UserControl
    {
        public int Number { get; set; }
        public event RoutedEventHandler OKButtonClickEvent;
        public event RoutedEventHandler ClosedEvent;

        public RescheduleFlyout()
        {
            this.InitializeComponent();
        }        

        public void Show(FrameworkElement placeToShow)
        {
            rescheduleFlyout.ShowAt(placeToShow);            
        }

        private void OKButtonClickHandler(object sender, RoutedEventArgs e)
        {
            rescheduleFlyout.Hide();
            Number = numberBox.Number;
            OKButtonClickEvent?.Invoke(rescheduleFlyout, null);            
        }

        private async void NumberTextBoxEnterKeyUpHandler(object sender, KeyRoutedEventArgs e)
        {
            await CoreWindow.GetForCurrentThread().Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (e.Key == Windows.System.VirtualKey.Enter)
                {
                    OKButtonClickHandler(sender, null);
                    e.Handled = true;
                }
            });
        }

        private void CancelButtonClickHandler(object sender, RoutedEventArgs e)
        {
            rescheduleFlyout.Hide();
        }

        private void RescheduleFlyoutClosedHandler(object sender, object e)
        {
            ClosedEvent?.Invoke(sender, null);
        }
    }
}
