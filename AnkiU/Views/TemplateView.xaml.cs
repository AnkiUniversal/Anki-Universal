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
using AnkiU.Anki;
using AnkiU.AnkiCore.Templates;
using AnkiU.Interfaces;
using AnkiU.UIUtilities;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace AnkiU.Views
{
    public sealed partial class TemplateView : UserControl, IZoom
    {
        private const int CODE_DIALOG_WMARGIN = 100;
        private const int CODE_DIALOG_HMARGIN = 200;
        private const string FRONT = "FRONTSIDE";
        private const string BACK = "BACKSIDE";
        private readonly string HTML_PATH;

        private JsonObject cardTemplate;
        public JsonObject CardTemplate
        {
            get { return cardTemplate; }
            set
            {
                cardTemplate = value;
                if (htmlEditor.IsWebviewReady)
                {
                    if (!htmlEditor.IsEditableFieldPopulate)
                    {
                        Task task = PopulateTemplateField();
                        task.Wait();
                    }
                    else
                    {
                        Task task = ChangeTemplateAsync();
                    }

                }
            }
        }

        private async Task ChangeTemplateAsync()
        {
            await htmlEditor.ChangeAllEditableFieldContent(
                                        new string[] { cardTemplate.GetNamedString("qfmt"),
                                           cardTemplate.GetNamedString("afmt")
                                        });
            await htmlEditor.FocusOn(FRONT);
        }

        private string css;
        public string Css
        {
            get { return css; }
            set
            {
                css = value;
                if (htmlEditor.IsWebviewReady)
                {
                    Task task = ChangeTemplateStyle();
                }
            }
        }

        public event ClickEventHandler WebviewButtonClickEvent;
        public event EditableFieldRoutedEventHandler TemplatePasteEvent;
        public event NoticeRoutedHandler InitCompleted;

        private MenuFlyout menuFlyout;
        private HtmlEditor htmlEditor;
        public HtmlEditor HtmlEditor { get { return htmlEditor; } }

        public double ZoomLevel { get; set; }

        public bool IsSave { get { return false; } }

        public TemplateView()
        {
            this.InitializeComponent();
            ZoomLevel = 1;

            if (UIHelper.IsHasPhysicalKeyboard())
                HTML_PATH = "/html/templateeditor.html";
            else
                HTML_PATH = "/html/templateeditortouch.html";

            string windowSize = WindowSizeStates.CurrentState.Name;
            menuFlyout = Resources["TemplateContextMenu"] as MenuFlyout;

            htmlEditor = new HtmlEditor(webViewGrid, contextMenuPlace, menuFlyout, 
                                       windowSize, HTML_PATH, CoreWindow.GetForCurrentThread().Dispatcher);

            htmlEditor.WebviewButtonClickEvent += HtmlEditorWebviewButtonClickEventHandler;
            htmlEditor.EditableFieldPasteEvent += FieldPasteEventHandler;
            htmlEditor.FieldReadyToPopulateEvent += PopulateTemplateField;
            htmlEditor.EditableFieldTextChangedEvent += EditableFieldTextChangedEventHandler;
            htmlEditor.FieldPopulateFinishEvent += HtmlEditorFieldPopulateFinishEventHandler;
        }

        private void EditableFieldTextChangedEventHandler(string fieldName, string html)
        {
            if (fieldName == FRONT)
                cardTemplate["qfmt"] = JsonValue.CreateStringValue(html);
            else
                cardTemplate["afmt"] = JsonValue.CreateStringValue(html);
        }

        private async Task PopulateTemplateField()
        {            
            await ChangeCodeDialogWidthHeight(webViewGrid.ActualWidth - CODE_DIALOG_WMARGIN, 
                                              webViewGrid.ActualHeight - CODE_DIALOG_HMARGIN);

            if (css != null)
                await ChangeTemplateStyle();

            List<string> fields = new List<string>();
            fields.Add(FRONT);
            string format = cardTemplate.GetNamedString("qfmt");
            fields.Add(format);
            fields.Add(BACK);
            format = cardTemplate.GetNamedString("afmt");
            fields.Add(format);
            await htmlEditor.PopulateAllEditableField(fields);
        }

        private async Task HtmlEditorFieldPopulateFinishEventHandler()
        {
            await InsertAfterField(FRONT, "<br> <br> <hr>");
            InitCompleted?.Invoke();
        }

        private void UserControlLoadedHandler(object sender, RoutedEventArgs e)
        {
            htmlEditor.NavigateWebviewToLocalPage();
        }

        private void HtmlEditorWebviewButtonClickEventHandler(object sender)
        {
            WebviewButtonClickEvent?.Invoke(sender);
        }

        private void FieldPasteEventHandler()
        {
            TemplatePasteEvent?.Invoke();
        }

        public async Task ChangeDeckMediaFolder(string path)
        {
            await htmlEditor.ChangeMediaFolder(path);
        }

        private async void AdaptiveTriggerCurrentStateChanged(object sender, VisualStateChangedEventArgs e)
        {
            //WANRING: This will cause memory leak if there are too many editor
            await ChangeCodeDialogWidthHeight(webViewGrid.ActualWidth - CODE_DIALOG_WMARGIN,
                                  webViewGrid.ActualHeight - CODE_DIALOG_HMARGIN);
            await htmlEditor.LoadNewToolBarWidth(WindowSizeStates.CurrentState.Name);
        }

        private void ContextMenuPaste(object sender, RoutedEventArgs e)
        {
            TemplatePasteEvent?.Invoke();
        }

        public async Task ChangeTemplateStyle()
        {
            try
            {
                await htmlEditor.WebViewControl.InvokeScriptAsync("ChangeTemplateStyle", new string[] { Css });
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }

        public async Task InsertAfterField(string name, string html)
        {
            try
            {
                await htmlEditor.WebViewControl.InvokeScriptAsync("InsertAfterField", new string[] { name, html });
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }

        public async Task ChangeCodeDialogWidthHeight(double width, double height)
        {
            try
            {
                await htmlEditor.WebViewControl.InvokeScriptAsync("ChangeCodeDialogWidthHeight", new string[] { width.ToString(), height.ToString() });
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }

        public async Task ChangeZoomLevel(double value)
        {
            try
            {
                if (UIHelper.IsHasPhysicalKeyboard())
                    await htmlEditor.WebViewControl.InvokeScriptAsync("ChangeEditableZoom", new string[] { value.ToString() });
                else
                {
                    var maxHeight = GetDefaultEditableAreaMaxHeight();
                    var newMaxHeigh = maxHeight / (value + 0.1);
                    await htmlEditor.WebViewControl.InvokeScriptAsync("ChangeEditableZoom", new string[] { value.ToString(), newMaxHeigh.ToString() });
                }
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }

        public async Task InsertIntoAllFields(string html)
        {
            try
            {                
                await htmlEditor.WebViewControl.InvokeScriptAsync("InsertIntoAllFields", new string[] { html });
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }

        private double GetDefaultEditableAreaMaxHeight()
        {
            //These are hardcoded in css style of templateeditortouch.cs
            var windowHeight = CoreWindow.GetForCurrentThread().Bounds.Height;
            if (windowHeight < 300)
                return 50;

            if (windowHeight < 500)
                return 100;

            if (windowHeight < 700)
                return 200;

            if (windowHeight < 1100)
                return 400;

            return 500;
        }
    }
}
