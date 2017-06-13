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
using AnkiU.UIUtilities;
using AnkiU.Models;
using Windows.UI.Text;
using AnkiU.Anki;
using System.Threading.Tasks;
using AnkiU.ViewModels;
using AnkiRuntimeComponent;
using Windows.UI.Popups;
using AnkiU.Interfaces;
using Windows.UI.Core;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage;
using Windows.Data.Json;

namespace AnkiU.Views
{
    public sealed partial class NoteFieldView : UserControl
    {
        public readonly string HTML_PATH;

        private bool isDuplicatePopUpShown = false;

        public NoteFieldsViewModel fieldsViewModel;

        private Note currentNote;
        public Note CurrentNote
        {
            get { return currentNote; }
        }

        private string deckMediaFolderName;
        public string DeckMediaFolderName
        {
            get { return deckMediaFolderName; }
            set
            {
                deckMediaFolderName = value;
                if(htmlEditor.IsWebviewReady)
                {
                    Task task = ChangeDeckMediaFolder(deckMediaFolderName);
                }
            }
        }          

        public event ClickEventHandler WebviewButtonClickEvent;
        public event EditableFieldRoutedEventHandler NoteFieldPasteEvent;
        public event NoticeRoutedHandler InitCompleted;

        private MenuFlyout menuFlyout;        
        private HtmlEditor htmlEditor;
        public HtmlEditor HtmlEditor { get { return htmlEditor; } }

        private CoreDispatcher dispatcher;

        public NoteFieldView()
        {
            this.InitializeComponent();
            dispatcher = CoreWindow.GetForCurrentThread().Dispatcher;

            if (UIHelper.IsHasPhysicalKeyboard())
                HTML_PATH = "/html/fieldeditor.html";
            else
                HTML_PATH = "/html/fieldeditortouch.html";

            string windowSize = WindowSizeStates.CurrentState.Name;
            menuFlyout = Resources["FieldContextMenu"] as MenuFlyout;

            htmlEditor = new HtmlEditor(webViewGrid, contextMenuPlace, 
                                        menuFlyout, windowSize, HTML_PATH, CoreWindow.GetForCurrentThread().Dispatcher);
            
            htmlEditor.WebviewButtonClickEvent += HtmlEditorWebviewButtonClickEventHandler;
            htmlEditor.EditableFieldPasteEvent += NoteFieldPasteEventHandler;
            htmlEditor.FieldReadyToPopulateEvent += PopulateNoteField;
            htmlEditor.EditableFieldTextChangedEvent += NoteFieldTextChangedEventHandler;
            htmlEditor.FieldPopulateFinishEvent += HtmlEditorFieldPopulateFinishHandler;                 
        }

        public async Task SetCurrentNoteAsync(Note note)
        {
            currentNote = note;
            if (htmlEditor.IsWebviewReady)
            {
                if (!htmlEditor.IsEditableFieldPopulate)
                {
                    await PopulateNoteField();
                }
                else
                {
                    await htmlEditor.ChangeAllEditableFieldContent(currentNote.Fields);
                    await RemoveDuplicatPopupIfNeededAsync(0);
                }
            }
        }

        private async Task PopulateNoteField()
        {
            //Make sure to change base reference before populating field
            //or else webView can't load media
            await ChangeDeckMediaFolder(deckMediaFolderName);

            var noteFields = new List<NoteField>();
            List<string> fields = new List<string>();
            await InitNoteFieldAndFieldString(noteFields, fields);
            await htmlEditor.PopulateAllEditableField(fields);
            fieldsViewModel = new NoteFieldsViewModel(noteFields);
        }

        private async Task InitNoteFieldAndFieldString(List<NoteField> noteFields, List<string> fields)
        {
            try
            {
                foreach (var f in currentNote.Model["flds"].GetArray())
                {
                    string name = f.GetObject().GetNamedString("name");
                    int ord = (int)JsonHelper.GetNameNumber(f.GetObject(),"ord");
                    string content = AddDivWrapIfNeeded(currentNote.GetItem(name));

                    fields.Add(name);
                    fields.Add(content);

                    noteFields.Add(new NoteField(currentNote.Id, name, ord, null));
                }
            }
            catch
            {
                await UIHelper.ShowMessageDialog("This note or its note type is corrupted.", "Failed!");
            }
        }

        /// <summary>
        /// This function is used to make sure our content met TinyMce requirements
        /// </summary>
        /// <param name="content"></param>
        private string AddDivWrapIfNeeded(string content)
        {
            if (content.StartsWith("<div>"))
                return content;

            return "<div>" + content + "</div>";
        }

        private Task HtmlEditorFieldPopulateFinishHandler()
        {
            return Task.Run(() =>
            {                                
                InitCompleted?.Invoke();
            });
        }

        private void UserControlLoadedHandler(object sender, RoutedEventArgs e)
        {
            htmlEditor.NavigateWebviewToLocalPage();
        }

        private void HtmlEditorWebviewButtonClickEventHandler(object sender)
        {
            WebviewButtonClickEvent?.Invoke(sender);
        }

        private void NoteFieldPasteEventHandler()
        {
            NoteFieldPasteEvent?.Invoke();
        }

        private void NoteFieldTextChangedEventHandler(string fieldName, string html)
        {
            //TinyMce encode space as &nbsp; so we need to replace it           
            var text = html.Replace("&nbsp;", " ");
            currentNote.SetItem(fieldName, text);
            var task = WarnIfFirstFieldDuplicateAsync(fieldName);
        }

