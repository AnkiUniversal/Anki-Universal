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
using AnkiU.UIUtilities;
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
using System.Threading.Tasks;
using Windows.UI.Core;
using AnkiU.Models;
using AnkiU.ViewModels;

namespace AnkiU.Views
{
    public sealed partial class CardInformationView : UserControl, IZoom
    {        
        private const int DEFAULT_FONTSIZE = 16;

        private bool isPointerPressed = false;
        private MenuFlyout rightColumnMenuFlyout = null;
        private MenuFlyout leftColumnMenuFlyout = null;

        public FrameworkElement PointToShowFlyout { get { return pointToShowFlyout; } }
        public MenuFlyout CardListViewMenuFlyout { get; set; }
        public CardInformation CardShowMenuFlyout { get; set; }
        public ListView CardListView { get { return cardListView; } }

        public bool IsSortFieldColumnReverse { get; set; } = false;
        public bool IsQuestionColumReverse { get; set; } = false;
        public bool IsAnswerColumReverse { get; set; } = false;
        public bool IsDueColumReverse { get; set; } = false;
        public bool IsLapseColumReverse { get; set; } = false;

        public SearchSortColumn CurrentSortColumn { get; set; } = SearchSortColumn.Question;

        public double ZoomLevel { get; set; }

        public bool IsSave { get { return false; } }

        public delegate void ResultViewChange(SearchSortColumn column, bool isReverse);
        public event ResultViewChange SortColumnChangedEvent;        

        public CardInformationView()
        {
            this.InitializeComponent();
            ZoomLevel = 1;
        }   

