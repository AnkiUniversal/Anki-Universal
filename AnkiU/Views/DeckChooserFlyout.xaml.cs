/*
Copyright (C) 2016-2017 Anki Universal Team <ankiuniversal@outlook.com>

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

using AnkiU.AnkiCore;
using AnkiU.Models;
using AnkiU.ViewModels;
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

namespace AnkiU.Views
{
    public sealed partial class DeckChooserFlyout : UserControl
    {
        private Collection collection;        
        private FrameworkElement placeToShow;

        public event RoutedEventHandler OkClick;

        public DeckChooserFlyout(Collection collection, string title)
        {
            this.InitializeComponent();
            this.collection = collection;            
            this.title.Text = title;
        }

        public void GetDeckList()
        {
            DeckNameViewModel deckNameViewModel = new DeckNameViewModel(collection, false, false);
            deckNameView.DataContext = deckNameViewModel.Decks;
        }

        public void ShowFlyout(FrameworkElement element, FlyoutPlacementMode place)
        {
            deckChooserFlyout.Placement = place;
            deckChooserFlyout.ShowAt(element);
            placeToShow = element;
        }

        public DeckInformation GetSelectedDeck()
        {
            return deckNameView.CurrentSelectedDeck();
        }

        private void OkButtonClickHandler(object sender, RoutedEventArgs e)
        {
            deckChooserFlyout.Hide();
            OkClick?.Invoke(sender, e);
        }

        private void CancelButtonClickHandler(object sender, RoutedEventArgs e)
        {
            deckChooserFlyout.Hide();
        }
    }
}