        private async Task WarnIfFirstFieldDuplicateAsync(string fieldName)
        {
            if (fieldName != fieldsViewModel.Fields[0].Name)
                return;

            var firstFieldValid = currentNote.DupeOrEmpty();
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                if (firstFieldValid == Note.FirstField.Duplicate)
                {
                    if (isDuplicatePopUpShown)
                        return;                        

                    await ShowPopup(fieldName);
                    isDuplicatePopUpShown = true;
                }
                else if (isDuplicatePopUpShown)
                {
                    await RemovePopup();
                    isDuplicatePopUpShown = false;
                }
            });            
        }

        public async Task AddNewField(string name, Note newNote)
        {
            int count = fieldsViewModel.Fields.Count;
            fieldsViewModel.Fields.Add(new NoteField(currentNote.Id, name, count, null));            
            await AddField(name);


            newNote.Tags = currentNote.Tags;
            for(int i = 0; i < count - 1; i++)            
                newNote.SetField(i, currentNote.Fields[i]);

            currentNote = newNote;
        }

        public async Task DeleteField(string name, int order, Note newNote)
        {
            await RemoveDuplicatPopupIfNeededAsync(order);

            int count = fieldsViewModel.Fields.Count;
            fieldsViewModel.Fields.RemoveAt(order); 

            await RemoveField(name);

            newNote.Tags = currentNote.Tags;
            for (int i = 0, j = 0; i < count; i++)
            {
                if (i != order)
                {
                    newNote.SetField(j, currentNote.Fields[i]);
                    fieldsViewModel.Fields[j].Order = j;
                    j++;
                }
            }

            currentNote = newNote;

            await ReOpenDuplicatePopupIfNeededAsync(order);
        }

        public async Task RenameField(string name, int order, Note newNote)
        {
            await RemoveDuplicatPopupIfNeededAsync(order);

            await RenameField(fieldsViewModel.Fields[order].Name, name);
            fieldsViewModel.Fields[order].Name = name;
            newNote.Tags = currentNote.Tags;
            for (int i = 0; i < fieldsViewModel.Fields.Count; i++)
                newNote.SetField(i, currentNote.Fields[i]);

            currentNote = newNote;

            await ReOpenDuplicatePopupIfNeededAsync(order);
        }

        public async Task RemoveDuplicatPopupIfNeededAsync(params int[] orders)
        {
            if (orders.Contains(0))
            {
                if (isDuplicatePopUpShown)
                {
                    await RemovePopup();
                    isDuplicatePopUpShown = false;
                }
            }
        }

        private async Task ReOpenDuplicatePopupIfNeededAsync(params int[] orders)
        {
            if (orders.Contains(0))
            {
                await WarnIfFirstFieldDuplicateAsync(fieldsViewModel.Fields[0].Name);
            }
        }

        public async Task MoveField(int oldOrder, int newOrder, Note newNote)
        {            
            await RemoveDuplicatPopupIfNeededAsync(oldOrder, newOrder);

            await MoveField(fieldsViewModel.Fields[oldOrder].Name, newOrder);

            var temp = fieldsViewModel.Fields[oldOrder];
            fieldsViewModel.Fields.RemoveAt(oldOrder);
            fieldsViewModel.Fields.Insert(newOrder, temp);
            for(int i = 0; i < fieldsViewModel.Fields.Count; i++)
                fieldsViewModel.Fields[i].Order = i;            

            newNote.Tags = currentNote.Tags;
            List<string> fields = new List<string>(currentNote.Fields);
            var fieldMove = fields[oldOrder];
            fields.RemoveAt(oldOrder);
            fields.Insert(newOrder, fieldMove);
            newNote.Fields = fields.ToArray();
            
            currentNote = newNote;

            await ReOpenDuplicatePopupIfNeededAsync(oldOrder, newOrder);
        }

        private async Task AddField(string name)
        {
            try
            {
                await htmlEditor.WebViewControl.InvokeScriptAsync("AddField", new string[] { name });
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }

        private async Task RemoveField(string name)
        {
            try
            {
                await htmlEditor.WebViewControl.InvokeScriptAsync("RemoveField", new string[] { name });
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }

        private async Task RenameField(string oldName, string newName)
        {
            try
            {
                await htmlEditor.WebViewControl.InvokeScriptAsync("RenameField", new string[] { oldName, newName });
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }

        private async Task MoveField(string name, int newOder)
        {
            try
            {
                await htmlEditor.WebViewControl.InvokeScriptAsync("MoveField", new string[] { name, newOder.ToString() });
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }

        private async Task ShowPopup(string name)
        {
            try
            {
                await htmlEditor.WebViewControl.InvokeScriptAsync("ShowPopup", new string[] { name });
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }

        private async Task RemovePopup()
        {
            try
            {
                await htmlEditor.WebViewControl.InvokeScriptAsync("RemovePopup", null);
            }
            catch (Exception ex)
            {
                UIHelper.ThrowJavascriptError(ex.HResult);
            }
        }

        public async Task ChangeDeckMediaFolder(string path)
        {
            await htmlEditor.ChangeMediaFolder(path);
        }        

        private void AdaptiveTriggerCurrentStateChanged(object sender, VisualStateChangedEventArgs e)
        {
            //WANRING: This will cause memory leak
            //await htmlEditor.LoadNewToolBarWidth(WindowSizeStates.CurrentState.Name);
        }

        private void FieldContextMenuPaste(object sender, RoutedEventArgs e)
        {
            NoteFieldPasteEvent?.Invoke();
        }
    }   
}
