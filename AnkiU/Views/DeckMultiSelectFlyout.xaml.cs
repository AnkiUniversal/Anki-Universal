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

using AnkiU.Models;
using AnkiU.UIUtilities;
using AnkiU.ViewModels;
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

namespace AnkiU.Views
{
    public sealed partial class DeckMultiSelectFlyout : UserControl
    {
        private FrameworkElement placeToShow;

        public List<DeckInformation> SelectedDecks { get; set; } = new List<DeckInformation>();

        public event RoutedEventHandler FlyoutClosed;

        public DeckMultiSelectFlyout(DeckNameViewModel viewModel)
        {
            this.InitializeComponent();
            deckListView.DataContext = viewModel.Decks;            
        }

        private void CheckBoxCheckedHandler(object sender, RoutedEventArgs e)
        {            
            var data = (sender as CheckBox).DataContext as DeckInformation;
            if (!SelectedDecks.Contains(data))
                SelectedDecks.Add(data);
        }

        private void CheckBoxUncheckedHandler(object sender, RoutedEventArgs e)
        {
            var data = (sender as CheckBox).DataContext as DeckInformation;
            if (SelectedDecks.Contains(data))
                SelectedDecks.Remove(data);
        }

        public void ShowFlyout(FrameworkElement element, FlyoutPlacementMode placement)
        {
            if (element.ActualWidth < rootGrid.MaxWidth)
                rootGrid.Width = element.ActualWidth;
            else
                rootGrid.Width = rootGrid.MaxWidth;
            
            rootGrid.MaxHeight = CoreWindow.GetForCurrentThread().Bounds.Height / 2;

            flyout.Placement = placement;
            flyout.ShowAt(element);
            placeToShow = element;
        }        

        private void FlyoutClosedEvent(object sender, object e)
        {
            FlyoutClosed?.Invoke(sender, null);
        }

    }
}
