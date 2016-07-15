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

using AnkiU.AnkiCore;
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
    public class ForeCastChartViewModel : IPlotChartModel
    {
        private const string cumulativeAxisKey = "cummulative";

        private LinearAxis cardsAxis;
        private LinearAxis cumulativeAxis;
        private CategoryAxis categoryAxis;

        public PlotModel ChartModel { get; private set; }

        public ForeCastChartViewModel(string title, string subTitle, List<DueForeCast> foreCasts)
        {
            if (foreCasts.Count == 0)
                return;

            this.ChartModel = new PlotModel
            {                
                Title = title,
                Subtitle = subTitle,
                LegendPlacement = LegendPlacement.Outside,
                LegendPosition = LegendPosition.BottomCenter,
                LegendOrientation = LegendOrientation.Horizontal,
                LegendBorderThickness = 0,
                Background = OxyColors.Transparent
            };

            var youngSeries = new ColumnSeries
            {
                Title = "Young",
                IsStacked = true,
                FillColor = OxyColors.LightGreen,
                StrokeThickness = 0
            };
            var matureSeries = new ColumnSeries
            {
                Title = "Mature",
                IsStacked = true,
                FillColor = OxyColors.DarkOliveGreen,
                StrokeThickness = 0
            };
            var cumulativeSeries = new LineSeries
            {
                Title = "Cumulative",
                Color = OxyColors.Blue,
                Smooth = true,
                TrackerFormatString = "{3}: {4:0}",
                YAxisKey = cumulativeAxisKey
            };

            categoryAxis = new CategoryAxis
            {
                Position = AxisPosition.Bottom,
                AbsoluteMaximum = foreCasts.Count + 1,
                AbsoluteMinimum = -1,
                MajorStep = foreCasts.Count / 5 + 1,
            };

            int maxYAxis = 0;
            int max;
            int total = 0;
            string affix = Stats.AffixDict[Stats.TimeType];
            foreach (var f in foreCasts)
            {
                youngSeries.Items.Add(new ColumnItem(f.Young));
                matureSeries.Items.Add(new ColumnItem(f.Mature));
                total += f.Young + f.Mature;
                cumulativeSeries.Points.Add(new DataPoint(f.Day, total));

                string catergory = f.Day.ToString() + affix;
                categoryAxis.Labels.Add(catergory);

                max = (f.Mature > f.Young) ? f.Mature : f.Young;
                if (maxYAxis < max)
                    maxYAxis = max;
            }

            cardsAxis = new LinearAxis
            {
                Position = AxisPosition.Left,
                Title = "Cards",
                MinimumPadding = 0.1,
                MaximumPadding = 0.6,
                MajorStep = maxYAxis / 5 + 1,
                MinorTickSize = 0,
                AbsoluteMinimum = 0,
                AbsoluteMaximum = maxYAxis + 1
            };

            cumulativeAxis = new LinearAxis
            {
                Position = AxisPosition.Right,
                Title = "Cumulative Cards",                
                Key = cumulativeAxisKey,
                MinimumPadding = 0.1,
                MaximumPadding = 0.6,
                MajorStep = total / 5 + 1,
                MinorTickSize = 0,
                AbsoluteMinimum = 0,
                AbsoluteMaximum = total + 1
            };

            ChartModel.Axes.Add(categoryAxis);
            ChartModel.Axes.Add(cardsAxis);
            ChartModel.Series.Add(matureSeries);
            ChartModel.Series.Add(youngSeries);
            ChartModel.Axes.Add(cumulativeAxis);
            ChartModel.Series.Add(cumulativeSeries);  
        }


    }
}
