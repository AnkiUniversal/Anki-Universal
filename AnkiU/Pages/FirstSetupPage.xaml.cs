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
using AnkiU.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Composition;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Hosting;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace AnkiU.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class FirstSetupPage : Page
    {
        private const int MIN_SECONDS_SHOW_FIRST_PAGE = 5;
        private TimeSpan timeStartShowing;

        private MainPage mainPage;

        public FirstSetupPage()
        {
            this.InitializeComponent();
            QuoteFadeOut.Completed += QuoteFadeOutCompletedHandler;
        }

        private void QuoteFadeOutCompletedHandler(object sender, object e)
        {
            quoteRoot.Visibility = Visibility.Collapsed;
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            try
            {
                base.OnNavigatedTo(e);
                mainPage = e.Parameter as MainPage;                
                mainPage.InitCollectionFinished += InitCollectionFinishedHandler;
                this.NavigationCacheMode = NavigationCacheMode.Disabled;
                QuoteFadeIn.Begin();
                timeStartShowing = DateTimeOffset.Now.TimeOfDay;
            }
            catch(Exception ex)
            {
                await UIHelper.ShowMessageDialog(ex.Message, "Failed to navigate to FirstPage");
            }
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            mainPage.InitCollectionFinished -= InitCollectionFinishedHandler;
            base.OnNavigatedFrom(e);
            mainPage.ContentFrame.BackStack.RemoveAt(0);
        }

        private async void InitCollectionFinishedHandler()
        {
            await mainPage.CurrentDispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                var elapseTime = DateTimeOffset.Now.TimeOfDay - timeStartShowing;
                if(elapseTime.Seconds < MIN_SECONDS_SHOW_FIRST_PAGE)
                {
                    var timeWait = (MIN_SECONDS_SHOW_FIRST_PAGE - elapseTime.Seconds)*1000;
                    await Task.Delay(timeWait);
                }
                MainPage.UserPrefs.IsFirstTimeOpenApp = false;
                mainPage.UpdateUserPreference();
                showProgessRoot.Visibility = Visibility.Collapsed;
                welcomRoot.Visibility = Visibility.Visible;
                QuoteFadeOut.Begin();                             
                WelcomFadeIn.Begin();                                
            });
        }

        private async void ViewTutorialClick(object sender, RoutedEventArgs e)
        {
            AllHelps.Tutorial = AllHelps.TutorialState.DeckCreation;
            await GotoDeckSelectPage();
        }

        private async void SkipTutorialClick(object sender, RoutedEventArgs e)
        {
            AllHelps.Tutorial = AllHelps.TutorialState.NotShow;
            await GotoDeckSelectPage();
        }

        private async Task GotoDeckSelectPage()
        {
            mainPage.ChangeStatusAndCommanBarColorMode();
            await mainPage.NavigateToDeckSelectPage();
        }

        private void PageSizeChanged(object sender, SizeChangedEventArgs e)
        {
            double width = e.NewSize.Width;
            double height = e.NewSize.Height;
            DeckOptionsPage.ScaleWithWindow(width, height, logoScale);
            DeckOptionsPage.ScaleWithWindow(width, height, welcomScale);
            DeckOptionsPage.ScaleWithWindow(width, height, quoteScale);
            DeckOptionsPage.ScaleWithWindow(width, height, progressScale);
        }
    }
}
