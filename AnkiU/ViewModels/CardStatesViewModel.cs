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
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Series;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static AnkiU.AnkiCore.Stats;

namespace AnkiU.ViewModels
{
    public class CardStatesViewModel : IPlotChartModel
    {
        public PlotModel ChartModel { get; private set; }

        public CardStatesViewModel(string title, string subTitle, CardStatesData cardStates)
        {
            if (cardStates.TotalCards == 0)
                return;

            string text = String.Format("\nTotal cards: {0}. Total notes: {1}.", cardStates.TotalCards, cardStates.TotalNotes);
            subTitle += text;

            this.ChartModel = new PlotModel
            {
                Title = title,
                Subtitle = subTitle,
                TitleHorizontalAlignment = TitleHorizontalAlignment.CenteredWithinView,
                LegendPlacement = LegendPlacement.Outside,
                LegendPosition = LegendPosition.BottomCenter,
                LegendOrientation = LegendOrientation.Horizontal,
                LegendBorderThickness = 0,
                Background = OxyColors.Transparent,
            };

            var categoryAxis = new CategoryAxis
            {
                FontSize = 13,
                Position = AxisPosition.Bottom,
                AbsoluteMaximum = 4,
                AbsoluteMinimum = -1,                
            };
            categoryAxis.Labels.Add("New");
            categoryAxis.Labels.Add("Young");
            categoryAxis.Labels.Add("Mature");
            categoryAxis.Labels.Add("Suspended &\n Buried");

            var columnSeries = new ColumnSeries
            {
                IsStacked = false,
                FillColor = OxyColors.LightGreen,
                StrokeThickness = 0,
                LabelPlacement = LabelPlacement.Outside,   
                LabelFormatString = "{0}"
            };
            columnSeries.Items.Add(new ColumnItem(cardStates.TotalNew));            
            columnSeries.Items.Add(new ColumnItem(cardStates.TotalYoung));
            columnSeries.Items.Add(new ColumnItem(cardStates.TotalMature));
            columnSeries.Items.Add(new ColumnItem(cardStates.TotalSuspendAndBury));

            columnSeries.Items[0].Color = OxyColors.Blue;
            columnSeries.Items[1].Color = OxyColors.LightGreen;
            columnSeries.Items[2].Color = OxyColors.DarkOliveGreen;
            columnSeries.Items[3].Color = OxyColors.Orange;            

            var maxYAxis = (new long[] { cardStates.TotalMature, cardStates.TotalYoung,
                                         cardStates.TotalNew, cardStates.TotalSuspendAndBury }).Max();
            var cardsAxis = new LinearAxis
            {
                Position = AxisPosition.Right,
                Title = "Cards",
                MinimumPadding = 0.1,
                MaximumPadding = 0.6,
                MajorStep = maxYAxis / 5 + 1,
                MinorTickSize = 0,
                AbsoluteMinimum = 0,
                AbsoluteMaximum = maxYAxis * 1.2
            };

            ChartModel.Axes.Add(categoryAxis);
            ChartModel.Axes.Add(cardsAxis);

            ChartModel.Series.Add(columnSeries);
        }
    }
}
