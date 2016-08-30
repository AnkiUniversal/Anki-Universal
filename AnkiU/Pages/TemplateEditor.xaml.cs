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
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using AnkiTemplate = AnkiU.AnkiCore.Templates.Template;

namespace AnkiU.Pages
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class TemplateEditor : Page, INightReadMode
    {        
        private MainPage mainPage;
        private Collection collection;        

        private string modelName;
        private TemplateInformationViewModel templateInformationViewModel = null;        
        
        private AsyncTaskRoutedHandler fieldButtonClickFunction;

        private FieldListView fieldListView = null;
        private NoteFieldsViewModel noteFieldsViewModel = null;
        private InputPane touchKeyboard = null;

        private HelpPopup helpPopup = null;

        private bool isNightMode = false;

        public event NoticeRoutedHandler AddFieldClick;

        public TemplateEditor()
        {
            this.InitializeComponent();            
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            mainPage = e.Parameter as MainPage;
            if (mainPage == null)
                throw new Exception("Wrong input parameter!");
            ShowProgessRing();
            collection = mainPage.Collection;
            collection.Database.SaveTransactionPoint();
            var model = collection.Models.GetCurrent(false);
            modelName = "Note type: " + model.GetNamedString("name");

            SetupTemplateInformationView();
            SetupTemplateView();

            ShowAllButtons();
            EnterTutorialModeIfNeeded();
            HookAllMethods();            
        }        

        private void ShowProgessRing()
        {
            mainPage.IsCanNavigateBack = false;
            progressRing.Visibility = Visibility.Visible;
            progressRing.IsActive = true;
        }

        private void SetupTemplateInformationView()
        {
            templateInformationViewModel = new TemplateInformationViewModel(collection.Models, false);
            templateInformationView.ViewModel = templateInformationViewModel;
            templateInformationView.ChangeSelectedItem(0);            
            templateInformationView.ComboBoxSelectionChangedEvent += ComboBoxSelectionChangedEventHandler;
            templateInformationView.SaveEvent += (s, e) => { mainPage.SaveAndStartNewDatabaseSessionAsync(); };
            templateInformationView.FlipButtonClick += FlipTemplateButtonClickHandler;
        }

        private async void FlipTemplateButtonClickHandler(object sender, RoutedEventArgs e)
        {            
            string question = templateView.CardTemplate.GetNamedString("qfmt");
            string answer = templateView.CardTemplate.GetNamedString("afmt");
            
            Match match = CardInformationViewModel.AnswerRegex.Match(answer);
            if(!match.Success)
            {
                await UIHelper.ShowMessageDialog("Couldn't find the line separating question (front) and answer (back).");
                return;
            }
            string answerFormatBeforeLine = answer.Substring(0, match.Index + match.Length);
            string answerFormatAfterLine = answer.Substring(match.Index + match.Length);
            templateView.CardTemplate["afmt"] = JsonValue.CreateStringValue(answerFormatBeforeLine + question);
            templateView.CardTemplate["qfmt"] = JsonValue.CreateStringValue(answerFormatAfterLine);
            templateView.CardTemplate = templateView.CardTemplate;
            SaveModel();
        }

        private void SetupTemplateView()
        {
            var css = templateInformationViewModel.CurrentModel.GetNamedString("css");            
            var template = templateInformationViewModel.TemplatesJson.GetObjectAt(0);
            templateView.Css = css;
            templateView.CardTemplate = template;
            templateView.WebviewButtonClickEvent += WebviewButtonClickEventHandler;
            templateView.InitCompleted += TemplateViewInitCompleted;
        }

        private async void ComboBoxSelectionChangedEventHandler(object sender, RoutedEventArgs e)
        {
            await SaveTemplate();
            var templateInfor = (sender as ComboBox).SelectedItem as TemplateInformation;
            var template = templateInformationViewModel.TemplatesJson.GetObjectAt(templateInfor.Ord);
            templateView.CardTemplate = template;            
        }

        private async void WebviewButtonClickEventHandler(object sender)
        {
            await mainPage.CurrentDispatcher.RunAsync(CoreDispatcherPriority.Normal, async () => { 
               string command = sender as string;
                switch(command)
                {
                    case "save":
                        mainPage.SaveButtonClickAnimateAsync();
                        await SaveTemplate();                        
                        break;
                    case "addField":
                        AddFieldButtonhandler();                        
                        break;
                    case "addCloze":
                        await AddClozeButtonhandler();
                        break;
                    case "addTypeFront":
                        await AddTypeFrontButtonhandler();
                        break;
                    case "addTypeBack":
                        await AddTypeBackButtonhandler();
                        break;
                    case ("groupbutton"):
                        HideTouchKeyboad();
                        break;
                    case "stylecode":
                        await StyleCodeButtonhandler();
                        break;
                    case ("forecolor"):
                        templateView.HtmlEditor.ShowForeColorPickerFlyout(templateInformationView, FlyoutPlacementMode.Bottom);
                        break;
                    case ("backcolor"):
                        templateView.HtmlEditor.ShowBackColorPickerFlyout(templateInformationView, FlyoutPlacementMode.Bottom);
                        break;
                    default:
                        break;
                }
            });
        }

        private void HideTouchKeyboad()
        {
            if(touchKeyboard != null)
                touchKeyboard.TryHide();
        }

        private async void TemplateViewInitCompleted()
        {
            await mainPage.CurrentDispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                HideProgessRing();
            });
        }

        private void HideProgessRing()
        {
            mainPage.IsCanNavigateBack = true;
            progressRing.IsActive = false;
            progressRing.Visibility = Visibility.Collapsed;
        }

        private async Task SaveTemplate()
        {
            await templateView.HtmlEditor.ForceNotifyContentChanged();
            SaveModel();
        }

        private void AddFieldButtonhandler()
        {
            ShowFlyout();
            fieldButtonClickFunction = AddField;            
        }

        private async Task AddClozeButtonhandler()
        {
            if((ModelType)templateInformationViewModel.CurrentModel.GetNamedNumber("type") != ModelType.CLOZE)
            {
                await UIHelper.ShowMessageDialog(UIConst.WARN_NOTCLOZETYPE);
                return;
            }

            await templateView.HtmlEditor.ForceNotifyContentChanged();
            if(await CheckIfClozeFieldExist())            
                return;

            ShowFlyout();                        
            fieldButtonClickFunction = AddClozeField;
        }

        private async Task<bool> CheckIfClozeFieldExist()
        {
            var isHasCloze = AnkiTemplate.ClozeRegex.IsMatch(templateView.CardTemplate.GetNamedString("qfmt"));
            if (isHasCloze)
            {
                await UIHelper.ShowMessageDialog("You can only have one cloze field per template.");
                return true;
            }

            isHasCloze = AnkiTemplate.ClozeRegex.IsMatch(templateView.CardTemplate.GetNamedString("afmt"));
            if (isHasCloze)
            {
                await UIHelper.ShowMessageDialog("Please delete the cloze field on the back first.");
                return true;
            }
            return false;
        }

        private void ShowFlyout()
        {
            InitFlyoutAndViewModelIfNeeded();
            fieldListView.Show(templateInformationView);
        }

        private void InitFlyoutAndViewModelIfNeeded()
        {
            if (fieldListView == null)
            {
                fieldListView = new FieldListView();
                noteFieldsViewModel = new NoteFieldsViewModel(templateInformationView.ViewModel.CurrentModel);
                fieldListView.SetDataContext(noteFieldsViewModel);
                fieldListView.FieldClickEvent += FieldFlyoutButtonClickHandler;
            }
        }

        private async Task AddTypeFrontButtonhandler()
        {
            await templateView.HtmlEditor.ForceNotifyContentChanged();
            if (await CheckTypeFieldExist())
                return;

            ShowFlyout();
            fieldButtonClickFunction = AddTypeField;
        }

        private async Task<bool> CheckTypeFieldExist()
        {
            var isHas = AnkiTemplate.TypeRegex.IsMatch(templateView.CardTemplate.GetNamedString("qfmt"));
            if (isHas)
            {
                await UIHelper.ShowMessageDialog("You can only have one type field");
                return true;
            }
            return false;
        }

        private async Task AddTypeBackButtonhandler()
        {
            await UIHelper.ShowMessageDialog("You shouldn't add a type field to backside.");
        }

        private async void FieldFlyoutButtonClickHandler(object sender, RoutedEventArgs e)
        {
            await fieldButtonClickFunction(sender);
        }

        private async Task AddField(object sender)
        {
            var field = (sender as Button).DataContext as NoteField;
            string text = "{{" + field.Name + "}}";
            await templateView.HtmlEditor.InsertHtml(text);
            fieldListView.Hide();
            AddFieldClick?.Invoke();
        }

        private async Task AddClozeField(object sender)
        {
            var field = (sender as Button).DataContext as NoteField;
            string text = "{{cloze:" + field.Name + "}}";
            await templateView.InsertIntoAllFields(text);
            fieldListView.Hide();
        }

        private async Task AddTypeField(object sender)
        {
            var field = (sender as Button).DataContext as NoteField;
            string text = "{{type:" + field.Name + "}}";
            await templateView.HtmlEditor.InsertHtml(text);
            fieldListView.Hide();
        }

        private void SaveModel()
        {
            collection.Models.Save(templateInformationViewModel.CurrentModel);
            templateView.HtmlEditor.IsModified = false;
            templateView.HtmlEditor.IsContentCheckOnce = true;
            mainPage.SaveAndStartNewDatabaseSessionAsync();
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            UnHookAllMethods();
            HideAllButtons();

            base.OnNavigatingFrom(e);
        }

        protected async override void OnNavigatedFrom(NavigationEventArgs e)
        {
            await templateView.HtmlEditor.ForceNotifyContentChanged();
            collection.Models.Save(templateInformationViewModel.CurrentModel, false);
            collection.SaveAndCommit();
 
            templateView.HtmlEditor.ClearWebViewControl();

            base.OnNavigatedFrom(e);      
        }

        private void ShowAllButtons()
        {
            mainPage.ZoomButtonsSeparator.Visibility = Visibility.Visible;       
            if(mainPage.WindowSizeState == WindowSizeState.narrow)
                mainPage.MoveZoomButtonToPrimary();
            mainPage.IsAutoSwitchZoomButtonToSecondary = false;
            mainPage.SaveButton.Visibility = Visibility.Visible;            
        }

        private void HideAllButtons()
        {           
            mainPage.ZoomButtonsSeparator.Visibility = Visibility.Collapsed;
            if (mainPage.WindowSizeState == WindowSizeState.narrow)            
                mainPage.MoveZoomButtonToSecondary();
            mainPage.IsAutoSwitchZoomButtonToSecondary = true;
            mainPage.SaveButton.Visibility = Visibility.Collapsed;            
        }

        private void HookAllMethods()
        {
            touchKeyboard = InputPane.GetForCurrentView();
            if (touchKeyboard != null)
                touchKeyboard.Hiding += TouchInputHiding;

            mainPage.SaveButton.Click += SaveButtonClickHandler;
            mainPage.EnableChangingReadMode(this, templateView.HtmlEditor);
            ChangeBackgroundColor();
            mainPage.HookZooming(templateView);
        }   

        private void UnHookAllMethods()
        {
            if (touchKeyboard != null)
                touchKeyboard.Hiding -= TouchInputHiding;

            mainPage.SaveButton.Click -= SaveButtonClickHandler;
            mainPage.DisableChangingReadMode();
            mainPage.UnhookZooming();
        }

        private async void TouchInputHiding(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            await templateView.HtmlEditor.ForceNotifyContentChanged();
        }

        private void SaveButtonClickHandler(object sender, RoutedEventArgs e)
        {
            SaveModel();
        }

        private async Task StyleCodeButtonhandler()
        {
            string css = templateInformationViewModel.CurrentModel.GetNamedString("css");
            RichEditBoxContentDialog dialog = new RichEditBoxContentDialog(css);
            await dialog.ShowAsync();
            css = dialog.Text;
            if(css != null)
            {
                templateInformationViewModel.CurrentModel["css"] = JsonValue.CreateStringValue(css);
                templateView.Css = css;
                collection.Models.Save(templateInformationViewModel.CurrentModel);
            }
        }

        public void ToggleReadMode()
        {
            isNightMode = !isNightMode;
            ChangeBackgroundColor();
        }

        private void ChangeBackgroundColor()
        {
            UIHelper.ToggleNightLight(isNightMode, userControl);
            if(helpPopup != null)            
                helpPopup.ChangeReadMode(isNightMode);            
        }

        private void EnterTutorialModeIfNeeded()
        {
            if(AllHelps.Tutorial == AllHelps.TutorialState.Template)
            {
                helpPopup = new HelpPopup();
                UIHelper.AddToGridInFull(mainGrid, helpPopup);
                StartTutorial();
            }
        }

        private void StartTutorial()
        {
            helpPopup.Title = "Template";
            helpPopup.SubtitleVisibility = Visibility.Collapsed;
            helpPopup.CloseXVisibility = Visibility.Collapsed;
            helpPopup.Text = "A template always has a FRONTSIDE and a BACKSIDE.\n"
                            + "FRONTSIDE is the first view of your card while BACKSIDE is shown after you've pressed \"Show Answer\".";
            helpPopup.NextEvent = FirstHelpPopupNext;
            helpPopup.SetOffSet(0, 50);
            helpPopup.ShowWithNext();
        }

        private void FirstHelpPopupNext()
        {            
            helpPopup.NextEvent = SecondHelpPopupNext;
            helpPopup.BackEvent = StartTutorial;

            helpPopup.Text = "Fields of note type must be marked by \"{{...}}\" or else they are treated as normal texts.\n"
                            + "If FRONTSIDE only contains \"{{Front}}\", then it will only show contents put in the \"Front\" field.";
            helpPopup.ShowWithNextAndBack();
        }

        private void SecondHelpPopupNext()
        {                        
            helpPopup.NextEvent = ThirdHelpPopupNext;
            helpPopup.BackEvent = FirstHelpPopupNext;

            helpPopup.Text = "BACKSIDE normally includes all contents of FRONTSIDE by using \"{{FrontSide}}\" (Not true for cloze type.)\n"
                + "If this is not desirable, you can delete it.";
            helpPopup.ShowWithNextAndBack();
        }

        private void ThirdHelpPopupNext()
        {
            helpPopup.NextEvent -= ThirdHelpPopupNext;
            helpPopup.BackEvent = SecondHelpPopupNext;

            helpPopup.Text = "Try adding a field into FRONTSIDE by pressing on its content area then press on the \"{{...}}\" button.\n (Certain features like font color and family require a physical keyboard.)";
            AddFieldClick += TutorialAddFieldClick;
            helpPopup.ShowWithBackAndClose();
        }

        private void TutorialAddFieldClick()
        {            
            AddFieldClick -= TutorialAddFieldClick;
            helpPopup.CloseXVisibility = Visibility.Collapsed;
            helpPopup.NextEvent = FourthNextEvent;
            helpPopup.Title = "Template";
            helpPopup.Text = "Once you've changed contents of a template, all cards generated from that template will also change.";            
            helpPopup.ShowWithNext();
        }

        private void FourthNextEvent()
        {            
            helpPopup.BackEvent = TutorialAddFieldClick;
            helpPopup.NextEvent = FifthHelpPopupNext;

            helpPopup.Title = "Why Need Templates?";
            helpPopup.Text = "By using templates you can change the look of your cards anytime.\n" 
                            + "Ex: you can import a shared deck, then edit its templates to suit your style without having to go over 1000+ cards again.";            
            helpPopup.ShowWithNextAndBack();
        }

        private void FifthHelpPopupNext()
        {
            helpPopup.BackEvent = FourthNextEvent;
            helpPopup.NextEvent = SixthHelpPopupNext;

            helpPopup.Title = "Two or More Templates";
            helpPopup.Text = "You can have more than one template for each note type (except cloze)." 
                            + " Each template will generate a new card.\n" 
                            + "Ex: if you have two templates, each note you add will normally create two cards.";
            helpPopup.ShowWithNextAndBack();
        }

        private void SixthHelpPopupNext()
        {
            helpPopup.NextEvent -= SixthHelpPopupNext;
            helpPopup.BackEvent = FifthHelpPopupNext;            

            templateInformationView.AddTemplateEvent += TutorialAddTemplateEvent;

            helpPopup.Title = "Add a Template";
            helpPopup.Text = "Now try adding a template named \"Reverse\" by pressing on the \"Add\" icon.";
            templateInformationView.BeginAnimation();
            helpPopup.ShowWithBackAndClose();
        }

        private void TutorialAddTemplateEvent()
        {
            helpPopup.BackEvent -= SixthHelpPopupNext;
            templateInformationView.AddTemplateEvent -= TutorialAddTemplateEvent;
            templateInformationView.FlipCardEvent += TutorialReverseFrontBackEvent;
            templateInformationView.SetAnimationOnEdit();
            templateInformationView.BeginAnimation();

            helpPopup.Title = "Reverse Front and Back";
            helpPopup.Text = "You can manually reverse contents of FRONTSIDE and BACKSIDE or press on the \"Edit\" icon then choose \"Switch Front and Back\"";
            templateInformationView.BeginAnimation();
            helpPopup.ShowWithClose();
        }

        private void TutorialReverseFrontBackEvent()
        {
            templateInformationView.FlipCardEvent -= TutorialReverseFrontBackEvent;
            templateInformationView.StopAnimation();

            helpPopup.NextEvent = SeventhHelpPopup;

            helpPopup.CloseXVisibility = Visibility.Collapsed;
            helpPopup.Title = "Congratulation";
            helpPopup.Text = "You have just successfully created a reverse template. All your previous notes will automatically generate new cards from this template.";
            helpPopup.ShowWithNext();
        }

        private void SeventhHelpPopup()
        {
            helpPopup.BackEvent = TutorialReverseFrontBackEvent;
            helpPopup.NextEvent = EighthHelpPopup;

            helpPopup.Title = "Delete Template";
            helpPopup.Text = "To delete the newly created template, please press on the \"Delete\" icon. This will also remove all cards generated from this template.";
            helpPopup.ShowWithNextAndBack();
        }

        private void EighthHelpPopup()
        {
            AllHelps.Tutorial = AllHelps.TutorialState.NotShow;
            helpPopup.BackEvent = SeventhHelpPopup;
            helpPopup.NextEvent -= EighthHelpPopup;

            helpPopup.Title = "Finished";
            helpPopup.Text = "This concludes \"Note Types & Templates\" tutorial. " + 
                             "If you wish to learn more about template, please view \"Template with Type Field\" tutorial.";
            MainPage.UserPrefs.SetHelpShown(AllHelps.HELP_NOTE_TYPE_AND_TEMPLATE, true);
            helpPopup.ShowWithBackAndClose();
        }
    }
}
