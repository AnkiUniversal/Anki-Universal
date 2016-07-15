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
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace AnkiU.Views
{
    public sealed partial class DeckNewOptionsView : UserControl, IAnkiDeckOptionsView
    {        
        private DeckNewOptionsViewModel viewModel;
        public DeckNewOptions Options { get { return viewModel.Options; } }

        public void SetDataModel(IAnkiDeckOptionsViewModel data)
        {
            viewModel = data as DeckNewOptionsViewModel;
            if (viewModel == null)
                throw new Exception("Wrong datatype. Expected datatype: DeckNewOptionsViewModel");

            //Can't use data binding for this TextBox, see numberic text box for reason
            delaysTextBox.Text = viewModel.Options.Delays;
            viewModel.Options.PropertyChanged += OptionsPropertyChangedHandler;
        }

        private void OptionsPropertyChangedHandler(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            delaysTextBox.Text = viewModel.Options.Delays;
        }

        public DeckNewOptionsView()
        {
            this.InitializeComponent();
        }

        private void delaysTextBoxTextChangingHandler(TextBox sender, TextBoxTextChangingEventArgs args)
        {
            sender.Text = sender.Text.StripNonDigitOrWhiteSpace();
            viewModel.Options.Delays = sender.Text;
        }

        private void RestoreButtonClickHandler(object sender, RoutedEventArgs e)
        {
            var temp = new DeckNewOptions();
            viewModel.Options.Bury = temp.Bury;
            viewModel.Options.Delays = temp.Delays;
            viewModel.Options.EasyInterval = temp.EasyInterval;
            viewModel.Options.GraduatingInterval = temp.GraduatingInterval;
            viewModel.Options.InitialFactor = temp.InitialFactor;
            viewModel.Options.Order = temp.Order;
            viewModel.Options.PerDay = temp.PerDay;
        }
    }
}
