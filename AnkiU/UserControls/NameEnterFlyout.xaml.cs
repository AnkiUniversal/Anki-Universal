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
    public sealed partial class NameEnterFlyout : UserControl
    {    
        public FlyoutPlacementMode Placement { get { return nameFlyout.Placement; } set { nameFlyout.Placement = value; } }    
        public string NewName { get; set; }
        public event RoutedEventHandler OkButtonClickEvent;
        public event NoticeRoutedHandler Opened;
        public event NoticeRoutedHandler Closed;
        private CoreDispatcher dispatcher;

        public NameEnterFlyout()
        {
            this.InitializeComponent();
            dispatcher = CoreWindow.GetForCurrentThread().Dispatcher;
            nameFlyout.Opened += NameFlyoutOpenedHandler;
            nameFlyout.Closed += NameFlyoutClosedHandler;
        }

        private void NameFlyoutClosedHandler(object sender, object e)
        {
            Closed?.Invoke();
        }

        private void NameFlyoutOpenedHandler(object sender, object e)
        {
            Opened?.Invoke();
        }

        public void Show(FrameworkElement placeToShow, string nameToShow = null)
        {
            if(nameToShow != null)
                renameFlyoutTextBox.Text = nameToShow;
            nameFlyout.ShowAt(placeToShow);
        }

        private void OKButtonClick(object sender, RoutedEventArgs e)
        {
            nameFlyout.Hide();
            NewName = AnkiCore.Utils.GetValidName(renameFlyoutTextBox.Text);
            OkButtonClickEvent?.Invoke(nameFlyout, null);            
        }

        private void CancelButtonClick(object sender, RoutedEventArgs e)
        {
            NewName = null;
            nameFlyout.Hide();
        }

        private async void NameFlyoutTextBoxKeyUpHandler(object sender, KeyRoutedEventArgs e)
        {
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if(e.Key == Windows.System.VirtualKey.Enter)
                    OKButtonClick(nameFlyout, null);
            });
        }
    }
}
