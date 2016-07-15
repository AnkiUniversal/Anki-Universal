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

using AnkiU.UIUtilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace AnkiU.UserControls
{
    public sealed partial class AudioRecorderDialog : ContentDialog
    {
        public StorageFile FileRecorded { get; set; }
        public StorageFolder Folder { get; set; }
        public AudioRecorderDialog(StorageFolder folder)
        {
            this.InitializeComponent();
            Folder = folder;
        }

        private async void SaveAndCloseButtonClickHandler(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            //Immediately cancel to avoid dialog closes before saving
            args.Cancel = true;

            FileRecorded = await recorder.TrySaveAudio(Folder);
            if (FileRecorded == null)
                await UIHelper.ShowMessageDialog("Failed to save file!");
            recorder.Close();
            this.Hide();
        }

        private void CloseButtonClickHandler(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            recorder.Close();
            this.Hide();
        }        
    }
}
