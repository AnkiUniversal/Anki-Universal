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

using AnkiU.Anki;
using AnkiU.AnkiCore;
using AnkiU.Interfaces;
using AnkiU.Models;
using AnkiU.UIUtilities;
using AnkiU.UserControls;
using AnkiU.ViewModels;
using AnkiU.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel.DataTransfer;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;
using AnkiTemplate = AnkiU.AnkiCore.Templates.Template;

namespace AnkiU.Pages
{
    public class NoteEditorPageParameter
    {
        public MainPage Mainpage { get; set; }
        public Note CurrentNote { get; set; }
    }

    public sealed partial class NoteEditor : Page, INightReadMode
    {
        private const int MAX_UNDO = 10;

        private HelpPopup helpPopup;

        private MainPage mainPage;
        private Collection collection;
        private long currentDeckId;
        private StorageFolder deckIdFolder;
        private StorageFolder tempFolder;

        private AnkiModelInfomartionViewModel modelInformationViewModel;
        private TagInformationViewModel tagsViewModel;
        private NotesFirstFieldViewModel firstFieldsViewModel = new NotesFirstFieldViewModel(MAX_UNDO);

        private NameEnterFlyout renameNoteTypeFlyout = null;
        private NameEnterFlyout addFieldFlyout = null;
        private FieldListView fieldListView = null;

        private NameEnterFlyout renameFieldFlyout = null;
        private NoteField fieldToRename = null;

        private IntNumberEnterFlyout repositionFieldFlyout = null;
        private int oldFieldOrder;

        private AsyncTaskRoutedHandler fieldButtonClickFunction;

        private Note currentNote;        
        private string deckName;

        private List<string> newFileAdded = new List<string>();

        private bool suppressModelComboboxSelectionChangeEvent = false;
        private bool isFromUndo = false;
        private bool isNewNoteMode;
        private bool isGoToModelPage = false;
        private bool isNightMode = false;

        public event NoticeRoutedHandler AddNewNoteEvent;

        public NoteEditor()
        {
            this.InitializeComponent();         
        }

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            var parameter = e.Parameter as NoteEditorPageParameter;
            if (parameter == null)
                throw new Exception("Wrong input parameter!");            

            mainPage = parameter.Mainpage;
            ShowProgessRing();

            collection = mainPage.Collection;
            collection.Database.SaveTransactionPoint();
            currentDeckId = collection.Deck.Selected();
            deckName = "Deck: " + collection.Deck.GetDeckName(currentDeckId);
            await SetpUpFolders();

            EnterTutorialModeIfNeeded();
            mainPage.EnableChangingReadMode(this, noteFieldView.HtmlEditor);
            ChangeBackgroundColor();
            mainPage.SaveButton.Visibility = Visibility.Visible;            

            SetupCurrentNote(parameter);
            await SetupNoteFieldViewAsync();
            SetupTagsView();

            CoreWindow.GetForCurrentThread().KeyDown += NoteEditorKeyUp;
        }

