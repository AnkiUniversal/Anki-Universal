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
using AnkiU.Models;
using AnkiU.UIUtilities;
using AnkiU.ViewModels;
using OxyPlot;
using OxyPlot.Series;
using OxyPlot.Windows;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Data.Pdf;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI;

namespace AnkiU.Pages
{ 
    public sealed partial class StatsPage : Page, INightReadMode
    {
        private const double DEFAULT_SCREEN_RATIO = 0.5;

        private const string TODAY_TEXT = "Studied {0} cards in {1} seconds today.\n" +
                                          "Again count: {2}. Learn: {3}. Review: {4}.\n Relearn: {5}. Custom study: {6}.";

        private bool isNightMode = false;
        private MainPage mainPage;
        private Collection collection;

        private List<PlotView> plotViews = new List<PlotView>();
        private PlotController controller = new PlotController();

        private List<IPlotChartModel> plotModels = new List<IPlotChartModel>();
        private ForeCastChartViewModel foreCastPlotViewModel;
        private ReviewChartViewModel reviewPlotViewModel;
        private CardStatesViewModel cardTypePlotViewModel;

        private List<Stats.DueForeCast> dueForeCast;
        private List<Stats.ReviewData> reviewData;
        private Stats.CardStatesData cardStates;
        private Stats.TodayStats todayStats;
        private DeckNameViewModel deckNameViewModel;

        public StatsPage()
        {
            this.InitializeComponent();            
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            mainPage = e.Parameter as MainPage;
            if (mainPage == null)
                throw new Exception("Wrong input parameter!");
            collection = mainPage.Collection;

            Stats.TimeType = Stats.TimeRangeType.MONTH;

            SetupDeckSelection();           
        }

        private void SetupDeckSelection()
        {
            deckNameViewModel = new DeckNameViewModel(collection);
            deckNameView.DataContext = deckNameViewModel.Decks;            
            if (Stats.IsWholeCollection)
            {
                deckNameView.ChangeSelectedItem(DeckNameViewModel.ALL_DECKS_ID);
            }
            else
                deckNameView.ChangeSelectedItem(collection.Deck.Selected());
            deckNameView.SelectionChangedEvent += SelectionChangedhandler;
        }

