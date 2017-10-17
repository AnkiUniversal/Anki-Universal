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
    public sealed partial class SyncDialog : ContentDialog
    {
        public string Label
        {
            get
            {                
                return progressLabel.Text;
            }
            set
            {
                progressLabel.Text = value;                
            }
        }

        private CoreDispatcher dispatcher;
        private bool isClosed = false;

        public SyncDialog(CoreDispatcher dispatcher)
        {
            this.InitializeComponent();
            this.dispatcher = dispatcher;
        }

        public void Show()
        {
            var task = dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                SyncIconRotation.Begin();

                isClosed = false;
                while (!isClosed)
                    await this.ShowAsync();                            
            });
        }

        public void Close()
        {
            var task = dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                isClosed = true;
                SyncIconRotation.Stop();
                this.Hide();
            });            
        }
    }
}