        public Task ChangeZoomLevel(double value)
        {
            var task = CoreWindow.GetForCurrentThread().Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                userControl.FontSize = value * DEFAULT_FONTSIZE;
            });
            return task.AsTask();
        }

        private void QuestionColumnSortOrderButtonClickHandler(object sender, RoutedEventArgs e)
        {
            Grid.SetColumn(currentSortBorder, 0);
            Grid.SetColumnSpan(currentSortBorder, 1);
            currentSortBorder.Visibility = Visibility.Visible;
            if (CurrentSortColumn != SearchSortColumn.Question)
            {
                CurrentSortColumn = SearchSortColumn.Question;
                SortColumnChangedEvent.Invoke(CurrentSortColumn, IsQuestionColumReverse);
                return;
            }

            IsQuestionColumReverse = !IsQuestionColumReverse;
            RotateSymbol(questionColumSortOrderSymbolTrans, IsQuestionColumReverse);
            SortColumnChangedEvent.Invoke(CurrentSortColumn, IsQuestionColumReverse);
        }

        private void AnswerMenuFlyoutItemClickHandler(object sender, RoutedEventArgs e)
        {
            answerLabel.Visibility = Visibility.Visible;
            if (CurrentSortColumn == SearchSortColumn.Due || CurrentSortColumn == SearchSortColumn.Lapse)
                currentSortBorder.Visibility = Visibility.Collapsed;

            dueLabel.Visibility = Visibility.Collapsed;
            lapsesLabel.Visibility = Visibility.Collapsed;           
        }

        private void DueAndRelearnMenuFlyoutItemClickHandler(object sender, RoutedEventArgs e)
        {
            answerLabel.Visibility = Visibility.Collapsed;
            if (CurrentSortColumn == SearchSortColumn.Answer)
                currentSortBorder.Visibility = Visibility.Collapsed;

            dueLabel.Visibility = Visibility.Visible;
            lapsesLabel.Visibility = Visibility.Visible;           
        }

        private void AnswerSortOrderButtonClickHandler(object sender, RoutedEventArgs e)
        {
            Grid.SetColumn(currentSortBorder, 1);
            Grid.SetColumnSpan(currentSortBorder, 2);
            currentSortBorder.Visibility = Visibility.Visible;
            if (CurrentSortColumn != SearchSortColumn.Answer)
            {
                CurrentSortColumn = SearchSortColumn.Answer;
                SortColumnChangedEvent.Invoke(CurrentSortColumn, IsAnswerColumReverse);
                return;
            }

            IsAnswerColumReverse = !IsAnswerColumReverse;
            RotateSymbol(answerSortOrderSymbolTrans, IsAnswerColumReverse);
            SortColumnChangedEvent.Invoke(CurrentSortColumn, IsAnswerColumReverse);
        }

        private void DueSortOrderButtonClickHandler(object sender, RoutedEventArgs e)
        {
            Grid.SetColumn(currentSortBorder, 1);
            Grid.SetColumnSpan(currentSortBorder, 1);
            currentSortBorder.Visibility = Visibility.Visible;
            if (CurrentSortColumn != SearchSortColumn.Due)
            {
                CurrentSortColumn = SearchSortColumn.Due;
                SortColumnChangedEvent.Invoke(CurrentSortColumn, IsDueColumReverse);
                return;
            }

            IsDueColumReverse = !IsDueColumReverse;
            RotateSymbol(dueSortOrderSymbolTrans, IsDueColumReverse);
            SortColumnChangedEvent.Invoke(CurrentSortColumn, IsDueColumReverse);
        }

        private void LapseSortOrderButtonClickHandler(object sender, RoutedEventArgs e)
        {
            Grid.SetColumn(currentSortBorder, 2);
            Grid.SetColumnSpan(currentSortBorder, 1);
            currentSortBorder.Visibility = Visibility.Visible;
            if (CurrentSortColumn != SearchSortColumn.Lapse)
            {
                CurrentSortColumn = SearchSortColumn.Lapse;
                SortColumnChangedEvent.Invoke(CurrentSortColumn, IsLapseColumReverse);
                return;
            }

            IsLapseColumReverse = !IsLapseColumReverse;
            RotateSymbol(lapseSortOrderSymbolTrans, IsLapseColumReverse);
            SortColumnChangedEvent.Invoke(CurrentSortColumn, IsLapseColumReverse);
        }

        private void RotateSymbol(CompositeTransform transform, bool isReverse)
        {
            if (isReverse)
                transform.Rotation = 180;
            else
                transform.Rotation = 0;
        }

        private void RightColumnButtonRightTappedHandler(object sender, RightTappedRoutedEventArgs e)
        {
            var element = sender as UIElement;

            if (isPointerPressed)
            {
                if (rightColumnMenuFlyout == null)
                    rightColumnMenuFlyout = Resources["RightColumnContextMenu"] as MenuFlyout;

                rightColumnMenuFlyout.ShowAt(element, e.GetPosition(element));
                e.Handled = true;
            }
        }

        private void PointerPressedHandler(object sender, PointerRoutedEventArgs e)
        {
            isPointerPressed = true;
        }

        private void RightColumnButtonHoldingHandler(object sender, HoldingRoutedEventArgs e)
        {
            var element = sender as UIElement;

            if (e.HoldingState == Windows.UI.Input.HoldingState.Started)
            {
                if (rightColumnMenuFlyout == null)
                    rightColumnMenuFlyout = Resources["RightColumnContextMenu"] as MenuFlyout;

                rightColumnMenuFlyout.ShowAt(element, e.GetPosition(element));
                e.Handled = true;

                // This, combined with a check in OnRightTapped prevents the firing of RightTapped from
                // launching another context menu
                isPointerPressed = false;
            }
        }

        private void CardListPointerPressedHandler(object sender, PointerRoutedEventArgs e)
        {
            isPointerPressed = true;
        }

        private void CardListHoldingHandler(object sender, HoldingRoutedEventArgs e)
        {            
            if (e.HoldingState == Windows.UI.Input.HoldingState.Started)
            {
                CardListViewMenuFlyout.ShowAt(null, e.GetPosition(null));
                CardShowMenuFlyout = (e.OriginalSource as FrameworkElement).DataContext as CardInformation;                
                e.Handled = true;
                isPointerPressed = false;

                var pointerPosition = e.GetPosition(mainGrid);
                pointToShowFlyout.Margin = new Thickness(pointerPosition.X, pointerPosition.Y, 0, 0);
            }
        }

        private void CardListRightTappedHandler(object sender, RightTappedRoutedEventArgs e)
        {
            if (isPointerPressed)
            {
                CardShowMenuFlyout = (e.OriginalSource as FrameworkElement).DataContext as CardInformation;
                CardListViewMenuFlyout.ShowAt(null, e.GetPosition(null));
                e.Handled = true;

                var pointerPosition = e.GetPosition(mainGrid);
                pointToShowFlyout.Margin = new Thickness(pointerPosition.X, pointerPosition.Y, 0, 0);
            }
        }

        private void LeftColumnPointerPressedHandler(object sender, PointerRoutedEventArgs e)
        {
            isPointerPressed = true;
        }

        private void LeftColumnHoldingHandler(object sender, HoldingRoutedEventArgs e)
        {
            var element = sender as UIElement;

            if (e.HoldingState == Windows.UI.Input.HoldingState.Started)
            {
                if (leftColumnMenuFlyout == null)
                    leftColumnMenuFlyout = Resources["LeftColumnContextMenu"] as MenuFlyout;

                leftColumnMenuFlyout.ShowAt(element, e.GetPosition(element));
                e.Handled = true;

                isPointerPressed = false;
            }
        }

        private void LeftColumnRightTapped(object sender, RightTappedRoutedEventArgs e)
        {
            var element = sender as UIElement;

            if (isPointerPressed)
            {
                if (leftColumnMenuFlyout == null)
                    leftColumnMenuFlyout = Resources["LeftColumnContextMenu"] as MenuFlyout;

                leftColumnMenuFlyout.ShowAt(element, e.GetPosition(element));
                e.Handled = true;
            }
        }

        private void SortFieldButtonClickHandler(object sender, RoutedEventArgs e)
        {
            Grid.SetColumn(currentSortBorder, 0);
            Grid.SetColumnSpan(currentSortBorder, 1);
            currentSortBorder.Visibility = Visibility.Visible;
            if (CurrentSortColumn != SearchSortColumn.SortField)
            {
                CurrentSortColumn = SearchSortColumn.SortField;
                SortColumnChangedEvent.Invoke(CurrentSortColumn, IsSortFieldColumnReverse);
                return;
            }

            IsSortFieldColumnReverse = !IsSortFieldColumnReverse;
            RotateSymbol(sortFieldColumSortOrderSymbolTrans, IsSortFieldColumnReverse);
            SortColumnChangedEvent.Invoke(CurrentSortColumn, IsSortFieldColumnReverse);
        }

        private void QuestionSortMenuClick(object sender, RoutedEventArgs e)
        {
            questionButton.Visibility = Visibility.Visible;
            if (CurrentSortColumn == SearchSortColumn.SortField)
                currentSortBorder.Visibility = Visibility.Collapsed;

            sortFieldButton.Visibility = Visibility.Collapsed;            
        }

        private void SortFieldMenuFlyoutItemClickHandler(object sender, RoutedEventArgs e)
        {
            sortFieldButton.Visibility = Visibility.Visible;
            if (CurrentSortColumn == SearchSortColumn.Question)
                currentSortBorder.Visibility = Visibility.Collapsed;

            questionButton.Visibility = Visibility.Collapsed;
        }
    }
}

