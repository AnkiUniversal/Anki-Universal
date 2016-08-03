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
    public sealed partial class NoteEditorPopup : UserControl
    {
        private const double DEFAULT_HEIGHT_MARGIN = 0;
        private const double DEFAULT_WIDTH_MARGIN = 0;

        private Frame contentFrame;
        private MainPage mainPage;

        public bool IsLightDismissEnabled { get { return popUp.IsLightDismissEnabled; } set { popUp.IsLightDismissEnabled = value; } }

        public event RoutedEventHandler CloseEvent;

        public long EditNoteId { get; set; }

        public NoteEditorPopup(NoteEditorPageParameter param)
        {
            this.InitializeComponent();
            mainPage = param.Mainpage;

            contentFrame = new Frame();
            UIHelper.AddToGridInFull(frameGrid, contentFrame);
            contentFrame.Navigate(typeof(NoteEditor), param);                        
        }

        private void CalculateSizeAndPosition()
        {
            var winWidth = userControl.ActualWidth;
            var winHeight = userControl.ActualHeight;
            var maxWidth = winWidth - DEFAULT_WIDTH_MARGIN;
            var maxHeight = winHeight - DEFAULT_HEIGHT_MARGIN;            

            popUp.MaxWidth = maxWidth;
            mainGrid.Width = maxWidth;
            popUp.MaxHeight = maxHeight;
            mainGrid.Height = maxHeight;
        }

        public void Show()
        {                        
            mainPage.CommanBar.Opening += CommanBarOpening;
            mainPage.CommanBar.Closed += CommanBarClosed;

            CalculateSizeAndPosition();
            popUp.IsOpen = true;
        }        

        private void CommanBarOpening(object sender, object e)
        {            
            //Close it to avoid UI overlap problems
            popUp.IsOpen = false;
        }

        private void CommanBarClosed(object sender, object e)
        {
            popUp.IsOpen = true;
        }

        public async void Close()
        {
            var noteEditorPage = (contentFrame.Content as NoteEditor);
            bool success = await noteEditorPage.SaveEditNote();
            if (!success)
                return;
            EditNoteId = noteEditorPage.GetCurrentNoteId();

            mainPage.CommanBar.Opening -= CommanBarOpening;
            mainPage.CommanBar.Closed -= CommanBarClosed;

            popUp.IsOpen = false;

            frameGrid.Children.Clear();
            await noteEditorPage.ClearPage();
            contentFrame.Content = null;
            contentFrame = null;
            GC.Collect();

            CloseEvent?.Invoke(null, null);
        }

        private void CloseButtonClick(object sender, RoutedEventArgs e)
        {
            Close();
        }    

        private void UserControlSizeChanged(object sender, SizeChangedEventArgs e)
        {
            CalculateSizeAndPosition();
        }
    }
}
