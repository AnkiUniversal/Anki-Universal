﻿/*
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

using AnkiU.Interfaces;
using AnkiU.Models;
using AnkiU.UIUtilities;
using AnkiU.ViewModels;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace AnkiU.Views
{
    public sealed partial class DeckHubView : UserControl, IAnkiDecksView
    {
        public event DeckItemClickEventHandler DeckItemClickEvent;
        public event DeckDragAnDropEventHandler DragAnDropEvent;        

        private DeckInformation draggedDeck;

        public bool IsDragAndDropEnable { get; private set; }

        public DeckHubView()
        {
            this.InitializeComponent();            
        }

        private void GridViewItemClickHandler(object sender, ItemClickEventArgs e)
        {
            var data = e.ClickedItem as DeckInformation;
            if (data == null)
                throw new Exception("Wrong data type");

            if (!IsDragAndDropEnable)
                DeckItemClickEvent?.Invoke(data.Id);
        }

        public void ShowShadow()
        {
            shadowBorderBindingTarget.Visibility = Visibility.Visible;
        }

        public void HideShadow()
        {
            shadowBorderBindingTarget.Visibility = Visibility.Collapsed;
        }

        private void ButtonItemClickHandler(object sender, RoutedEventArgs e)
        {
            var data = (sender as FrameworkElement).DataContext as DeckInformation;
            if (data == null)
                throw new Exception("Wrong data type");

            var button = sender as Button;

            DeckItemClickEvent?.Invoke(data.Id);
        }

        private void OnElementDragStarting(UIElement sender, DragStartingEventArgs args)
        {
            draggedDeck = UIHelper.GetDeck(sender);
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            Enlarge.Stop();
            var viewItem = e.OriginalSource as Grid;
            Storyboard.SetTarget(EnlargeX, viewItem);
            Storyboard.SetTarget(EnlargeY, viewItem);
            Enlarge.Begin();
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Link;
        }

        private void OnDragLeave(object sender, DragEventArgs e)
        {
            Enlarge.Stop();
        }

        public void EnableDragAndDropMode()
        {
            IsDragAndDropEnable = true;
            hitTestBindingTarget.IsHitTestVisible = false;
        }

        public void DisableDragAndDropMode()
        {
            IsDragAndDropEnable = false;
            hitTestBindingTarget.IsHitTestVisible = true;            
        }

        private void OnDragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            draggedDeck = e.Items[0] as DeckInformation;
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            var parent = UIHelper.GetDeck(sender);
            if (draggedDeck == null)
                return;

            DragAnDropEvent?.Invoke(parent, draggedDeck);
        }

        private void OnDragItemsCompleted(ListViewBase sender, DragItemsCompletedEventArgs args)
        {
            Enlarge.Stop();
        }
    }
}
