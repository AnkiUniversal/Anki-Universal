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
using System.Threading.Tasks;
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
    public sealed partial class NotificationPopup : UserControl
    {        
        public NotificationPopup()
        {
            this.InitializeComponent();            
        }

        public async void ShowAsync(CoreDispatcher CurrentDispatcher, string textToShow, int showInMiliseconds, double opacity = 1)
        {
            await CurrentDispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                textBlock.Text = textToShow;
                popup.Opacity = opacity;
                popup.IsOpen = true;
                await Task.Delay(showInMiliseconds);
                popup.IsOpen = false;
            });
        }
    }
}
