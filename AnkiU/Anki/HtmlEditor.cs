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

using AnkiRuntimeComponent;
using AnkiU.AnkiCore;
using AnkiU.Interfaces;
using AnkiU.UIUtilities;
using AnkiU.UserControls;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace AnkiU.Anki
{    
    public class HtmlEditor : UserControl, INightReadMode
    {
        private CoreDispatcher dispatcher;
        private bool isNightMode = false;        

        private ButtonPassingWebToWinRT buttonEventNotify;
        private EditableFieldPassWebToWinRT editableFieldNotify;

        private ColorPicker foreColorPicker = null;
        private ColorPicker backColorPicker = null;

        private WebView webViewControl;
        public WebView WebViewControl { get { return webViewControl; } }

        public string htmlFilePath { get; set; }
        public string WindowSizeName { get; set; }
        public Grid webViewGrid;
        public FrameworkElement ContextMenuPlace { get; set; }
        public MenuFlyout MenuFlyout { get; set; }

        public bool IsWebviewReady { get; set; } = false;
        public bool IsEditableFieldPopulate { get; set; } = false;

        public bool IsModified { get; set; } = false;
        public bool IsContentCheckOnce { get; set; } = false;

        public event ClickEventHandler WebviewButtonClickEvent;
        public event EditableFieldRoutedEventHandler EditableFieldPasteEvent;
        public event EditableFieldChangedHandler EditableFieldTextChangedEvent;

        public delegate Task EditorRoutedEvent();
        public event EditorRoutedEvent FieldReadyToPopulateEvent;
        public event EditorRoutedEvent FieldPopulateFinishEvent;

        public HtmlEditor(Grid webViewGrid, FrameworkElement contextMenuPlace, MenuFlyout menuFlyout, string windowSizeName, string htmlFilePath, CoreDispatcher dispatcher)
        {
            this.webViewGrid = webViewGrid;            
            this.ContextMenuPlace = contextMenuPlace;
            this.WindowSizeName = windowSizeName;
            this.MenuFlyout = menuFlyout;
            this.htmlFilePath = htmlFilePath;
            this.dispatcher = dispatcher;

            AddWebViewControlIntoGrid();            

            buttonEventNotify = new ButtonPassingWebToWinRT();
            buttonEventNotify.ButtonClickEvent += ButtonEventNotifyClickEventHandler;

            editableFieldNotify = new EditableFieldPassWebToWinRT();
            editableFieldNotify.EditableFieldTextChangedEvent += EditableFieldTextChangedEventHandler;
            editableFieldNotify.EditableFieldPasteEvent += EditablePasteEventHandler;
            editableFieldNotify.EditableContextMenuEvent += EditableFieldContextMenuEventHandler;
        }

        private async void EditableFieldContextMenuEventHandler(object obj)
        {
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                var array = obj as object[];
                var offsetX = (double)array[0];
                var offsetY = (double)array[1];
                ContextMenuPlace.Margin = new Thickness(offsetX, offsetY, 0, 0);
                await Task.Delay(10);
                MenuFlyout.ShowAt(ContextMenuPlace, new Point(0, 0));
            });
        }

        private void ButtonEventNotifyClickEventHandler(object sender)
        {
            //Schedule to run this in another thread so javascript
            //can continue to excute its events
            Task.Run(() =>
            {
                string str = sender as string;
                if (str == "enter")
                {
                    var task = InsertLineBreak();
                }
                else
                    WebviewButtonClickEvent?.Invoke(sender);
            });
        }

        private void EditablePasteEventHandler()
        {
            EditableFieldPasteEvent?.Invoke();
        }

        protected virtual void EditableFieldTextChangedEventHandler(string fieldName, string html)
        {
            IsContentCheckOnce = true;
            //TODO: Considered changing the way we detect if a field do note have content
            // vs a field has content changed
            if (IsModified == false)
            {
                var text = RemoveDivWrap(html).Replace("&nbsp;", " ");
                if (!String.IsNullOrWhiteSpace(text))
                {
                    IsModified = true;
                }
            }
            EditableFieldTextChangedEvent?.Invoke(fieldName, html);
        }

        public static string RemoveDivWrap(string tinyMceText)
        {
            if (String.IsNullOrWhiteSpace(tinyMceText))
                return tinyMceText;

            if (!tinyMceText.StartsWith("<div>") || !tinyMceText.EndsWith("</div>"))
                return tinyMceText;

            //"<div>".Length = 5
            //"<div></div>".Length = 11
            return tinyMceText.Substring(5, tinyMceText.Length - 11);
        }

        private void AddWebViewControlIntoGrid()
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
        }

        private void WebViewControlNavigationStartingHandler(WebView sender, WebViewNavigationStartingEventArgs args)
        {
            //Inject bojects into javascript with the corresponding name 
            webViewControl.AddWebAllowedObject("buttonNotify", buttonEventNotify);
            webViewControl.AddWebAllowedObject("editableFieldNotify", editableFieldNotify);
        }

        private async void WebViewControlNavigationCompletedHandler(WebView sender, WebViewNavigationCompletedEventArgs args)
        {
            IsWebviewReady = true;
            if (UIHelper.IsHasPhysicalKeyboard())
                await IsTouchInput(false);
            else
                await IsTouchInput(true);

            await ChangeReadMode();

            if(FieldReadyToPopulateEvent != null)
                await FieldReadyToPopulateEvent();
            IsEditableFieldPopulate = true;

            await ChangeTinymceToolBarWidth(WindowSizeName);
            await InitRichTextEditor();

            if (FieldPopulateFinishEvent != null)
                await FieldPopulateFinishEvent();            
        }

        public void NavigateWebviewToLocalPage()
        {
            TinymceStreamUriWinRTResolver resolver = new TinymceStreamUriWinRTResolver();
            var localUri = webViewControl.BuildLocalStreamUri("HtmlEditor", htmlFilePath);
            webViewControl.NavigateToLocalStreamUri(localUri, resolver);
        }       

        public async Task PopulateAllEditableField(List<string> fields)
        {
            try
            {
                await webViewControl.InvokeScriptAsync("PopulateAllEditableField", fields);
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }

        public async Task InsertNewEditableField(string name, string content)
        {
            try
            {
                await webViewControl.InvokeScriptAsync("InsertNewEditableField", new string[] { name, content});
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }

        private async Task ClearBody()
        {
            try
            {
                await webViewControl.InvokeScriptAsync("ClearBody", null);
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }

        public async Task ChangeAllEditableFieldContent(string[] fields)
        {
            try
            {
                await webViewControl.InvokeScriptAsync("ChangeAllEditableFieldContent", fields);
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }

        public async Task ChangeMediaFolder(string path)
        {
            try
            {
                await webViewControl.InvokeScriptAsync("ChangeMediaFolder", new string[] { path });
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }

        /// <summary>
        /// Until Microsoft fix their caching problem,
        /// This function is very IMPORTANT to clear webview cache
        /// It will have to be used each time user stop viewing deck
        /// </summary>
        public void ClearWebViewControl()
        {
            //Make sure all richeditor are removed so we won't have memory leak
            var task = RemoveAllEditor();
            IsWebviewReady = false;            
            webViewGrid.Children.Clear();            
            webViewControl = null;
            GC.Collect();
        }

        public async Task InitRichTextEditor()
        {
            try
            {
                await webViewControl.InvokeScriptAsync("InitRichTextEditor", null);
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }

        private async Task ChangeReadMode()
        {
            try
            {
                string mode;
                if (isNightMode)
                    mode = "night";
                else
                    mode = "day";
                await webViewControl.InvokeScriptAsync("ChangeReadMode", new string[] { mode });
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }

        public async Task InsertHtml(string text)
        {
            try
            {
                await webViewControl.InvokeScriptAsync("InsertIntoTinymce", new string[] { text });
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }

        private async Task ChangeTinymceToolBarWidth(string size)
        {
            try
            {
                await webViewControl.InvokeScriptAsync("ChangeTinymceToolBarWidth", new string[] { size });
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }

        public async Task InsertCloze(int count)
        {
            try
            {
                await webViewControl.InvokeScriptAsync("InsertCloze", new string[] { count.ToString() });
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }

        public async Task FocusOn(string name)
        {
            try
            {
                webViewControl.Focus(FocusState.Programmatic);
                await webViewControl.InvokeScriptAsync("FocusOn", new string[] { name });
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }

        public async Task HideEditor()
        {
            try
            {
                await webViewControl.InvokeScriptAsync("HideEditor", null);
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }

        public async Task ShowEditor()
        {
            try
            {
                await webViewControl.InvokeScriptAsync("ShowEditor", null);
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }

        private async Task IsTouchInput(bool value)
        {
            try
            {
                string str;
                if (value)
                    str = "true";
                else
                    str = "false";
                await webViewControl.InvokeScriptAsync("IsTouchInput", new string[] { str });
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }
        
        public async Task ForceNotifyContentChanged()
        {
            try
            {
                //If webview is not ready there is no need to check if its content changed
                if (webViewControl == null)
                    return;
                if (!IsWebviewReady)
                    return;

                await webViewControl.InvokeScriptAsync("ForceNotifyContentChanged", null);
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }

        public async Task LoadNewToolBarWidth(string size)
        {
            try
            {
                await webViewControl.InvokeScriptAsync("LoadNewToolBarWidth", new string[] { size });
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }

        public async Task RemoveAllEditor()
        {
            try
            {
                await webViewControl.InvokeScriptAsync("RemoveAllEditor", null);
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }

        /// <summary>
        /// Updated (13_08_2016): this function is no longer needed on new Windows Phone 10 Vers
        /// But we still keeps it here if it appears again in future updates
        /// This function should only be used to deal with touch input only.
        /// Tinymce and enter key of touch keyboard on win 10 mobile does not work well with each other
        /// so we have to handle it differently
        /// </summary>
        private bool isEnterTimeOut = true;        
        private async Task InsertLineBreak()
        {
            try
            {
                if (isEnterTimeOut)
                {
                    isEnterTimeOut = false;
                    await webViewControl.Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                    {
                        var task = webViewControl.InvokeScriptAsync("InsertLineBreak", null);
                    });
                    await Task.Delay(300);
                    isEnterTimeOut = true;
                }
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }

        public async void ToggleReadMode()
        {
            isNightMode = !isNightMode;
            if (IsWebviewReady)
                await ChangeReadMode();
        }

        private void FieldContextMenuPaste(object sender, RoutedEventArgs e)
        {
            EditableFieldPasteEvent?.Invoke();
        }

        public void ReloadWebView()
        {
            ClearWebViewControl();
            AddWebViewControlIntoGrid();
            NavigateWebviewToLocalPage();
        }

        public void ShowForeColorPickerFlyout(FrameworkElement target, Windows.UI.Xaml.Controls.Primitives.FlyoutPlacementMode placement)
        {
            if (foreColorPicker == null)
            {
                foreColorPicker = new ColorPicker();
                foreColorPicker.ColorChoose += OnForeColorChoose;
            }
            foreColorPicker.ShowFlyout(target, placement);
        }

        private async void OnForeColorChoose(Windows.UI.Xaml.Media.Brush brush)
        {
            foreColorPicker.HideFlyout();
            string hex = UIHelper.GetHexColor(brush);
            await ChangeForeColor(hex);
        }

        public void ShowBackColorPickerFlyout(FrameworkElement target, Windows.UI.Xaml.Controls.Primitives.FlyoutPlacementMode placement)
        {
            if (backColorPicker == null)
            {
                backColorPicker = new ColorPicker();
                backColorPicker.ColorChoose += OnBackColorChoose;
            }
            backColorPicker.ShowFlyout(target, placement);
        }

        private async void OnBackColorChoose(Windows.UI.Xaml.Media.Brush brush)
        {
            backColorPicker.HideFlyout();
            string hex = UIHelper.GetHexColor(brush);
            await ChangeBackColor(hex);
        }

        private async Task ChangeForeColor(string color)
        {
            try
            {
                await webViewControl.InvokeScriptAsync("ChangeForeColor", new string[] { color });
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }

        private async Task ChangeBackColor(string color)
        {
            try
            {
                await webViewControl.InvokeScriptAsync("ChangeBackColor", new string[] { color });
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }
    }
}
