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

using AnkiU.Interfaces;
using AnkiU.Models;
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

namespace AnkiU.Views
{
    public sealed partial class DeckLapseOptionsView : UserControl, IAnkiDeckOptionsView
    {
        private DeckLapseOptionsViewModel viewModel;
        public DeckLapseOptions Options { get { return viewModel.Options; } }

        public void SetDataModel(IAnkiDeckOptionsViewModel data)
        {
            viewModel = data as DeckLapseOptionsViewModel;
            if (viewModel == null)
                throw new Exception("Wrong datatype. Expected datatype: DeckLapseOptionsViewModel");

            delaysTextBox.Text = viewModel.Options.Delays;
            viewModel.Options.PropertyChanged += Options_PropertyChanged;            
        }

        private void Options_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            //Can't used data binding for this TextBox, see numberic TextBox for reason
            delaysTextBox.Text = viewModel.Options.Delays;
        }

        public DeckLapseOptionsView()
        {
            this.InitializeComponent();            
        }

        private void DelaysTextBoxTextChangingHandler(TextBox sender, TextBoxTextChangingEventArgs args)
        {            
            sender.Text = sender.Text.StripNonDigitOrWhiteSpace();

            //Can't used data binding for this TextBox, see numberic TextBox for reason
            viewModel.Options.Delays = sender.Text;
        }

        private void RestoreButtonClickHandler(object sender, RoutedEventArgs e)
        {
            var temp = new DeckLapseOptions();
            viewModel.Options.Delays = temp.Delays;
            viewModel.Options.LeechAction = temp.LeechAction;
            viewModel.Options.LeechFailsThreshold = temp.LeechFailsThreshold;
            viewModel.Options.MinInt = temp.MinInt;
            viewModel.Options.NewInterval = temp.NewInterval;
        }
    }
}
