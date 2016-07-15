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
    public class ReviewChartViewModel : IPlotChartModel
    {
        private const string TRACKER_FORMAT = "{0}\n{1}: {2:0}\n{3}: {4:0}";
        private const string cumulativeAxisKey = "cummulative";

        public PlotModel ChartModel { get; private set; }

        private int totalYoung;
        private int totalMature;
        private int totalLearn;
        private int totalRelearn;
        private int totalCram;

        public int TotalReviews()
        {
            return totalYoung + totalMature + totalLearn + totalRelearn + totalCram;
        }

        public double RelearnRatio()
        {
            var total = totalYoung + totalMature;
            if (total == 0)
                return 0;

            return (double)totalRelearn / (totalYoung + totalMature);
        }

        public ReviewChartViewModel(string title, string subTitle, List<ReviewData> reviewData)
        {
            if (reviewData.Count == 0)
                return;

            this.ChartModel = new PlotModel
            {
                Title = title,
                Subtitle = subTitle,                
                LegendPlacement = LegendPlacement.Inside,
                LegendPosition = LegendPosition.LeftTop,
                LegendOrientation = LegendOrientation.Vertical,
                LegendBorderThickness = 0,
                Background = OxyColors.Transparent,
            };
            ChartModel.TitleHorizontalAlignment = TitleHorizontalAlignment.CenteredWithinView;

            var youngCumulative = new LineSeries
            {
                Title = "Young",
                Color = OxyColors.LightGreen,
                Smooth = true,
                TrackerFormatString = TRACKER_FORMAT,
                YAxisKey = cumulativeAxisKey
            };
            var matureCumulative = new LineSeries
            {
                Title = "Mature",
                Color = OxyColors.DarkOliveGreen,
                Smooth = true,
                TrackerFormatString = TRACKER_FORMAT,
                YAxisKey = cumulativeAxisKey
            };
            var learnCumulative = new LineSeries
            {
                Title = "Learn",
                Color = OxyColors.Blue,
                Smooth = true,
                TrackerFormatString = TRACKER_FORMAT,
                YAxisKey = cumulativeAxisKey
            };
            var relearnCumulative = new LineSeries
            {
                Title = "Relearn",
                Color = OxyColors.Orange,
                Smooth = true,
                TrackerFormatString = TRACKER_FORMAT,
                YAxisKey = cumulativeAxisKey
            };
            var cramCumulative = new LineSeries
            {
                Title = "Cram",
                Color = OxyColors.Gold,
                Smooth = true,
                TrackerFormatString = TRACKER_FORMAT,
                YAxisKey = cumulativeAxisKey
            };

            string affix = Stats.AffixDict[Stats.TimeType];
            foreach (var r in reviewData)
            {
                totalYoung += r.Young;
                youngCumulative.Points.Add(new DataPoint(r.Day, totalYoung));

                totalMature += r.Mature;
                matureCumulative.Points.Add(new DataPoint(r.Day, totalMature));

                totalLearn += r.Learn;
                learnCumulative.Points.Add(new DataPoint(r.Day, totalLearn));

                totalRelearn += r.Relearn;
                relearnCumulative.Points.Add(new DataPoint(r.Day, totalRelearn));

                totalCram += r.Cram;
                cramCumulative.Points.Add(new DataPoint(r.Day, totalCram));
            }

            var max = (new int[] { totalYoung, totalMature, totalLearn, totalRelearn, totalCram }).Max();
            var cumulativeAxis = new LinearAxis
            {
                Position = AxisPosition.Right,
                Title = "Cumulative Answers",
                Key = cumulativeAxisKey,
                MinimumPadding = 0.1,
                MaximumPadding = 0.6,
                MajorStep = max / 5 + 1,
                MinorTickSize = 0,
                AbsoluteMinimum = 0,
                AbsoluteMaximum = max + 1
            };

            var range = reviewData[reviewData.Count - 1].Day - reviewData[0].Day;
            var xAxis = new LinearAxis
            {
                Position = AxisPosition.Bottom,
                Title = Stats.TimeNameDict[Stats.TimeType],
                MajorStep = range / 5 + 1,
                MinorTickSize = 0,
                AbsoluteMinimum = reviewData[0].Day - 1,
                AbsoluteMaximum = reviewData[reviewData.Count - 1].Day + 1
            };

            ChartModel.Axes.Add(cumulativeAxis);
            ChartModel.Axes.Add(xAxis);
            AddSeries(youngCumulative, matureCumulative, 
                learnCumulative, relearnCumulative, cramCumulative);
        }

        private void AddSeries(LineSeries youngCumulative, LineSeries matureCumulative, 
            LineSeries learnCumulative, LineSeries relearnCumulative, LineSeries cramCumulative)
        {
            if (totalYoung > 0)
                ChartModel.Series.Add(youngCumulative);

            if (totalMature > 0)
                ChartModel.Series.Add(matureCumulative);

            if (totalLearn > 0)
                ChartModel.Series.Add(learnCumulative);

            if (totalRelearn > 0)
                ChartModel.Series.Add(relearnCumulative);

            if (totalCram > 0)
                ChartModel.Series.Add(cramCumulative);
        }

    }
}
