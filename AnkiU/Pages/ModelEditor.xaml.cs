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
using AnkiU.UserControls;
using AnkiU.ViewModels;
using AnkiU.Views;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace AnkiU.Pages
{
    public sealed partial class ModelEditor : Page, INightReadMode
    {        
        private bool isNightMode = false;

        private const string CHANGE_TO_STD = "Change to Standard";
        private const string CHANGE_TO_CLOZE = "Change to Cloze";

        private MainPage mainPage;
        private Collection collection;
        private JsonObject currentModel;
        private AnkiModelInfomartionViewModel modelViewModel;
        private TemplateInformationViewModel templateViewModel;
        private NoteFieldsViewModel fieldsViewModel;
        private MenuFlyout fieldMenu;
        private NoteField fieldShowMenu;        

        private NameEnterFlyout renameNoteTypeFlyout = null;
        private NameEnterFlyout addFieldFlyout = null;
        private NameEnterFlyout renameFieldFlyout = null;        
        private IntNumberEnterFlyout repositionFieldFlyout = null;

        private HelpPopup helpPopup = null;

        public event NoticeRoutedHandler AddFieldEvent;

        public ModelEditor()
        {
            this.InitializeComponent();
            fieldMenu = Resources["fieldMenuFlyout"] as MenuFlyout;
            fieldMenu.Placement = FlyoutPlacementMode.Bottom;
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            mainPage = e.Parameter as MainPage;
            if (mainPage == null)
                throw new Exception("Wrong parameter type!");
            collection = mainPage.Collection;            
            SetupAnkiModelView();

            EnterTutorialModeIfNeeded();
            HookAllEvents();
        }      

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            UnHookAllEvents();

            base.OnNavigatingFrom(e);
        }

        private void HookAllEvents()
        {
            mainPage.CommanBar.ClosedDisplayMode = AppBarClosedDisplayMode.Minimal;
            mainPage.EnableChangingReadMode(this);
            ChangeBackgroundColor();
        }

        private void UnHookAllEvents()
        {
            mainPage.CommanBar.ClosedDisplayMode = AppBarClosedDisplayMode.Compact;
            mainPage.DisableChangingReadMode();
        }        

        private void SetupAnkiModelView()
        {
            modelInformationView.ComboBoxSelectionChangedEvent -= ModelInformationViewComboBoxSelectionChangedEventHandler;
            modelViewModel = new AnkiModelInfomartionViewModel(collection.Models.All());
            modelInformationView.DataContext = modelViewModel.Models;
            modelInformationView.Label = "Note type:";

            var model = collection.Models.GetCurrent(false);
            if (model != null)
                modelInformationView.ChangeSelectedItem((long)model.GetNamedNumber("id"));
            else
            {
                modelInformationView.ChangeSelectedIndex(0);
                collection.Models.SetCurrent(modelInformationView.GetSelectedModelId());
            }
            modelInformationView.ComboBoxSelectionChangedEvent += ModelInformationViewComboBoxSelectionChangedEventHandler;
            UpdateModelInformation();
        }

        private void UpdateModelInformation()
        {
            templateViewModel = new TemplateInformationViewModel(collection.Models, false);
            currentModel = collection.Models.GetCurrent(false);
            fieldsViewModel = new NoteFieldsViewModel(currentModel);
            fieldListView.DataContext = fieldsViewModel.Fields;
            totalNoteTextBlock.Text = collection.Models.NoteUseCount(currentModel).ToString();
            totalTemplatsTextBlock.Text = templateViewModel.Templates.Count.ToString();
            var type = (ModelType)currentModel.GetNamedNumber("type");
            if (type == ModelType.CLOZE)
                changeModelType.Text = CHANGE_TO_STD;
            else
                changeModelType.Text = CHANGE_TO_CLOZE;
        }

        private void ModelInformationViewComboBoxSelectionChangedEventHandler(object sender, SelectionChangedEventArgs e)
        {
            collection.Models.SetCurrent(modelInformationView.GetSelectedModelId());
            UpdateModelInformation();
        }

        private void EditTemplatesMenuClickHandler(object sender, RoutedEventArgs e)
        {
            Frame.Navigate(typeof(TemplateEditor), mainPage);
        }

        public void ToggleReadMode()
        {
            isNightMode = !isNightMode;
            ChangeBackgroundColor();
        }

        private void ChangeBackgroundColor()
        {
            UIHelper.ToggleNightLight(isNightMode, userControl);
            if (isNightMode)
                fieldListroot.Background = new SolidColorBrush(Windows.UI.Colors.Black);
            else
                fieldListroot.Background = new SolidColorBrush(Windows.UI.Colors.White);

            if (helpPopup != null)
                helpPopup.ChangeReadMode(isNightMode);
        }

        private async void AddMenuFlyoutItemClickHandler(object sender, RoutedEventArgs e)
        {
            var isContinue = await MainPage.WarnFullSyncIfNeeded();
            if (!isContinue)
                return;

            InitAddFieldFlyout();
            addFieldFlyout.Show(pointToShowFlyout, "");
        }

        private void InitAddFieldFlyout()
        {
            if (addFieldFlyout == null)
            {
                addFieldFlyout = new NameEnterFlyout();
                addFieldFlyout.OkButtonClickEvent += AddFieldFlyoutOkButtonClickEventHandler;
                addFieldFlyout.Placement = FlyoutPlacementMode.Bottom;
            }
        }

        private async void AddFieldFlyoutOkButtonClickEventHandler(object sender, RoutedEventArgs e)
        {
            await mainPage.CurrentDispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                string name = addFieldFlyout.NewName;
                var isValid = await UIHelper.CheckValidName(name, fieldsViewModel.GetExistedFieldsName(), UIConst.WARN_NOTEFIELD_EXIST);

                if (!isValid)
                {
                    addFieldFlyout.Show(pointToShowFlyout);
                    addFieldFlyout.Placement = FlyoutPlacementMode.Bottom;
                    return;
                }

                var newField = collection.Models.NewField(name);
                collection.Models.AddField(currentModel, newField);

                var fieldJson = currentModel.GetNamedArray("flds").GetObjectAt((uint)(fieldsViewModel.Fields.Count));
                var newFieldOrder = fieldShowMenu.Order + 1;
                collection.Models.MoveField(currentModel, fieldJson, newFieldOrder);
                fieldsViewModel.Fields.Insert(newFieldOrder, new NoteField(0, name, newFieldOrder, null));
                fieldsViewModel.UpdateFieldOrder();
                SavePrefs();
                AddFieldEvent?.Invoke();
            });
        }

        private async void RenameMenuFlyoutItemClickHandler(object sender, RoutedEventArgs e)
        {
            var isContinue = await MainPage.WarnFullSyncIfNeeded();
            if (!isContinue)
                return;

            if (renameFieldFlyout == null)
            {
                renameFieldFlyout = new NameEnterFlyout();
                renameFieldFlyout.Placement = FlyoutPlacementMode.Bottom;
                renameFieldFlyout.OkButtonClickEvent += RenameFieldFlyoutOkButtonClickEventHandler;
            }
            renameFieldFlyout.Show(pointToShowFlyout, fieldShowMenu.Name);
        }

        private async void RenameFieldFlyoutOkButtonClickEventHandler(object sender, RoutedEventArgs e)
        {
            await mainPage.CurrentDispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                string newName = renameFieldFlyout.NewName;
                bool isValid = await UIHelper.CheckValidName(newName, fieldsViewModel.GetExistedFieldsName(), UIConst.WARN_NOTEFIELD_EXIST);
                if (!isValid)
                {
                    renameFieldFlyout.Show(pointToShowFlyout);
                    return;
                }

                var fieldJson = currentModel.GetNamedArray("flds").GetObjectAt((uint)fieldShowMenu.Order);
                collection.Models.RenameField(currentModel, fieldJson, newName);
                fieldsViewModel.Fields[fieldShowMenu.Order].Name = newName;

                SavePrefs();
            });
        }

        private async void RepositionMenuFlyoutItemClickHandler(object sender, RoutedEventArgs e)
        {
            var isContinue = await MainPage.WarnFullSyncIfNeeded();
            if (!isContinue)
                return;

            if (repositionFieldFlyout == null)
            {
                repositionFieldFlyout = new IntNumberEnterFlyout();
                repositionFieldFlyout.Placement = FlyoutPlacementMode.Bottom;
                repositionFieldFlyout.OKButtonClickEvent += RepositionFieldFlyoutOKButtonClickEventHandler;
            }
            int max = fieldsViewModel.Fields.Count;
            var textToShow = "Enter new position (1..." + max + ")";
            repositionFieldFlyout.Number = 1;
            repositionFieldFlyout.Show(pointToShowFlyout, textToShow, max, 1);
        }

        private async void RepositionFieldFlyoutOKButtonClickEventHandler(object sender, RoutedEventArgs e)
        {
            await mainPage.CurrentDispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                var newOrder = repositionFieldFlyout.Number - 1;
                var fieldJson = currentModel.GetNamedArray("flds").GetObjectAt((uint)fieldShowMenu.Order);
                collection.Models.MoveField(currentModel, fieldJson, newOrder);
                fieldsViewModel.MoveField(fieldShowMenu.Order, newOrder);
                SavePrefs();
            });
        }

        private async void DeleteMenuFlyoutItemClickHandler(object sender, RoutedEventArgs e)
        {
            var isContinue = await MainPage.WarnFullSyncIfNeeded();
            if (!isContinue)
                return;

            var noteCount = collection.Models.NoteUseCount(currentModel);
            isContinue = await UIHelper.AskUserConfirmation(String.Format(UIConst.WARN_DELETE_FIELD, noteCount));
            if (!isContinue)
                return;
                        
            var fieldJson = currentModel.GetNamedArray("flds").GetObjectAt((uint)fieldShowMenu.Order);
            collection.Models.RemoveField(currentModel, fieldJson);
            fieldsViewModel.Fields.RemoveAt(fieldShowMenu.Order);
            fieldsViewModel.UpdateFieldOrder();

            SavePrefs();
        }

        private void SavePrefs()
        {
            var task = Task.Run(() =>
            {
                MainPage.UserPrefs.IsFullSyncRequire = true;
                mainPage.UpdateUserPreference();
                collection.SaveAndCommit();
            });
        }

        private void RenameMenuFlyoutItemClick(object sender, RoutedEventArgs e)
        {
            if (renameNoteTypeFlyout == null)
            {
                renameNoteTypeFlyout = new NameEnterFlyout();                
                renameNoteTypeFlyout.OkButtonClickEvent += RenameNoteTypeFlyoutOkButtonClickEventHandler;
                renameNoteTypeFlyout.Placement = FlyoutPlacementMode.Bottom;
            }
            renameNoteTypeFlyout.Show(editModelButton, modelInformationView.CurrentName());
        }

        private async void RenameNoteTypeFlyoutOkButtonClickEventHandler(object sender, RoutedEventArgs e)
        {
            await mainPage.CurrentDispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () =>
            {
                var newName = renameNoteTypeFlyout.NewName;
                var isValid = await UIHelper.CheckValidName(newName, modelViewModel.Models, UIConst.WARN_NOTETYPE_EXIST);
                if (!isValid)
                {
                    renameNoteTypeFlyout.Show(editModelButton);
                    return;
                }

                currentModel["name"] = JsonValue.CreateStringValue(newName);
                modelInformationView.ChangeSelectedItemName(newName);

                collection.Models.Save(currentModel);
                collection.SaveAndCommitAsync();
            });
        }

        private async void ChangeModelTypeClickHandler(object sender, RoutedEventArgs e)
        {
            bool isContinue = await MainPage.WarnFullSyncIfNeeded();
            if (!isContinue)
                return;

            if ((ModelType)currentModel.GetNamedNumber("type") == ModelType.STD)
            {
                if (templateViewModel.Templates.Count > 1)
                {
                    await UIHelper.ShowMessageDialog("Cloze note type can only have one template.\n" +
                                                     "Please choose \"Edit templates\" and remove redundant templates first.",
                                                     "Error!");
                    return;
                }
                currentModel["type"] = JsonValue.CreateNumberValue((int)ModelType.CLOZE);
                var css = currentModel.GetNamedString("css");
                if (!css.Contains(".cloze"))
                {
                    currentModel["css"] = JsonValue.CreateStringValue(css
                                                                     + ".cloze {"
                                                                     + "font-weight: bold;"
                                                                     + "color: blue;" + "}");
                }
                changeModelType.Text = CHANGE_TO_STD;
                await UIHelper.ShowMessageDialog("Change to cloze note successed.\n" + 
                                                 "You still need to add one cloze field into the template before you can add note with cloze contents.");
            }
            else
            {
                currentModel["type"] = JsonValue.CreateNumberValue((int)ModelType.STD);
                changeModelType.Text = CHANGE_TO_CLOZE;
                await UIHelper.ShowMessageDialog("Change to standard note successed. You can now add more than one template.\n" +
                                                 "Please be aware that all cloze contents will now no longer work.\n" +
                                                 "You can change back to cloze if this is undesirable.");
            }
            collection.Models.Save(currentModel);
            collection.SaveAndCommitAsync();
            MainPage.UserPrefs.IsFullSyncRequire = true;
        }

        private async void DeleteModelButtonClickHandler(object sender, RoutedEventArgs e)
        {
            //python ver sometime uses null for no deck. So we have to take if it's null or not first
            var deckIdValue = currentModel.GetNamedValue("did", JsonValue.CreateNullValue());
            if (deckIdValue.ValueType != JsonValueType.Null)
            {
                var deckId = (long)deckIdValue.GetNumber();
                if (deckId != Constant.DEFAULTDECK_ID && collection.Deck.HasDeckId(deckId))
                {
                    var deckName = collection.Deck.GetDeckName(deckId);
                    await UIHelper.ShowMessageDialog("Unable to delete. This note type is used by deck " + deckName + ".");
                    return;
                }
            }

            var noteCounts = collection.Models.NoteUseCount(currentModel);
            if (noteCounts != 0)
            {
                await UIHelper.ShowMessageDialog("Unable to delete. This note type has " + noteCounts + " note(s).");
                return;
            }            

            var isContinue = await MainPage.WarnFullSyncIfNeeded();
            if (!isContinue)
                return;

            isContinue = await UIHelper.AskUserConfirmation("Are you sure you want to delete this unused note type?");
            if (!isContinue)
                return;

            collection.Models.Remove(currentModel, false);            
            SetupAnkiModelView();
            SavePrefs();
        }

        private void FieldButtonClick(object sender, RoutedEventArgs e)
        {
            var buttonShowMenu = sender as Button;
            fieldShowMenu = buttonShowMenu.DataContext as NoteField;            
        }

        private void fieldButtonTapped(object sender, TappedRoutedEventArgs e)
        {
            var pointerPosition = e.GetPosition(fieldListroot);
            pointToShowFlyout.Margin = new Thickness(pointerPosition.X, pointerPosition.Y, 0, 0);
            fieldMenu.ShowAt(pointToShowFlyout);
        }

        private void EnterTutorialModeIfNeeded()
        {
            if (AllHelps.Tutorial == AllHelps.TutorialState.NoteType)
            {
                helpPopup = new HelpPopup();
                UIHelper.AddToGridInFull(mainGrid, helpPopup);
                helpPopup.CloseXVisibility = Visibility.Collapsed;
                helpPopup.Title = "Note Type";
                helpPopup.SubTitle = "(Access this from \"Manage Note Types\")";
                helpPopup.Text = AllHelps.NOTE_TYPE_DEFINITION;
                helpPopup.NextEvent = HelpPopupNextEventHandler;
                helpPopup.ShowWithNext();           
            }
        }

        private void HelpPopupNextEventHandler()
        {
            helpPopup.Title = "Template";
            helpPopup.SubtitleVisibility = Visibility.Collapsed;
            helpPopup.Text = AllHelps.TEMPLATE_DEFINITION +
                                "\nPlease press on the \"Edit\" icon then choose \"Edit Templates\".";
            editModelButton.Click += EditModelButtonClickHandler;
            AllHelps.Tutorial = AllHelps.TutorialState.Template;
            NoticeMe.Begin();
            helpPopup.Show();
        }              

        private void EditModelButtonClickHandler(object sender, RoutedEventArgs e)
        {
            editModelButton.Click -= EditModelButtonClickHandler;
            NoticeMe.Stop();
        }
    }
}
