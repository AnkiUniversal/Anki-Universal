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
using AnkiU.AnkiCore;
using System.Threading.Tasks;
using Windows.Foundation.Metadata;
using Windows.UI.Core;
using AnkiRuntimeComponent;
using System.Runtime.CompilerServices;
using AnkiU.Anki;
using AnkiU.Interfaces;
using AnkiU.UIUtilities;
using Windows.Storage;
using Windows.ApplicationModel;

namespace AnkiU.UserControls
{
    public sealed partial class CardView : UserControl, IUserInputString, INightReadMode, IZoom
    {
        public const string HTML_PATH = "/html/card.html";

        public delegate void KeyDownEventHandler(Windows.System.VirtualKey key);
        public delegate void CardViewLoadHandler();

        public event KeyDownEventHandler KeyDownMappingEvent;
        /// <summary>
        /// WARINING: Use this event to know that all webview related stuffs (function, etc.) have been
        /// properly loaded before accessing them.
        /// </summary>
        public event CardViewLoadHandler CardHtmlLoadedEvent;
        public event CardViewLoadHandler NavigateToWebsiteStartEvent;

        private static string currentDeviceFamily = Windows.System.Profile.AnalyticsInfo.VersionInfo.DeviceFamily;

        private WebView webViewControl;
        public WebView WebViewControl { get { return webViewControl; } }

        private bool isWebViewReady = false;
        public bool IsWebViewRead { get { return isWebViewReady; } }

        private bool isNavigateToErrorPage = false;
        private string restoredCardStyle;
        private KeyPassingWebToWinRT keyNotify;
        private string deckMediaFolder;

        public bool IsBrowseWebOnCard { get; set; } = false;

        public double ZoomLevel { get; set; }

        public bool IsSave
        {
            get
            {
                return true;
            }
        }

        private bool isNightMode;

        public CardView()
        {
            this.InitializeComponent();
            InitWebView();
            keyNotify = new KeyPassingWebToWinRT();
            keyNotify.KeyDownEvent += KeyDownEventFire;
        }

        public void InitWebView()
        {
            webViewControl = new WebView();
            ScrollViewer.SetVerticalScrollBarVisibility(webViewControl, ScrollBarVisibility.Hidden);
            webViewGrid.Children.Add(webViewControl);
            HookWebViewEvents();
        }

        private void HookWebViewEvents()
        {
            webViewControl.NavigationStarting += WebViewControlNavigationStartingHandler;
            webViewControl.NavigationCompleted += WebViewControlNavigationCompletedHandler;
            webViewControl.LongRunningScriptDetected += (a, s) => { NavigateToErrorPage(); };
            webViewControl.NavigationFailed += (a, s) => { NavigateToErrorPage(); };
            webViewControl.UnviewableContentIdentified += (a, s) => { NavigateToErrorPage(); };
            webViewControl.UnsupportedUriSchemeIdentified += (a, s) => { NavigateToErrorPage(); };
            webViewControl.UnsafeContentWarningDisplaying += (a, s) => { NavigateToErrorPage(); };
        }

        private void NavigateToErrorPage()
        {
            NavigateToBlankPage();
            isNavigateToErrorPage = true;
        }

        public void NavigateToBlankPage()
        {
            HtmlHelpter.LoadLocalHtml(webViewControl, HTML_PATH);
        }

        /// <summary>
        /// Until Microsoft fix their caching problem,
        /// This function is very IMPORTANT to clear webview cache
        /// It will have to be used each time user stop viewing deck
        /// </summary>
        public void ClearWebViewControl()
        {
            isWebViewReady = false;
            webViewGrid.Children.Clear();
            webViewControl = null;
            GC.Collect();
        }

