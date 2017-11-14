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
    public sealed partial class CollectionOptionsView : UserControl
    {

        public CollectionOptionsView()
        {
            this.InitializeComponent();
        }

        public CollectionOptionViewModel ViewModel { get; set; }
        public CollectionOptions Options { get { return ViewModel.Options; } }        

        private void RestoreButtonClickHandler(object sender, RoutedEventArgs e)
        {
            var temp = new CollectionOptions();
            ViewModel.Options.IsShowEstTime = temp.IsShowEstTime;
            ViewModel.Options.IsShowDueCount = temp.IsShowDueCount;
            ViewModel.Options.ReviewType = temp.ReviewType;
            ViewModel.Options.IsTTSAutoplay = temp.IsTTSAutoplay;
            ViewModel.Options.CollapseTime = temp.CollapseTime;
            ViewModel.Options.IsEnableNotification = temp.IsEnableNotification;
            ViewModel.Options.AnswerPosition = temp.AnswerPosition;
            ViewModel.Options.IsBlackNightMode = temp.IsBlackNightMode;
        }
    }
}
