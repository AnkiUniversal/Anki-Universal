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
using System.Reflection;
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
using AnkiU.UIUtilities;
using System.Threading.Tasks;
using Windows.UI.ViewManagement;
using Windows.Graphics.Display;
using AnkiU.Models;
using AnkiU.ViewModels;
using Windows.UI;
using AnkiU.Interfaces;

namespace AnkiU.Views
{
    public sealed partial class DeckListView : UserControl, IAnkiDecksView
    {
        public event DeckItemClickEventHandler DeckItemClickEvent;
        public event DeckDragAnDropEventHandler DragAnDropEvent;

        private DeckInformation draggedDeck;

        public bool IsDragAndDropEnable { get { return ListView.CanDragItems; } }

        public DeckListView()
        {
            this.InitializeComponent();

        }

        private void ListViewItemClickHandler(object sender, ItemClickEventArgs e)
        {
            var item = e.ClickedItem as DeckInformation;
            if (item == null)
                throw new Exception("Wrong data type");

            if(!IsDragAndDropEnable)
                DeckItemClickEvent?.Invoke(item.Id);
        }


        private void OnDragItemsStarting(object sender, DragItemsStartingEventArgs e)
        {
            draggedDeck = e.Items[0] as DeckInformation;
        }

        private void OnDragEnter(object sender, DragEventArgs e)
        {
            e.AcceptedOperation = Windows.ApplicationModel.DataTransfer.DataPackageOperation.Link;
        }

        private void OnDrop(object sender, DragEventArgs e)
        {
            var parent = UIHelper.GetDeck(sender);
            if (draggedDeck == null)
                return;
            DragAnDropEvent?.Invoke(parent, draggedDeck);
        }

        public void EnableDragAndDropMode()
        {
            ListView.CanDragItems = true;
        }

        public void DisableDragAndDropMode()
        {
            ListView.CanDragItems = false;
        }
    }
}