        private void PageLoaded(object sender, RoutedEventArgs e)
        {
            UpdatePlotHeight();
            ShowInProgressRing();

            Task.Run(async () =>
            {
                GetAllStats();

                await mainPage.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    UpdateAllStats();
                    AddPlotchartModelAndView();
                    BindCommandToPlotViews();

                    HookAllMainpageEvents();
                    monthRadioButton.Checked += OneMonthCheckedHandler;
                    HideInProgressRing();
                    PreventTouchInteractWithPlotViews();
                });
            });
        }

        private void BindCommandToPlotViews()
        {
            controller.UnbindAll();
            controller.BindMouseDown(OxyMouseButton.Left, PlotCommands.Track);
            
            foreCastPlotView.Controller = controller;
            reviewPlotView.Controller = controller;
            cardTypePlotView.Controller = controller;
        }

        private void UpdateAllStats()
        {
            UpdateTodayStats();
            UpdatForeCastDueChart();
            UpdateReviewChart();
            UpdateCardStatesChart();
        }

        private void UpdateCardStatesChart()
        {            
            cardTypePlotViewModel = new CardStatesViewModel("CARD TYPES", "The division of cards in your deck(s)", cardStates);
            if(cardTypePlotViewModel.ChartModel == null)
            {
                HideView(cardTypePlotView);
                cardTypePlotView.Model = null;
                return;
            }
            ShowView(cardTypePlotView, 3);
            cardTypePlotView.Model = cardTypePlotViewModel.ChartModel;            
        }

        private void UpdateReviewChart()
        {                        
            reviewPlotViewModel = new ReviewChartViewModel("ANSWER COUNT", "The number of questions you have answered.", reviewData);            
            if (reviewPlotViewModel.ChartModel == null)
            {
                HideView(reviewPlotRoot);
                reviewPlotView.Model = null;
                return;
            }
            ShowView(reviewPlotRoot, 2);

            reviewPlotView.Model = reviewPlotViewModel.ChartModel;
            totalReviewTextBlock.Text = reviewPlotViewModel.TotalReviews().ToString();
            relearnRatio.Text = String.Format("{0:0.#}", reviewPlotViewModel.RelearnRatio() * 100) + "%";                   
        }

        private void UpdatForeCastDueChart()
        {            
            foreCastPlotViewModel = new ForeCastChartViewModel("FORECAST", "The number of reviews due in the future.", dueForeCast);
            if (foreCastPlotViewModel.ChartModel == null)
            {
                HideView(foreCastPlotView);
                foreCastPlotView.Model = null;
                return;
            }
            ShowView(foreCastPlotView, 1);
            foreCastPlotView.Model = foreCastPlotViewModel.ChartModel;
        }

        private void HideView(FrameworkElement view)
        {            
            //WARNING: To avoid weird behavior when toggling Visibility of OxyPlotView 
            //We hide inactive plot by moving them to an invisible row 
            Grid.SetRow(view, 4);
        }

        private void ShowView(FrameworkElement view, int position)
        {
            Grid.SetRow(view, position);
        }

        private void GetAllStats()
        {
            todayStats = Stats.GetTodayStats(collection);
            cardStates = Stats.GetCardStates(collection);
            reviewData = Stats.GetReviewCountAndTime(collection);
            dueForeCast = Stats.GetDueForeCast(collection);
        }

        private void UpdateTodayStats()
        {
            todayStatsTextBlock.Text = String.Format(TODAY_TEXT, todayStats.CardsToday, todayStats.Time, todayStats.Failed,
              todayStats.Learning, todayStats.Review, todayStats.Relearn, todayStats.Filter);
        }

        private void AddPlotchartModelAndView()
        {                              
            if (foreCastPlotViewModel.ChartModel != null)
            {                
                plotModels.Add(foreCastPlotViewModel);
                plotViews.Add(foreCastPlotView);
            }
            if (reviewPlotViewModel.ChartModel != null)
            {
                plotModels.Add(reviewPlotViewModel);
                plotViews.Add(reviewPlotView);
            }
            if (cardTypePlotViewModel.ChartModel != null)
            {
                plotModels.Add(cardTypePlotViewModel);
                plotViews.Add(cardTypePlotView);
            }
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            UnHookAllMainpageEvents();
            base.OnNavigatingFrom(e);
        }

        private void HookAllMainpageEvents()
        {
            mainPage.CommanBar.ClosedDisplayMode = AppBarClosedDisplayMode.Minimal;
            mainPage.ReadModeButtonSeparator.Visibility = Visibility.Collapsed;
            mainPage.EnableChangingReadMode(this);
            ChangeChartReadMode();
        }

        private void UnHookAllMainpageEvents()
        {
            mainPage.CommanBar.ClosedDisplayMode = AppBarClosedDisplayMode.Compact;
            mainPage.ReadModeButtonSeparator.Visibility = Visibility.Visible;
            mainPage.DisableChangingReadMode();
        }

        private void InvalidatePlotViews()
        {
            foreach (var view in plotViews)
                view.InvalidatePlot(true);
        }

        private void PageSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdatePlotHeight();
        }

        private void UpdatePlotHeight()
        {            
            foreCastPlotView.Height = DEFAULT_SCREEN_RATIO * page.ActualHeight;
            reviewPlotView.Height = DEFAULT_SCREEN_RATIO * page.ActualHeight;
            cardTypePlotView.Height = DEFAULT_SCREEN_RATIO * page.ActualHeight;
        }

        private void OneMonthCheckedHandler(object sender, RoutedEventArgs e)
        {
            Stats.TimeType = Stats.TimeRangeType.MONTH;
            UpdateTimeRangeCharts();
        }

        private void OneYearCheckedHandler(object sender, RoutedEventArgs e)
        {
            Stats.TimeType = Stats.TimeRangeType.YEAR;
            UpdateTimeRangeCharts();
        }

        private void LifeCheckedHandler(object sender, RoutedEventArgs e)
        {
            Stats.TimeType = Stats.TimeRangeType.LIFE;
            UpdateTimeRangeCharts();
        }

        private void UpdateTimeRangeCharts()
        {
            ShowInProgressRing();

            Task.Run(async () =>
            {
                dueForeCast = Stats.GetDueForeCast(collection);
                reviewData = Stats.GetReviewCountAndTime(collection);

                await mainPage.CurrentDispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    ClearPlortChartAndView();
                    UpdatForeCastDueChart();
                    UpdateReviewChart();
                    AddPlotchartModelAndView();
                    ChangeChartReadMode();

                    HideInProgressRing();
                });
            });
        }

        private void SelectionChangedhandler(object sender, SelectionChangedEventArgs e)
        {
            ShowInProgressRing();

            var combobox = sender as ComboBox;
            var deckId = (combobox.SelectedItem as DeckInformation).Id;
            if (deckId != DeckNameViewModel.ALL_DECKS_ID)
            {
                Stats.IsWholeCollection = false;
                collection.Deck.Select(deckId, false);
            }
            else
                Stats.IsWholeCollection = true;
            
            Task.Run(async () =>
            {                
                GetAllStats();

                await mainPage.CurrentDispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
                {
                    ClearPlortChartAndView();
                    UpdateAllStats();
                    AddPlotchartModelAndView();
                    ChangeChartReadMode();

                    HideInProgressRing();
                });
            });
        }

        private void HideInProgressRing()
        {
            chartsRoot.Opacity = 1;
            progressRing.IsActive = false;
            progressRing.Visibility = Visibility.Collapsed;
        }

        private void ShowInProgressRing()
        {
            chartsRoot.Opacity = 0.5;
            progressRing.Visibility = Visibility.Visible;
            progressRing.IsActive = true;
        }

        private void ClearPlortChartAndView()
        {
            plotViews.Clear();
            plotModels.Clear();
        }       

        private void PreventTouchInteractWithPlotViews()
        {
            if (!UIHelper.IsHasPhysicalMouse())
            {
                foreach (var view in plotViews)
                    view.IsHitTestVisible = false;
            }
        }

        public void ToggleReadMode()
        {
            isNightMode = !isNightMode;
            UIHelper.ToggleNightLight(isNightMode, this);
            ChangeChartReadMode();
        }

        private void ChangeChartReadMode()
        {
            if (isNightMode)
            {
                foreach (var model in plotModels)
                    UIHelper.ChangePlotModelToNight(model.ChartModel);
                statsTextRoot.Background = Application.Current.Resources["NightGray"] as SolidColorBrush;
                mainGrid.Background = new SolidColorBrush(Colors.Black);
            }
            else
            {
                foreach (var model in plotModels)
                    UIHelper.ChangePlotModelToDay(model.ChartModel);
                statsTextRoot.Background = new SolidColorBrush(Colors.White);
                mainGrid.Background = new SolidColorBrush(Colors.LightGray);
            }
            InvalidatePlotViews();
        }

    }
}
