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

namespace AnkiU.UserControls
{
    public sealed partial class HelpPopup : UserControl
    {        
        private RoutedEventHandler NextOrCloseFunc;

        public Image Image { get { return helpImage; } }
        public string Text { get { return helpTextBlock.Text; } set { helpTextBlock.Text = value; } }
        public string Title { get { return title.Text; } set { title.Text = value; } }
        public string SubTitle { get { return subTitle.Text; } set { subTitle.Text = value; } }

        public Visibility SubtitleVisibility { get { return subTitle.Visibility; } set { subTitle.Visibility = value; } }
        public Visibility CloseXVisibility { get { return closeButtonTop.Visibility; } set { closeButtonTop.Visibility = value; } }

        public bool IsLightDismissEnabled
        { get { return popUp.IsLightDismissEnabled; } set { popUp.IsLightDismissEnabled = value; } }

        public event NoticeRoutedHandler Closed;
        public NoticeRoutedHandler NextEvent { get; set; }
        public NoticeRoutedHandler BackEvent { get; set; }

        public HelpPopup()
        {
            this.InitializeComponent();
            closeButton.Click += CloseButtonClick;
            nextButton.Click += NextButtonClick;
            popUp.Closed += PopUpClosedHandler;
        }

        private void PopUpClosedHandler(object sender, object e)
        {
            Closed?.Invoke();
        }

        private void NextButtonClick(object sender, RoutedEventArgs e)
        {
            NextEvent?.Invoke();
        }

        private void CloseButtonClick(object sender, RoutedEventArgs e)
        {
            Hide();            
        }

        private void BackButtonClick(object sender, RoutedEventArgs e)
        {
            BackEvent?.Invoke();
        }

        private void NextOrCloseButtonClickHandler(object sender, RoutedEventArgs e)
        {
            NextOrCloseFunc(sender, e);
        }

        public void SetOffSet(double horizontal, double vertical)
        {
            popUp.HorizontalOffset = horizontal;
            popUp.VerticalOffset = vertical;
        }

        public void Show()
        {
            closeButton.Visibility = Visibility.Collapsed;
            nextButton.Visibility = Visibility.Collapsed;
            nextAndBackGrid.Visibility = Visibility.Collapsed;
            closeButtonTop.Visibility = Visibility.Collapsed;
            ShowPopup();
        }

        private void ShowPopup()
        {            
            popUp.IsOpen = true;
            FadeIn.Begin();
        }

        public void ShowWithClose()
        {
            closeButtonTop.Visibility = Visibility.Visible;
            closeButton.Visibility = Visibility.Visible;
            nextButton.Visibility = Visibility.Collapsed;
            nextAndBackGrid.Visibility = Visibility.Collapsed;

            ShowPopup();
        }

        public void ShowWithNext()
        {
            closeButton.Visibility = Visibility.Collapsed;            
            nextButton.Visibility = Visibility.Visible;
            nextAndBackGrid.Visibility = Visibility.Collapsed;

            ShowPopup();
        }

        public void ShowWithNextAndBack()
        {
            closeButton.Visibility = Visibility.Collapsed;
            nextButton.Visibility = Visibility.Collapsed;            
            nextAndBackGrid.Visibility = Visibility.Visible;
            nextOrCloseButton.Content = "Next";
            NextOrCloseFunc = NextButtonClick;

            ShowPopup();
        }

        public void ShowWithBackAndClose()
        {
            closeButton.Visibility = Visibility.Collapsed;
            nextButton.Visibility = Visibility.Collapsed;            
            nextAndBackGrid.Visibility = Visibility.Visible;
            closeButtonTop.Visibility = Visibility.Visible;
            nextOrCloseButton.Content = "Close";
            NextOrCloseFunc = CloseButtonClick;

            ShowPopup();
        }

        public void Hide()
        {
            popUp.IsOpen = false;
            popUp.Opacity = 0;       
        }

        public void ChangeReadMode(bool isNightMode)
        {
            if (isNightMode)
            {
                userControl.Background = UIHelper.CommandBarAcrylicDarkBrush;
                userControl.Foreground = new SolidColorBrush(Windows.UI.Colors.White);
            }
            else
            {
                userControl.Background = UIHelper.CommandBarAcrylicLightBrush;
                userControl.Foreground = new SolidColorBrush(Windows.UI.Colors.Black);
            }
        }        
    }
}