        private async void WebViewControlNavigationCompletedHandler(WebView sender, WebViewNavigationCompletedEventArgs args)
        {
            isWebViewReady = true;
            await ChangeReadMode();            
            await SetTouchInput(!UIHelper.IsHasPhysicalKeyboard());

            if (!isNavigateToErrorPage)
            {                
                CardHtmlLoadedEvent?.Invoke();
            }
            else
            {
                try
                {
                    string html = await MainPage.LoadStringFromPackageFileAsync("/html/error.html");
                    await ChangeDeckMediaFolder(deckMediaFolder);
                    await ChangeCardStyle(restoredCardStyle);
                    await ChangeCardContent(html, "Card");
                    isNavigateToErrorPage = false;
                }
                catch { }
            }
        }

        private async void WebViewControlNavigationStartingHandler(WebView sender, WebViewNavigationStartingEventArgs args)
        {
            if (args.Uri.AbsoluteUri.Contains("http"))
            {
                if (!IsBrowseWebOnCard)
                {
                    //Do not use Stop() function of WebView here
                    //as it will fire  NavigationStarting a second time
                    args.Cancel = true;
                    await Windows.System.Launcher.LaunchUriAsync(args.Uri);
                }
                else
                    NavigateToWebsiteStartEvent?.Invoke();
            }
            else
            {
                if (!IsValidLocalFilePath(args.Uri.LocalPath))
                {
                    //Don't navigate to unvalid path
                    args.Cancel = true;
                    return;
                }

                //Only inject object into local html to avoid being attacked by unsafe-websites
                //Inject keyNotify into javascript with the name as notifyAppObj                    
                webViewControl.AddWebAllowedObject("notifyAppObj", keyNotify);
            }
        }

        private bool IsValidLocalFilePath(string localPath)
        {
            return File.Exists(Package.Current.InstalledLocation.Path + localPath);
        }

        private void UserControlLoadedHandler(object sender, RoutedEventArgs e)
        {
            HtmlHelpter.LoadLocalHtml(webViewControl, HTML_PATH);
        }

        public void ReloadCardView()
        {
            InitWebView();
            HtmlHelpter.LoadLocalHtml(webViewControl, HTML_PATH);
        }

        public void KeyDownEventFire(int keyCode)
        {
            var key = (Windows.System.VirtualKey)keyCode;
            KeyDownMappingEvent?.Invoke(key);
        }

        public async Task ChangeDeckMediaFolder(string path)
        {
            try
            {
                deckMediaFolder = path;
                await webViewControl.InvokeScriptAsync("ChangeDeckMediaFolder", new string[] { path });
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }

        public async Task ChangeCardContent(string newContent, string cardClass = null)
        {
            try
            {
                await webViewControl.InvokeScriptAsync("ChangeCardContent", new string[] { newContent, cardClass });
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }

        public async Task ChangeCardStyle(string newStyle)
        {
            try
            {
                restoredCardStyle = newStyle;
                await webViewControl.InvokeScriptAsync("ChangeCardStyle", new string[] { restoredCardStyle });
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult, "ChangeCardStyle");
            }
        }

        public async Task PlayAllMedia()
        {
            try
            {
                await webViewControl.InvokeScriptAsync("PlayAllMedia", null);
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult, "PlayAllMedia");
            }
        }

        private async Task ChangeReadMode()
        {
            try
            {
                string readMode;
                if (isNightMode)
                    readMode = "night";
                else
                    readMode = "day";

                await webViewControl.InvokeScriptAsync("ChangeReadMode", new string[] { readMode });
            }
            catch (Exception ex)
            {
               UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }

        public async Task<string> GetInput()
        {
            try
            {
               return await webViewControl.InvokeScriptAsync("GetUserInputString", null);
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
            //This code won't be reached but visual studio will 
            //consider it as an error if not all code patch return a value
            return null;
        }

        public async void ToggleReadMode()
        {
            isNightMode = !isNightMode;
            if (isWebViewReady)
                await ChangeReadMode();
        }

        public async Task ChangeZoomLevel(double value)
        {
            try
            {
                await webViewControl.InvokeScriptAsync("ChangeBodyZoom", new string[] { value.ToString() });
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }

        private async Task SetTouchInput(bool value)
        {
            try
            {
                string arg = value ? "true" : "false";
                await webViewControl.InvokeScriptAsync("SetTouchInput", new string[] { arg });
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }
    }
}


