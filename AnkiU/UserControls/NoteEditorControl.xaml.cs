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

using AnkiU.Pages;
using AnkiU.UIUtilities;
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
    public sealed partial class NoteEditorControl : UserControl
    {
        private Frame contentFrame;
        private MainPage mainPage;

        public event RoutedEventHandler ClosedEvent;

        public bool IsNavigated { get; set; }
        public long EditNoteId { get; set; }

        public NoteEditorControl()
        {
            this.InitializeComponent();
        }

        public void NavigateToNoteEditor(NoteEditorPageParameter param)
        {
            mainPage = param.Mainpage;

            contentFrame = new Frame();
            UIHelper.AddToGridInFull(frameGrid, contentFrame);
            contentFrame.Navigate(typeof(NoteEditor), param);
            IsNavigated = true;
        }

        public async void Close()
        {
            var noteEditorPage = (contentFrame.Content as NoteEditor);
            bool success = await noteEditorPage.SaveEditNote();
            if (!success)
                return;
            EditNoteId = noteEditorPage.GetCurrentNoteId();

            frameGrid.Children.Clear();
            await noteEditorPage.ClearPage();
            contentFrame.Content = null;
            contentFrame = null;
            //GC.Collect(); //Disable in "Creator Update"

            IsNavigated = false;

            ClosedEvent?.Invoke(null, null);
        }

        private void CloseButtonClick(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
