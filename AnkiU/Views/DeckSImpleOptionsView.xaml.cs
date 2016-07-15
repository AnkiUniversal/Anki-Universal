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
    public sealed partial class DeckSImpleOptionsView : UserControl, IAnkiDeckOptionsView
    {
        public readonly string IntervalModHelp = UIConst.CONFIG_INTERVALMOD_HELP;
        public readonly string BuryNewCardHelp = UIConst.CONFIG_BURYNEW_HELP;
        public readonly string BuryReviewHelp = UIConst.CONFIG_BURYREVIEW_HELP;
        public readonly string LeechThresholdHelp = UIConst.CONFIG_LEECHTHRES_HELP;

        public DeckSImpleOptionsView()
        {
            this.InitializeComponent();
        }

        private DeckSimpleOptionsViewModel viewModel;
        public DeckSimpleOptions Options { get { return viewModel.Options; } }

        public void SetDataModel(IAnkiDeckOptionsViewModel data)
        {
            viewModel = data as DeckSimpleOptionsViewModel;
            if (viewModel == null)
                throw new Exception("Wrong datatype. Expected datatype: DeckSimpleOptionsViewModel");            
        }

        private void RestoreButtonClickHandler(object sender, RoutedEventArgs e)
        {
            var temp = new DeckSimpleOptions();
            viewModel.Options.ReviewCardPerDay = temp.ReviewCardPerDay;
            viewModel.Options.NewCardPerDay = temp.NewCardPerDay;
            viewModel.Options.IvlFct = temp.IvlFct;
            viewModel.Options.BuryRelatedNewCard = temp.BuryRelatedNewCard;
            viewModel.Options.BuryRelatedReviewCard = temp.BuryRelatedReviewCard;
            viewModel.Options.LeechAction = temp.LeechAction;
            viewModel.Options.LeechFailsThreshold = temp.LeechFailsThreshold;
            viewModel.Options.AutoPlay = temp.AutoPlay;
            viewModel.Options.NewcardOrder = temp.NewcardOrder;
        }
    }
}