        private bool isProcessedKeyPressEvent = false;
        private async void NoteEditorKeyUp(CoreWindow sender, KeyEventArgs args)
        {
            if (isProcessedKeyPressEvent)
                return;

            await mainPage.CurrentDispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {                
                var control = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control);
                if(control.HasFlag(CoreVirtualKeyStates.Down) && args.VirtualKey == VirtualKey.S)
                {
                    isProcessedKeyPressEvent = true;
                    await SaveNote();
                    await Task.Delay(500);
                    isProcessedKeyPressEvent = false;
                }
            });
        }

        private void ShowProgessRing()
        {
            mainPage.IsCanNavigateBack = false;
            progessRing.Visibility = Visibility.Visible;
            progessRing.IsActive = true;
        }

        private void SetupCurrentNote(NoteEditorPageParameter parameter)
        {
            if (parameter.CurrentNote == null)
            {
                SetupNewNoteView();
                SetupDeckModel();
                currentNote = collection.NewNote();
                SetupAnkiModelView();
                isNewNoteMode = true;
            }
            else
            {
                SetupEditNoteView();
                
                currentNote = parameter.CurrentNote;
                isNewNoteMode = false;
            }
        }

        private void SetupNewNoteView()
        {
            FindName("noteTypeGrid");
            FindName("undoFlyoutContentPresenter");

            noteTypeGrid.Visibility = Visibility.Visible;
            mainPage.UndoButton.Visibility = Visibility.Visible;
            mainPage.UndoButton.Click += UndoButtonClickHandler;            
            mainPage.SaveButton.Click += SaveNewNoteButtonClick;
            mainPage.UndoButton.IsEnabled = false;            
            undoFirstFieldView.DataContext = firstFieldsViewModel.FirstFields;
        }

        private void SetupEditNoteView()
        {
            mainPage.UndoButton.Visibility = Visibility.Collapsed;
            mainPage.SaveButton.Click += SaveEditNoteButtonClickHandler;
        }

        private void UndoButtonClickHandler(object sender, RoutedEventArgs e)
        {
            undoFlyout.ShowAt(sender as AppBarButton);
        }

        private void SetupAnkiModelView()
        {
            modelInformationViewModel = new AnkiModelInfomartionViewModel(collection.Models.All());
            modelInformationView.DataContext = modelInformationViewModel.Models;
            modelInformationView.ChangeSelectedItem(currentNote.ModelId);
            modelInformationView.ComboBoxSelectionChangedEvent += ModelComboBoxSelectionChangedEventHandler;
            //Different with python and java ver, we do not allow user to change deck model
            modelInformationView.DisableModelSelection();
        }
        private async Task SetupNoteFieldViewAsync()
        {            
            await noteFieldView.SetCurrentNoteAsync(currentNote);
            noteFieldView.DeckMediaFolderName = "/" + collection.Media.MediaFolder.Name + "/" + currentDeckId + "/";
            noteFieldView.WebviewButtonClickEvent += NoteFieldViewWebviewButtonClickEventHandler;
            noteFieldView.NoteFieldPasteEvent += NoteFieldPasteEventHandler;
            noteFieldView.InitCompleted += NoteFieldViewInitCompleted;
        }

        private void SetupTagsView()
        {
            tagsViewModel = new TagInformationViewModel(collection, currentNote);
            tagsView.ViewModel = tagsViewModel;
        }

        private async Task SetpUpFolders()
        {
            deckIdFolder = await collection.Media.MediaFolder.TryGetItemAsync(currentDeckId.ToString()) as StorageFolder;
            if (deckIdFolder == null)
                deckIdFolder = await collection.Media.MediaFolder.CreateFolderAsync(currentDeckId.ToString());            
        }

        private async Task<StorageFolder> GetTempFolder()
        {
            if (tempFolder == null)
            {
                tempFolder = await Storage.AppLocalFolder.TryGetItemAsync("tempNoteEditor") as StorageFolder;
                if (tempFolder != null)
                    await tempFolder.DeleteAsync();
                tempFolder = await Storage.AppLocalFolder.CreateFolderAsync("tempNoteEditor");
            }
            return tempFolder;
        }

        private void SetupDeckModel(long? id = null)
        {
            JsonObject model;
            long modelID;
            if (id == null)
            {
                model = collection.Models.GetCurrent();
                modelID = (long)model.GetNamedNumber("id");
            }
            else
            {
                modelID = (long)id;
                model = collection.Models.Get(modelID);
            }

            collection.Models.SetCurrent(modelID);
            collection.Deck.Current()["mid"] = model.GetNamedValue("id");
            model["did"] = JsonValue.CreateNumberValue(currentDeckId);
        }
        
        protected async override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            if (!noteFieldView.HtmlEditor.IsContentCheckOnce)
            {
                e.Cancel = true;
                await noteFieldView.HtmlEditor.ForceNotifyContentChanged();                
                ContinueNavigating();
                return;
            }

            if (noteFieldView.HtmlEditor.IsModified)
            {
                e.Cancel = true;
                bool isContinue = false;
                if (noteFieldView.HtmlEditor.IsModified)
                    isContinue = await UIHelper.AskUserConfirmation(UIConst.WARN_NOTSAVE);
                if (isContinue)
                {
                    noteFieldView.HtmlEditor.IsModified = false;
                    ContinueNavigating();
                    return;
                }
            }
            else
            {
                HideAllButtonOfThisPage();
                UnHookAllEvents();
            }

            base.OnNavigatingFrom(e);
        }

        protected async override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);            
            collection.SaveAndCommitAsync();
            noteFieldView.HtmlEditor.ClearWebViewControl();
            await TryDeleteTempFolder();                     
        }

        private async Task TryDeleteTempFolder()
        {
            if (tempFolder == null)
                return;

            try
            {
                await tempFolder.DeleteAsync();
            }
            catch
            {//Something prevent us to delete temp folder
             //Just return for now
            }
        }

        private void HideAllButtonOfThisPage()
        {
            mainPage.DisableChangingReadMode();
            mainPage.UndoButton.IsEnabled = true;
            mainPage.UndoButton.Visibility = Visibility.Collapsed;            
            mainPage.SaveButton.Visibility = Visibility.Collapsed;
        }
        private void UnHookAllEvents()
        {
            CoreWindow.GetForCurrentThread().KeyDown -= NoteEditorKeyUp;

            mainPage.UndoButton.Click -= UndoButtonClickHandler;
            mainPage.SaveButton.Click -= SaveEditNoteButtonClickHandler;
            mainPage.SaveButton.Click -= SaveNewNoteButtonClick;
        }

        private async void SaveEditNoteButtonClickHandler(object sender, RoutedEventArgs e)
        {
            var isValid = await CheckNoteValidity();
            if (!isValid)
                return;

            currentNote.SaveChangesToDatabase();
            noteFieldView.HtmlEditor.IsModified = false;
            noteFieldView.HtmlEditor.IsContentCheckOnce = true;

            if (!isFromUndo)
            {
                if (Frame.CanGoBack)
                    Frame.GoBack();
            }
            else
            {
                SwitchToNewNoteView();
                await CacheAndMoveToNextNote();
                isFromUndo = false;

                await noteFieldView.HtmlEditor.FocusOn(noteFieldView.fieldsViewModel.Fields[0].Name);
            }
        }

        private async void SaveNewNoteButtonClick(object sender, RoutedEventArgs e)
        {
            var isValid = await CheckNoteValidity();
            if (!isValid)
                return;

            var numberOfGenCards = collection.AddNote(currentNote);
            if (numberOfGenCards == 0)
            {
                await UIHelper.ShowMessageDialog(UIConst.WARN_NOTEMPLATE_MATCH, "Invalid note!");
                return;
            }

            ShowNumberOfCardsAdded(numberOfGenCards);
            noteFieldView.HtmlEditor.IsModified = false;
            noteFieldView.HtmlEditor.IsContentCheckOnce = false;
            await CacheAndMoveToNextNote();            

            AddNewNoteEvent?.Invoke();

            await noteFieldView.HtmlEditor.FocusOn(noteFieldView.fieldsViewModel.Fields[0].Name);
        }

        private void ShowNumberOfCardsAdded(int numberOfGenCards)
        {
            string text = numberOfGenCards + " card(s) added";
            numberOfCardsAddedPopup.ShowAsync(mainPage.CurrentDispatcher, text, 1000);
        }

        private async Task<bool> CheckNoteValidity()
        {
            var firstField = currentNote.DupeOrEmpty();
            if (firstField == Note.FirstField.Empty)
            {
                await UIHelper.ShowMessageDialog("First field cannot be empty.", "Invalid note!");
                return false;
            }
            else if (firstField == Note.FirstField.Duplicate)
            {
                await AskUserIfNeedEditExistingNote();
                return false;
            }

            return true;
        }

        private async Task AskUserIfNeedEditExistingNote()
        {
            bool isEdit = await UIHelper.AskUserConfirmation(UIConst.WARN_NOTE_EXIST);
            if (isEdit)            
                await EditExistingNote();            
        }

        private async Task EditExistingNote()
        {
            var editNote = collection.GetNote(currentNote.DupeNoteId);
            RemoveNoteFromUndoQueueIfHas(editNote);                    
            currentNote = editNote;
            await noteFieldView.SetCurrentNoteAsync(editNote);
            tagsViewModel.CurrentNote = currentNote;
            tagsViewModel.UpdateNoteTagsFromNote();            

            if (isNewNoteMode)
            {
                SwitchToEditView();
                isFromUndo = true;
            }
        }

        private void RemoveNoteFromUndoQueueIfHas(Note editNote)
        {
            var noteField = firstFieldsViewModel.GetNoteField(editNote.Id);
            if (noteField != null)
            {
                firstFieldsViewModel.RemoveFirstFieldFromList(noteField);
                DisableUndoButtonIfNoUndoLeft();
            }
        }

        private async Task CacheAndMoveToNextNote()
        {     
            firstFieldsViewModel.AddFirstFieldToList(currentNote);
            mainPage.UndoButton.IsEnabled = true;
            await UpdateCurrentNote();
            mainPage.SaveAndStartNewDatabaseSessionAsync();
        }

        private async Task UpdateCurrentNote()
        {
            currentNote = collection.NewNote();
            await noteFieldView.SetCurrentNoteAsync(currentNote);
            tagsViewModel.CurrentNote = currentNote;
            tagsViewModel.CloneUsedTagsToNewNote();
        }

        private async void NoteFieldViewWebviewButtonClickEventHandler(object sender)
        {
            await mainPage.CurrentDispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                try
                {
                    string name = sender as string;
                    if (name == null)
                        return;

                    switch (name)
                    {
                        case ("media"):
                            await AddMedia();
                            break;
                        case ("link"):
                            await AddLink();
                            break;
                        case ("microphone"):
                            await AudioRecorder();
                            break;
                        case ("cloze"):
                            await AddCloze();
                            break;
                        case ("save"):                            
                            await SaveNote();
                            break;
                        default:
                            break;
                    }
                }
                catch(Exception ex)
                {
                    // This code should never be reached
                    await UIHelper.ShowMessageDialog("Note Editor: " + ex.Message);
                }
            });
        }

        private async Task SaveNote()
        {
            mainPage.SaveButtonClickAnimateAsync();

            await noteFieldView.HtmlEditor.ForceNotifyContentChanged();

            //This delay is not important.
            //This is just to make sure that all async checking event can end properly before continuing
            await Task.Delay(100);

            if (isNewNoteMode && !isFromUndo)
                SaveNewNoteButtonClick(null, null);
            else
                SaveEditNoteButtonClickHandler(null, null);
        }

        private async Task AddMedia()
        {
            var file = await UIHelper.OpenFilePicker("AddMediaToken", Media.ALLOWED_EXTENSION);
            if (file == null)
                return;

            await TryAddMedia(file);
        }

        private static bool IsMediaFileType(StorageFile file)
        {
            return Media.ALLOWED_EXTENSION.Contains(file.FileType.ToLower());
        }
        private async Task TryAddMedia(StorageFile file)
        {
            var fileName = await TryAddNewFileIntoDeckIdMediaFolder(file);
            if (fileName != null)
                await InsertHtmlTagIntoField(file, fileName);
        }

        private async Task<string> TryAddNewFileIntoDeckIdMediaFolder(StorageFile file)
        {
            if (!IsMediaFileType(file))
            {
                await UIHelper.ShowMessageDialog("Unsupported file format!", "Error!");
                return null;
            }

            bool isCancel = false;
            bool isReuse = false;
            bool isReplace = false;
            string legalName = collection.Media.StripIllegal(file.Name);
            var existItem = await deckIdFolder.TryGetItemAsync(legalName) as StorageFile;
            if (existItem != null)
            {
                FourOptionsDialog dialog = InitDuplicateDialog();
                dialog.OpenButtonClicked += async (s, e) => { await Windows.System.Launcher.LaunchFileAsync(existItem); };
                await dialog.ShowAsync();

                isCancel = dialog.IsFourthButtonClick();
                if (isCancel)
                    return null;

                isReplace = dialog.IsFirstButtonClick();
                isReuse = dialog.IsThirdButtonClick();
            }

            if (!isReuse && !isReplace)                            
                legalName = await AddNewFile(file);
            else if(isReplace)
            {
                if (!existItem.IsEqual(file))
                {
                    await file.CopyAndReplaceAsync(existItem);
                    collection.Media.MarkFileAddIntoDatabase(existItem.Name, currentDeckId);
                }
            }

            return legalName;
        }

        private static FourOptionsDialog InitDuplicateDialog()
        {
            FourOptionsDialog dialog = new FourOptionsDialog();
            dialog.Message = "This deck already has a media file with the same name.\n ";
            dialog.Title = "Duplicate File Name";
            dialog.FirstButton.Content = "Replace";
            dialog.SecondButton.Content = "Rename";
            dialog.ThirdButton.Content = "Reuse";            
            return dialog;
        }

        private async Task InsertHtmlTagIntoField(StorageFile file, string fileName)
        {
            string html;
            if (file.ContentType.Contains("image"))
                html = String.Format(Media.IMAGE_HTML, fileName);
            else
                html = String.Format(Media.SOUND_HTML, fileName);

            await noteFieldView.HtmlEditor.InsertHtml(html);
        }

        private async Task<string> AddNewFile(StorageFile file)
        {
            string fileName = await collection.Media.AddFile(file, currentDeckId);
            newFileAdded.Add(fileName);                        

            return fileName;
        }

        private async Task AddLink()
        {
            HyperLinkDialog hfLinkDialog = new HyperLinkDialog();
            hfLinkDialog.HyperlinkCreateEvent += async (html) => { await noteFieldView.HtmlEditor.InsertHtml(html); };
            await hfLinkDialog.ShowAsync();
        }
        
        private async Task AudioRecorder()
        {
            AudioRecorderDialog dialog = new AudioRecorderDialog(await GetTempFolder());
            await dialog.ShowAsync();
            if (dialog.FileRecorded != null)
                await TryAddMedia(dialog.FileRecorded);
        }

        private async Task AddCloze()
        {
            if((ModelType)currentNote.Model.GetNamedNumber("type") == ModelType.STD)
            {
                await UIHelper.ShowMessageDialog(UIConst.WARN_NOTCLOZETYPE);
                return;
            }

            //Make sure to get current content of active editor first
            await noteFieldView.HtmlEditor.ForceNotifyContentChanged();

            var questionFormat = currentNote.Model.GetNamedArray("tmpls").GetObjectAt(0).GetNamedString("qfmt");
            if (!AnkiTemplate.ClozeRegex.IsMatch(questionFormat))
            {
                await UIHelper.ShowMessageDialog(UIConst.WARN_NOCLOZE_FIELD);
                return;
            }
            int highest = 1;
            foreach (var field in currentNote.Fields)
            {
                var matches = AnkiTemplate.ClozeCountRegex.Matches(field);
                foreach (Match m in matches)
                {
                    int value = int.Parse(m.Groups[1].ToString());
                    if (value >= highest)
                        highest = value + 1;
                }
            }
            await noteFieldView.HtmlEditor.InsertCloze(highest);
        }

        private async void NoteFieldViewInitCompleted()
        {
            await mainPage.CurrentDispatcher.RunAsync(CoreDispatcherPriority.Normal, () => 
            {                
                HideProgressRing();
            });
        }

        private void HideProgressRing()
        {
            mainPage.IsCanNavigateBack = true;
            progessRing.IsActive = false;
            progessRing.Visibility = Visibility.Collapsed;
        }

        private async void NoteFieldPasteEventHandler()
        {
            try
            {
                await TryPasteContentFromClipboard();
            }
            catch
            { //Failed to paste should not make the program crashs
            }
        }

        private async Task TryPasteContentFromClipboard()
        {
            var dataPackageView = Clipboard.GetContent();
            if (dataPackageView.Contains(StandardDataFormats.StorageItems))
            {
                await PasteStorageFiles(dataPackageView);
            }
            else if (dataPackageView.Contains(StandardDataFormats.Bitmap))
            {
                await PasteBitmapImage(dataPackageView);
            }
            else if (dataPackageView.Contains(StandardDataFormats.Html))
            {
                await PasteHtmlFormat(dataPackageView);
            }
            else if (dataPackageView.Contains(StandardDataFormats.WebLink))
            {
                await PasteAndAutoLinkUri(dataPackageView);
            }
            else if (dataPackageView.Contains(StandardDataFormats.Text))
            {
                await PastePlainText(dataPackageView);
            }
        }

        private async Task PasteStorageFiles(DataPackageView dataPackageView)
        {
            var pasteContent = await dataPackageView.GetStorageItemsAsync();
            foreach (var content in pasteContent)
            {
                var file = content as StorageFile;
                if (file == null)
                    continue;

                await TryAddMedia(file);
            }
        }        

        private async Task PasteBitmapImage(DataPackageView dataPackageView)
        {
            IRandomAccessStreamReference imageReceived = null;            
            imageReceived = await dataPackageView.GetBitmapAsync();
            if (imageReceived != null)
            {
                StorageFile file;
                using (var imageStream = await imageReceived.OpenReadAsync())
                {
                    BitmapDecoder decoder = await BitmapDecoder.CreateAsync(imageStream);
                    var softwareBitmap = await decoder.GetSoftwareBitmapAsync();

                    file = await CreateBitmapFileWithUniqueName();
                    using (var stream = await file.OpenAsync(FileAccessMode.ReadWrite))
                    {
                        BitmapEncoder encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.JpegEncoderId, stream);
                        encoder.SetSoftwareBitmap(softwareBitmap);
                        encoder.IsThumbnailGenerated = false;
                        await encoder.FlushAsync();
                    }
                }

                await TryAddMedia(file);
            }
        }

        private async Task PasteHtmlFormat(DataPackageView dataPackageView)
        {
            var html = await dataPackageView.GetHtmlFormatAsync();
            await noteFieldView.HtmlEditor.InsertHtml(html);
        }

        private async Task PasteAndAutoLinkUri(DataPackageView dataPackageView)
        {            
            var linkRegex = new Regex(@"(?i)(http.+?(?=http))", RegexOptions.Compiled);

            var uri = (await dataPackageView.GetWebLinkAsync()).AbsoluteUri;
            //No idea why dataPackage can return a duplicate link
            //so use regex to make sure we only take in one
            var match = linkRegex.Match(uri);
            string link;
            if (match.Success)
                link = match.Groups[1].ToString();
            else
                link = uri;

            var html = String.Format(HyperLinkDialog.LINK_HTML, link, link);
            await noteFieldView.HtmlEditor.InsertHtml(html);
        }

        private async Task PastePlainText(DataPackageView dataPackageView)
        {
            var text = await dataPackageView.GetTextAsync();
            await noteFieldView.HtmlEditor.InsertHtml(text);
        }

        private async Task<StorageFile> CreateBitmapFileWithUniqueName()
        {
            StringBuilder fileName = UIHelper.GetDateTimeStringForName();

            StorageFile file;
            while (true)
            {
                fileName.Append(DateTimeOffset.Now.Millisecond);
                string fileNameInDeckIdFolder = fileName + ".bmp";
                file = await deckIdFolder.TryGetItemAsync(fileNameInDeckIdFolder) as StorageFile;
                if (file == null)                                    
                {
                    await GetTempFolder();

                    file = await tempFolder.CreateFileAsync(fileNameInDeckIdFolder, CreationCollisionOption.ReplaceExisting);
                    break;
                }
            }
            return file;
        }

        private async void ModelComboBoxSelectionChangedEventHandler(object sender, SelectionChangedEventArgs e)
        {
            var comboBox = sender as ComboBox;
            var selected = comboBox.SelectedItem as AnkiModelInformation;

            if (suppressModelComboboxSelectionChangeEvent)
            {
                suppressModelComboboxSelectionChangeEvent = false;
                return;
            }

            bool isContinue = await UIHelper.AskUserConfirmation("Changing note type will reset all your inputs. Continue?");
            if (!isContinue)
            {
                ChangeSelectedModel(currentNote.ModelId);
                return;
            }

            SetupDeckModel(selected.Id);
            await UpdateCurrentNote();
            noteFieldView.HtmlEditor.ReloadWebView();
            fieldListView = null;
        }

        private void ChangeSelectedModel(long id)
        {
            suppressModelComboboxSelectionChangeEvent = true;
            modelInformationView.ChangeSelectedItem(id);
        }

        private async void UndoEditButtonClickHandler(object sender, RoutedEventArgs e)
        {
            undoFlyout.Hide();

            if (noteFieldView.HtmlEditor.IsModified)
            {
                bool isContinue = await UIHelper.AskUserConfirmation(UIConst.WARN_NOTSAVE);
                if (!isContinue)
                    return;
            }

            var note = (sender as FrameworkElement).DataContext as NoteField;
            firstFieldsViewModel.RemoveFirstFieldFromList(note);
            DisableUndoButtonIfNoUndoLeft();
            SwitchToEditView();

            var editNote = collection.GetNote(note.Id);
            await ChangeModelIfNeededAndUpdateNoteField(editNote);
            currentNote = editNote;
            tagsViewModel.CurrentNote = currentNote;
            tagsViewModel.UpdateNoteTagsFromNote();

            isFromUndo = true;
        }

        private async Task ChangeModelIfNeededAndUpdateNoteField(Note editNote)
        {
            if (editNote.ModelId != currentNote.ModelId)
            {
                noteFieldView.HtmlEditor.ReloadWebView();
                SetupDeckModel(editNote.ModelId);                
                ChangeSelectedModel(editNote.ModelId);
                await noteFieldView.SetCurrentNoteAsync(editNote);
            }
            else
                await noteFieldView.SetCurrentNoteAsync(editNote);
        }

        private void DisableUndoButtonIfNoUndoLeft()
        {
            if (firstFieldsViewModel.FirstFields.Count == 0)
                mainPage.UndoButton.IsEnabled = false;
        }

        private void SwitchToEditView()
        {            
            noteTypeGrid.Visibility = Visibility.Collapsed;
            mainPage.UndoButton.Visibility = Visibility.Collapsed;

            mainPage.SaveButton.Click -= SaveNewNoteButtonClick;
            mainPage.SaveButton.Click -= SaveEditNoteButtonClickHandler;

            mainPage.SaveButton.Click += SaveEditNoteButtonClickHandler;
        }

        private void SwitchToNewNoteView()
        {
            noteTypeGrid.Visibility = Visibility.Visible;
            mainPage.UndoButton.Visibility = Visibility.Visible;

            mainPage.SaveButton.Click -= SaveEditNoteButtonClickHandler;
            mainPage.SaveButton.Click -= SaveNewNoteButtonClick;

            mainPage.SaveButton.Click += SaveNewNoteButtonClick;            
        }

        private void EditTemplatesMenuClickHandler(object sender, RoutedEventArgs e)
        {
            isGoToModelPage = true;
            Frame.Navigate(typeof(TemplateEditor), mainPage);
        }

        private void ContinueNavigating()
        {
            if(isGoToModelPage)
                Frame.Navigate(typeof(TemplateEditor), mainPage);
            else
                Frame.GoBack();
        }

        public void ToggleReadMode()
        {
            isNightMode = !isNightMode;
            ChangeBackgroundColor();
        }

        private void ChangeBackgroundColor()
        {
            UIHelper.ToggleNightLight(isNightMode, userControl);
            if (helpPopup != null)
                helpPopup.ChangeReadMode(isNightMode);
        }

        private void RenameNoteTypeMenuFlyoutItemClickHandler(object sender, RoutedEventArgs e)
        {
            if(renameNoteTypeFlyout == null)
            {
                renameNoteTypeFlyout = new NameEnterFlyout();
                renameNoteTypeFlyout.OkButtonClickEvent += RenameNoteTypeFlyoutOkButtonClickEventHandler;                
            }
            renameNoteTypeFlyout.Show(editModelButton, modelInformationView.CurrentName());
        }

        private async void RenameNoteTypeFlyoutOkButtonClickEventHandler(object sender, RoutedEventArgs e)
        {
            await mainPage.CurrentDispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                var newName = renameNoteTypeFlyout.NewName;
                newName = newName.Trim();
                var isValid = await UIHelper.CheckValidName(newName, modelInformationViewModel.Models, UIConst.WARN_NOTETYPE_EXIST);
                if (!isValid)
                {
                    renameNoteTypeFlyout.Show(editModelButton);
                    return;
                }

                currentNote.Model["name"] = JsonValue.CreateStringValue(newName);
                modelInformationView.ChangeSelectedItemName(newName);

                collection.Models.Save(currentNote.Model);
                mainPage.SaveAndStartNewDatabaseSessionAsync();
            });
        }

        private async void AddFieldMenuItemClickHandler(object sender, RoutedEventArgs e)
        {
            var isContinue = await MainPage.WarnFullSyncIfNeeded();
            if (!isContinue)
                return;            
            
            if(addFieldFlyout == null)
            {
                addFieldFlyout = new NameEnterFlyout();
                addFieldFlyout.OkButtonClickEvent += AddFieldFlyoutOkButtonClickEventHandler;
            }
            addFieldFlyout.Show(editModelButton, "");            
        }

        private async void AddFieldFlyoutOkButtonClickEventHandler(object sender, RoutedEventArgs e)
        {
            await mainPage.CurrentDispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                string name = addFieldFlyout.NewName;

                var isValid = await UIHelper.CheckValidName(name, noteFieldView.fieldsViewModel.GetExistedFieldsName(), UIConst.WARN_NOTEFIELD_EXIST);

                if (!isValid)
                {
                    addFieldFlyout.Show(editModelButton);
                    return;
                }

                var newField = collection.Models.NewField(name);
                collection.Models.AddField(currentNote.Model, newField);
                currentNote = collection.NewNote();

                await noteFieldView.AddNewField(name, currentNote);
                SavePrefs();
            });
        }

        private async void DeleteFieldMenuClickHandler(object sender, RoutedEventArgs e)
        {
            await ShowWarnThenFieldList(DeleteField);
        }

        private async Task ShowWarnThenFieldList(AsyncTaskRoutedHandler func)
        {
            var isContinue = await MainPage.WarnFullSyncIfNeeded();
            if (!isContinue)
                return;
            InitFieldListViewIfNeeded();
            fieldButtonClickFunction = func;
            fieldListView.Show(editModelButton);
        }

        private void InitFieldListViewIfNeeded()
        {
            if (fieldListView == null)
            {
                fieldListView = new FieldListView();
                fieldListView.SetDataContext(noteFieldView.fieldsViewModel);
                fieldListView.FieldClickEvent += FieldListViewClickEventHandler;
            }
        }

        private async Task DeleteField(object sender)
        {            
            NoteField field = (sender as Button).DataContext as NoteField;
            fieldListView.Hide();

            var noteCount = collection.Models.NoteUseCount(currentNote.Model);
            bool isContinue = await UIHelper.AskUserConfirmation(String.Format(UIConst.WARN_DELETE_FIELD, noteCount));
            if (!isContinue)
                return;

            var model = currentNote.Model;
            var fieldJson = model.GetNamedArray("flds").GetObjectAt((uint)field.Order);
            collection.Models.RemoveField(model, fieldJson);
            currentNote = collection.NewNote();
            await noteFieldView.DeleteField(field.Name, field.Order, currentNote);

            SavePrefs();
        }

        private async void FieldListViewClickEventHandler(object sender, RoutedEventArgs e)
        {
            await fieldButtonClickFunction(sender);
        }

        private async void RenameFlyoutItemClickHandler(object sender, RoutedEventArgs e)
        {
           await ShowWarnThenFieldList(RenameField);
        }

        private async Task RenameField(object sender)
        {
            await CoreWindow.GetForCurrentThread().Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                fieldToRename = (sender as Button).DataContext as NoteField;
                fieldListView.Hide();

                if (renameFieldFlyout == null)
                {
                    renameFieldFlyout = new NameEnterFlyout();
                    renameFieldFlyout.OkButtonClickEvent += RenameFieldFlyoutOkButtonClickEventHandler;
                }
                renameFieldFlyout.Show(editModelButton, fieldToRename.Name);
            });
        }

        private async void RenameFieldFlyoutOkButtonClickEventHandler(object sender, RoutedEventArgs e)
        {
            await CoreWindow.GetForCurrentThread().Dispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                string newName = renameFieldFlyout.NewName;
                newName = newName.Trim();
                bool isValid = await UIHelper.CheckValidName(newName, noteFieldView.fieldsViewModel.GetExistedFieldsName(), UIConst.WARN_NOTEFIELD_EXIST);
                if (!isValid)
                {
                    renameFieldFlyout.Show(editModelButton);
                    return;
                }

                var model = currentNote.Model;
                var fieldJson = model.GetNamedArray("flds").GetObjectAt((uint)fieldToRename.Order);
                collection.Models.RenameField(model, fieldJson, newName);
                currentNote = collection.NewNote();
                await noteFieldView.RenameField(newName, fieldToRename.Order, currentNote);

                SavePrefs();
            });
        }

        private async void RespositionMenuItemClickhandler(object sender, RoutedEventArgs e)
        {
            await ShowWarnThenFieldList(RepositionField);
        }

        private async Task RepositionField(object sender)
        {
            await CoreWindow.GetForCurrentThread().Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                var fieldToPosition = (sender as Button).DataContext as NoteField;
                oldFieldOrder = fieldToPosition.Order;
                fieldListView.Hide();

                if (repositionFieldFlyout == null)
                {
                    repositionFieldFlyout = new IntNumberEnterFlyout();
                    repositionFieldFlyout.OKButtonClickEvent += RepositionFieldFlyoutOKButtonClickEventHandler;
                }
                int max = noteFieldView.fieldsViewModel.Fields.Count;
                var textToShow = "Enter new position (1..." + max + ")";
                repositionFieldFlyout.Number = 1;
                repositionFieldFlyout.Show(editModelButton, textToShow, max, 1);
            });
        }

        private async void RepositionFieldFlyoutOKButtonClickEventHandler(object sender, RoutedEventArgs e)
        {
            await mainPage.CurrentDispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                var newOrder = repositionFieldFlyout.Number - 1;
                if (newOrder == oldFieldOrder)
                    return;

                var model = currentNote.Model;
                var fieldJson = model.GetNamedArray("flds").GetObjectAt((uint)oldFieldOrder);
                collection.Models.MoveField(model, fieldJson, newOrder);
                currentNote = collection.NewNote();
                await noteFieldView.MoveField(oldFieldOrder, newOrder, currentNote);

                SavePrefs();
            });
        }

        private void SavePrefs()
        {
            MainPage.UserPrefs.IsFullSyncRequire = true;
            mainPage.UpdateUserPreference();
            mainPage.SaveAndStartNewDatabaseSessionAsync();
        }

        private void EnterTutorialModeIfNeeded()
        {
            if (AllHelps.Tutorial == AllHelps.TutorialState.AddNote)
            {
                AddNewNoteEvent += TutorialAddNewNoteEventHandler;
                helpPopup = new HelpPopup();
                UIHelper.AddToGridInFull(mainGrid, helpPopup);
                helpPopup.Title = "Note Fields";
                helpPopup.SubtitleVisibility = Visibility.Collapsed;
                helpPopup.Text = "Try put some texts into both fields \"Front\" and \"Back\" "
                                + "(you will understand their meaning when viewing cards.) "
                                + "Then press on the \"Save\" icon (or Ctrl + S) to add note.";
                helpPopup.ShowWithClose();
                mainPage.NoticeMe.Stop();
                UIHelper.SetStoryBoardTarget(mainPage.BlinkingBlue, mainPage.SaveButton.Name);                
                mainPage.NoticeMe.Begin();
            }
        }

        private void TutorialAddNewNoteEventHandler()
        {
            mainPage.NoticeMe.Stop();
            AllHelps.Tutorial = AllHelps.TutorialState.ViewCard;
            Frame.GoBack();
        }
    }
}
