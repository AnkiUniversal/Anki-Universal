﻿using AnkiU.Models;
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

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

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
