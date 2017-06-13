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
using AnkiU.Pages;
using AnkiU.UIUtilities;
using AnkiU.UserControls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace AnkiU.Views
{
    public sealed partial class AllHelps : UserControl
    {
        private Collection collection;
        private bool isNightMode = false;
        private HelpPopup helpPopup;
        private CreditsPopup creditPopup;

        public const string HELP_DECK_NOTE = "Basic";
        public const string HELP_NOTE_TYPE_AND_TEMPLATE = "NoteTypeAndTemplate";        
        public const string HELP_CUSTOM_STUDY = "CustomStudy";
        public const string HELP_TYPE_FIELD = "TypeField";
        public const string HELP_ClOZE_FIELD = "ClozeField";
        public const string HELP_DECK_OPTION = "DeckOption";
        public const string HELP_SYNC = "Sync";
        public const string HELP_SUBDECK = "SubDeck";

        public const string NOTE_TYPE_DEFINITION = "A note type determines how many fields each note will have.\n" +
                                                "When you add, reposition, or delete fields of a note type, all notes using it will be affected.";
        public const string TEMPLATE_DEFINITION = "A template determines how each field should be shown on your cards.";

        public enum TutorialState
        {
            NotShow,
            DeckCreation,
            AddNote,
            ViewCard,
            SharedDeck,
            NoteType,
            Template
        }
        public static TutorialState Tutorial { get; set; }

        public Frame ContentFrame { get; set; }
        public MainPage MainPage { get; set; }
        public SplitView SplitView { get; set; }

        public event NoticeRoutedHandler HelpClose;

        public AllHelps(Frame contentFrame, MainPage mainPage, SplitView splitView)
        {
            this.InitializeComponent();
            ContentFrame = contentFrame;
            MainPage = mainPage;
            SplitView = splitView;
            collection = mainPage.Collection;

            SetupTutorialsVisibility();
        }

        public void SetupTutorialsVisibility()
        {
            if (MainPage.UserPrefs.IsHelpAlreadyShown(HELP_NOTE_TYPE_AND_TEMPLATE))
                SetTemplateAndTypeFieldTutVisible();
            else
                templateWithTypeField.Visibility = Visibility.Collapsed;

            if (MainPage.UserPrefs.IsHelpAlreadyShown(HELP_DECK_OPTION))
                DeckOption.Visibility = Visibility.Visible;

            SetupFlag();
        }

        private void SetupFlag()
        {
            if (MainPage.UserPrefs.IsHelpAlreadyShown(HELP_DECK_NOTE))
                DecksAndNotesFlag.Visibility = Visibility.Collapsed;
            if (MainPage.UserPrefs.IsHelpAlreadyShown(HELP_NOTE_TYPE_AND_TEMPLATE))
                NoteTypeAndTemplateFlag.Visibility = Visibility.Collapsed;
            if (MainPage.UserPrefs.IsHelpAlreadyShown(HELP_CUSTOM_STUDY))
                CustomStudyFlag.Visibility = Visibility.Collapsed;
            if (MainPage.UserPrefs.IsHelpAlreadyShown(HELP_TYPE_FIELD))
                TypeFieldFlag.Visibility = Visibility.Collapsed;
            if (MainPage.UserPrefs.IsHelpAlreadyShown(HELP_ClOZE_FIELD))
                ClozeFlag.Visibility = Visibility.Collapsed;
            if (MainPage.UserPrefs.IsHelpAlreadyShown(HELP_DECK_OPTION))
                DeckOptionFlag.Visibility = Visibility.Collapsed;
            if (MainPage.UserPrefs.IsHelpAlreadyShown(HELP_SYNC))
                DataSyncingFlag.Visibility = Visibility.Collapsed;

        }

        private void SetTemplateAndTypeFieldTutVisible()
        {
            templateWithTypeField.Visibility = Visibility.Visible;
            if (!MainPage.UserPrefs.IsHelpAlreadyShown(HELP_TYPE_FIELD))
                templateWithClozeField.Visibility = Visibility.Collapsed;
        }

        #region Add Deck And Note type
        private void AddDeckAndNoteClick(object sender, RoutedEventArgs e)
        {            
            if (MainPage.UserPrefs.IsHelpAlreadyShown(HELP_DECK_NOTE))
            {
                InitHelpPopupIfNeeded();
                StartAddECkAndNotehelp();
            }
            else
            {
                HideSplitView();
                MainPage.UserPrefs.SetHelpShown(HELP_DECK_NOTE, true);
                DecksAndNotesFlag.Visibility = Visibility.Collapsed;
                AllHelps.Tutorial = AllHelps.TutorialState.DeckCreation;
                ReNavigateToDeckSelectPage();
            }
        }

        private void StartAddECkAndNotehelp()
        {
            helpPopup.Title = "Deck";
            helpPopup.SubtitleVisibility = Visibility.Collapsed;
            helpPopup.Text = "Deck is the place to store cards that belong to a certain subject, such as \"Math\", \"Japanese Vocabulary\", etc.";
            helpPopup.NextEvent = FirstAddDeckAndNoteNext;
            helpPopup.ShowWithNext();
        }

        private void FirstAddDeckAndNoteNext()
        {
            helpPopup.BackEvent = StartAddECkAndNotehelp;
            helpPopup.NextEvent = SecondAddDeckAndNoteNext;

            helpPopup.Title = "Deck";            
            helpPopup.Text = "Please avoid creating small and specific decks like \"Math First Grade\", as you may take hints from their name and it's alway easier to just recall a handful of cards.";
            helpPopup.ShowWithNextAndBack();
        }

        private void SecondAddDeckAndNoteNext()
        {
            helpPopup.BackEvent = FirstAddDeckAndNoteNext;
            helpPopup.NextEvent = ThirdAddDeckAndNoteNext;

            helpPopup.Title = "Note";
            helpPopup.Text = "Note is the place to store contents of your cards. Instead of creating many small decks, you should add tags to note, such as \"MathFirstGrade\". This way you can easily extract a sub-deck by using tags.";
            helpPopup.ShowWithNextAndBack();
        }

        private void ThirdAddDeckAndNoteNext()
        {
            helpPopup.BackEvent = SecondAddDeckAndNoteNext;
            helpPopup.NextEvent -= SecondAddDeckAndNoteNext;

            helpPopup.Title = "Note";
            helpPopup.Text = "To change how your cards will display contents from notes, please view \"Note Types & Templates\". To extract sub-decks please view \"Custom Study\".";
            helpPopup.ShowWithBackAndClose();
        }
        #endregion

        #region CustomStudy
        private void CustomStudy(object sender, RoutedEventArgs e)
        {
            MainPage.UserPrefs.SetHelpShown(HELP_CUSTOM_STUDY, true);
            CustomStudyFlag.Visibility = Visibility.Collapsed;
            InitHelpPopupIfNeeded();
            CustomStudyStart();
        }

        private void CustomStudyStart()
        {
            helpPopup.Title = "Custom Study";
            helpPopup.SubtitleVisibility = Visibility.Collapsed;
            helpPopup.Text = "If you want to study more than normal or create some decks with a specific constraint, then this is what you are looking for.";
            helpPopup.NextEvent = FirstCustomStudyNext;
            helpPopup.ShowWithNext();
        }

        private void FirstCustomStudyNext()
        {
            helpPopup.BackEvent = CustomStudyStart;
            helpPopup.NextEvent = SecondCustomStudyNext;
            helpPopup.Text = "To use this feature, please left-click (or touch) on a deck with zero new and due cards (you must finish learning and reviewing its today's cards first)";
            helpPopup.ShowWithNextAndBack();
        }

        private void SecondCustomStudyNext()
        {
            helpPopup.BackEvent = FirstCustomStudyNext;
            helpPopup.NextEvent -= SecondCustomStudyNext;
            helpPopup.Text = "The fisrt two options will let you learn more than normal. All other options will create a deck named \"Custom Study Session\". This deck will automatically be deleted after you have viewed all its cards. Once deleted, all cards will be returned to the original deck.";
            helpPopup.ShowWithBackAndClose();
        }
        #endregion

        #region Note Type and Template
        private async void NoteTypeAndTemplateClick(object sender, RoutedEventArgs e)
        {            
            if (MainPage.UserPrefs.IsHelpAlreadyShown(HELP_NOTE_TYPE_AND_TEMPLATE))
            {
                InitHelpPopupIfNeeded();
                NoteTypeAndTemplateStart();
            }
            else
            {
                if (MainPage.Collection.Deck.Count() <= 1)
                {
                    await UIHelper.ShowMessageDialog("Please add a deck and a note before viewing this tutorial.");
                    return;
                }

                if (!MakeSureUsedModelWithExistingNote())
                {
                    await UIHelper.ShowMessageDialog("Please add a deck using non-cloze note type and a note before viewing this tutorial.");
                    return;
                }

                HideSplitView();
                AllHelps.Tutorial = AllHelps.TutorialState.NoteType;                
                SetTemplateAndTypeFieldTutVisible();
                NoteTypeAndTemplateFlag.Visibility = Visibility.Collapsed;

                ContentFrame.Navigate(typeof(ModelEditor), MainPage);
            }
        }

        private void NoteTypeAndTemplateStart()
        {
            helpPopup.Title = "Note Type";
            helpPopup.SubtitleVisibility = Visibility.Collapsed;
            helpPopup.Text = NOTE_TYPE_DEFINITION;
            helpPopup.NextEvent = FirstNoteTypeAndTemplateNext;
            helpPopup.ShowWithNext();
        }

        private void FirstNoteTypeAndTemplateNext()
        {
            helpPopup.BackEvent = NoteTypeAndTemplateStart;
            helpPopup.NextEvent = SecondNoteTypeAndTemplateNext;
            helpPopup.Title = "Template";            
            helpPopup.Text = TEMPLATE_DEFINITION + "\nFields must be marked by {{...}} or they are treated as normal texts.";
            helpPopup.ShowWithNextAndBack();
        }

        private void SecondNoteTypeAndTemplateNext()
        {
            helpPopup.BackEvent = FirstNoteTypeAndTemplateNext;
            helpPopup.NextEvent = ThirdNoteTypeAndTemplateNext;
            helpPopup.Title = "Template";
            helpPopup.Text = "A template always have two sides, FRONTSIDE and BACKSIDE. {{FrontSide}} is a special field that is used in BACKSIDE to include all contents of FRONTSIDE.";
            helpPopup.ShowWithNextAndBack();
        }

        private void ThirdNoteTypeAndTemplateNext()
        {
            helpPopup.BackEvent = SecondNoteTypeAndTemplateNext;
            helpPopup.NextEvent = FourthNoteTypeAndTemplateNext;
            helpPopup.Title = "Template";
            helpPopup.Text = "A note type (except cloze) can have more than one template. Each template will generate a card from a note if its FRONTSIDE is not blank.\n Ex: If you have three templates then each note will normally create three cards.";
            helpPopup.ShowWithNextAndBack();
        }

        private void FourthNoteTypeAndTemplateNext()
        {
            helpPopup.BackEvent = ThirdNoteTypeAndTemplateNext;
            helpPopup.NextEvent -= FourthNoteTypeAndTemplateNext;
            helpPopup.Title = "Template";
            helpPopup.Text = "When you change contents of a template, all cards generated from that template will also be changed. Cards are also automatically generated/deleted when you add/remove a template.";
            helpPopup.ShowWithBackAndClose();
        }
        #endregion

        #region Type Field
        private void TemplateWithTypeFieldClick(object sender, RoutedEventArgs e)
        {
            MainPage.UserPrefs.SetHelpShown(HELP_TYPE_FIELD, true);
            TypeFieldFlag.Visibility = Visibility.Collapsed;
            InitHelpPopupIfNeeded();            
            helpPopup.Closed += HelpPopupCloseHandler;            
            StartTemplateWithTypeField();

            templateWithClozeField.Visibility = Visibility.Visible;
        }

        private void StartTemplateWithTypeField()
        {
            helpPopup.SubtitleVisibility = Visibility.Collapsed;
            helpPopup.Image.Visibility = Visibility.Visible;
            helpPopup.Image.Source = GetImageSource("TypeFieldIntro.png");            
            helpPopup.Title = "Type Field";            
            helpPopup.Text = "Type field allows you to type (or write) your answer into your cards. This will help you memorize cards better.";
            helpPopup.NextEvent = FirstTypeFieldNext;
            helpPopup.ShowWithNext();
        }

        private void FirstTypeFieldNext()
        {
            helpPopup.BackEvent = StartTemplateWithTypeField;
            helpPopup.NextEvent = SecondTypeFieldNext;

            helpPopup.Image.Visibility = Visibility.Collapsed;
            helpPopup.Title = "Limitation?";
            helpPopup.Text = "All note types can have type fields. You can only add one type field into FRONTSIDE of a template." + 
                            " In addition, FRONTSIDE must have at least another field so it won't become blank.";
            helpPopup.ShowWithNextAndBack();
        }

        private void SecondTypeFieldNext()
        {
            helpPopup.BackEvent = FirstTypeFieldNext;
            helpPopup.NextEvent = ThirdTypeFieldNext;

            helpPopup.Image.Visibility = Visibility.Visible;
            helpPopup.Image.Source = GetImageSource("AddTypeField.png");
            helpPopup.Title = "Add a Type Field";            
            helpPopup.Text = "";
            helpPopup.ShowWithNextAndBack();
        }

        private void ThirdTypeFieldNext()
        {
            helpPopup.BackEvent = SecondTypeFieldNext;
            helpPopup.NextEvent = FourthTypeFieldNext;

            helpPopup.Image.Visibility = Visibility.Visible;
            helpPopup.Image.Source = GetImageSource("TypeTemplate.png");
            helpPopup.Title = "A Valid Template";
            helpPopup.Text = "FRONTSIDE must have at least another field.";
            helpPopup.ShowWithNextAndBack();
        }

        private void FourthTypeFieldNext()
        {
            helpPopup.BackEvent = ThirdTypeFieldNext;
            helpPopup.NextEvent = FifthTypeFieldNext;

            helpPopup.Image.Visibility = Visibility.Visible;
            helpPopup.Image.Source = GetImageSource("NoteTypeField.png");
            helpPopup.Title = "Add a note";
            helpPopup.Text = "Add a note like normal.";
            helpPopup.ShowWithNextAndBack();
        }

        private void FifthTypeFieldNext()
        {
            helpPopup.BackEvent = FourthTypeFieldNext;
            helpPopup.NextEvent = SixthTypeFieldNext;

            helpPopup.Image.Visibility = Visibility.Visible;
            helpPopup.SubtitleVisibility = Visibility.Collapsed;
            helpPopup.Image.Source = GetImageSource("CardTypeField.png");
            helpPopup.Title = "Answer a Card";
            helpPopup.Text = "Correct answers are highlighted in green while wrong answers are in yellow and gray.";
            helpPopup.ShowWithNextAndBack();
        }

        private void SixthTypeFieldNext()
        {
            helpPopup.BackEvent = FifthTypeFieldNext;
            helpPopup.NextEvent -= SixthTypeFieldNext;

            helpPopup.Image.Visibility = Visibility.Collapsed;
            helpPopup.Title = "Ink to Text";
            helpPopup.SubtitleVisibility = Visibility.Visible;
            helpPopup.SubTitle = "(Handwriting Recognition)";
            helpPopup.Text = "Instead of typing, you can also input your answer by using \"Ink to Text\" feature when viewing card. You can switch back to typing anytime by choosing \"Hide Ink\".";
            helpPopup.ShowWithBackAndClose();
        }
        #endregion

        #region Cloze Field
        private void TemplateWithClozeFieldClick(object sender, RoutedEventArgs e)
        {
            MainPage.UserPrefs.SetHelpShown(HELP_ClOZE_FIELD, true);
            ClozeFlag.Visibility = Visibility.Collapsed;
            InitHelpPopupIfNeeded();
            helpPopup.Closed += HelpPopupCloseHandler;
            StartTemplateWithClozeField();
        }

        private void StartTemplateWithClozeField()
        {
            helpPopup.SubtitleVisibility = Visibility.Collapsed;
            helpPopup.Image.Visibility = Visibility.Collapsed;
            helpPopup.Title = "Cloze Field";
            helpPopup.Text = "A B [...] D E F.\n" 
                             + "A cloze field allows you to create questions with answers hidden by [...].";
            helpPopup.NextEvent = FirstClozeFieldNext;
            helpPopup.ShowWithNext();
        }

        private void FirstClozeFieldNext()
        {
            helpPopup.BackEvent = StartTemplateWithClozeField;
            helpPopup.NextEvent = SecondClozeFieldNext;

            helpPopup.Image.Visibility = Visibility.Collapsed;
            helpPopup.Title = "Limitation?";
            helpPopup.Text = "Only cloze note type can have a cloze field. A cloze note type can only have one template and one cloze field." 
                            + " In addition, you should not use {{FrontSide}} in BACKSIDE.";
            helpPopup.ShowWithNextAndBack();
        }

        private void SecondClozeFieldNext()
        {
            helpPopup.BackEvent = FirstClozeFieldNext;
            helpPopup.NextEvent = ThirdClozeFieldNext;

            helpPopup.Image.Visibility = Visibility.Visible;
            helpPopup.Image.Source = GetImageSource("ChangeToCloze.png");
            helpPopup.Title = "Change to Cloze Note Type";
            helpPopup.Text = "A cloze note type can only have one template.";
            helpPopup.ShowWithNextAndBack();
        }

        private void ThirdClozeFieldNext()
        {
            helpPopup.BackEvent = SecondClozeFieldNext;
            helpPopup.NextEvent = FourthClozeFieldNext;

            helpPopup.Image.Visibility = Visibility.Visible;
            helpPopup.Image.Source = GetImageSource("AddClozeField.png");
            helpPopup.Title = "Add a Cloze Field";
            helpPopup.Text = "You should also delete \"{{FrontSide}}\" from BACKSIDE.";
            helpPopup.ShowWithNextAndBack();
        }

        private void FourthClozeFieldNext()
        {
            helpPopup.BackEvent = ThirdClozeFieldNext;
            helpPopup.NextEvent = FifthClozeFieldNext;

            helpPopup.Image.Visibility = Visibility.Visible;
            helpPopup.Image.Source = GetImageSource("ValidClozeTemplate.png");
            helpPopup.Title = "A Valid Cloze Template";
            helpPopup.Text = "";
            helpPopup.ShowWithNextAndBack();
        }

        private void FifthClozeFieldNext()
        {
            helpPopup.BackEvent = FourthClozeFieldNext;
            helpPopup.NextEvent = SixthClozeFieldNext;

            helpPopup.Image.Visibility = Visibility.Visible;
            helpPopup.Image.Source = GetImageSource("AddClozeNote.png");
            helpPopup.Title = "Add a Cloze Note";
            helpPopup.Text = "To add clozes, press on the \"[...]\" button (or Ctrl + Shift + C), then input your cloze contents. A note can have multiple clozes.";
            helpPopup.ShowWithNextAndBack();
        }

        private void SixthClozeFieldNext()
        {
            helpPopup.BackEvent = FifthClozeFieldNext;
            helpPopup.NextEvent -= SixthClozeFieldNext;

            helpPopup.Image.Visibility = Visibility.Collapsed;            
            helpPopup.Title = "Type Field vs Cloze Field";
            helpPopup.Text = "Type fields are simpler to use and better for long-term retention rate. You can also create a type-cloze field if you want. Ex: {{type:cloze:Front}}";
            helpPopup.ShowWithBackAndClose();
        }
        #endregion

        #region Deck Options
        public void ShowDeckOptionHelp(object sender, RoutedEventArgs e)
        {
            MainPage.UserPrefs.SetHelpShown(HELP_DECK_OPTION, true);
            DeckOption.Visibility = Visibility.Visible;
            DeckOptionFlag.Visibility = Visibility.Collapsed;
            InitHelpPopupIfNeeded();
            helpPopup.Closed += HelpPopupCloseHandler;
            StartDeckOption();
        }

        private void StartDeckOption()
        {
            helpPopup.SubtitleVisibility = Visibility.Collapsed;            
            helpPopup.Title = "Deck Options";
            helpPopup.Text = "To avoid making you set or change the same settings for each deck over and over again, in Anki we use option presets (or also called option groups.)";
            helpPopup.NextEvent = FirstDeckOptionNext;
            helpPopup.ShowWithNext();
        }

        private void FirstDeckOptionNext()
        {
            helpPopup.BackEvent = StartDeckOption;
            helpPopup.NextEvent = SecondDeckOptionNext;            
            helpPopup.Title = "Deck Options";
            helpPopup.Text = "By default, you have 5 option presets. They are uneditable, however, you can create new presets from them "
                              + "by pressing on the \"Add\" icon.\n"
                              + "Option presets created by you can be edited or removed anytime.";            
            helpPopup.ShowWithNextAndBack();
        }

        private void SecondDeckOptionNext()
        {
            helpPopup.BackEvent = FirstDeckOptionNext;
            helpPopup.NextEvent -= SecondDeckOptionNext;
            helpPopup.Title = "Deck Options";
            helpPopup.Text = "If you are new to Anki, then \"Simple\" mode should cover all your needs. If you wish to use \"Expert\" mode, "
                              + "please read the manual.";                              
            helpPopup.ShowWithBackAndClose();
        }
        #endregion

        #region Data Syncing
        private void DataSyncingClick(object sender, RoutedEventArgs e)
        {
            MainPage.UserPrefs.SetHelpShown(HELP_SYNC, true);            
            DataSyncingFlag.Visibility = Visibility.Collapsed;
            InitHelpPopupIfNeeded();            
            StartDataSyncing();
        }

        private void StartDataSyncing()
        {
            helpPopup.SubtitleVisibility = Visibility.Collapsed;
            helpPopup.Title = "How to Sync?";
            helpPopup.Text = "To sync your data, simply press on the \"Sync\" icon in the deck select page.\n"
                             + "To avoid losing your progress, you should ALWAYS sync your devices right after opening the app and right before closing it.";
            helpPopup.NextEvent = FirstDataSyncNext;
            helpPopup.ShowWithNext();
        }

        private void FirstDataSyncNext()
        {
            helpPopup.BackEvent = StartDataSyncing;
            helpPopup.NextEvent = SecondDataSyncNext;

            helpPopup.Title = "(Automatically) Media Syncing";
            helpPopup.Text = "To enable syncing media files on a device, please go to \"Settings\\Sync\" and " 
                             + "check \"Also sync media files\".\n"  
                             + "Only files changed from the last sync time will be uploaded or downloaded.";

            helpPopup.ShowWithNextAndBack();
        }

        private void SecondDataSyncNext()
        {
            helpPopup.BackEvent = FirstDataSyncNext;
            helpPopup.NextEvent = ThirdDataSyncNext;

            helpPopup.Title = "(Manually) Media Syncing";
            helpPopup.Text = "You can also add media files manually by using \"Back up Media\" and \"Insert Media Files\" features.\n"
                             + "We recommend you to do this when you need to download a large number of media files to your devices. Ex: first time syncing your phone.";
            helpPopup.ShowWithNextAndBack();
        }

        private void ThirdDataSyncNext()
        {
            helpPopup.BackEvent = SecondDataSyncNext;
            helpPopup.NextEvent = FourhtDataSyncNext;


            helpPopup.Title = "Example: Manually Syncing";
            helpPopup.Text = "Assume you need to download 2000 media files uploaded from your laptop to your phone:\n"
                             + "1. On your laptop, use \"Back up Media Files\", then copy the zip file(s) to your phone.\n" 
                             + "2. On your phone, DISABLE media syncing, then sync your data.\n"
                             + "3. Use \"Insert Media Files\" to add media. ENABLE media syncing and sync your phone again.\n";
            helpPopup.ShowWithNextAndBack();
        }

        private void FourhtDataSyncNext()
        {
            helpPopup.BackEvent = ThirdDataSyncNext;

            helpPopup.Title = "Example: Manually Syncing";
            helpPopup.Text = "From now on, your media files will continue to be synced automatically without having to download 2000 files.\n" 
                            + "Note that you can also download these files through normal syncing. However, it will take a long time to complete and if any connection errors happen then you will have to download all files again.";
            helpPopup.ShowWithBackAndClose();
        }
        #endregion

        #region Sub Deck
        public void ShowSubDeckHelp()
        {
            MainPage.UserPrefs.SetHelpShown(HELP_SUBDECK, true);
            InitHelpPopupIfNeeded();
            StartSubDeckHelp();
        }

        private void StartSubDeckHelp()
        {
            helpPopup.SubtitleVisibility = Visibility.Collapsed;
            helpPopup.Title = "Create a SubDeck";
            helpPopup.Text = "To make a deck become a subdeck, drag and drop it onto another deck. That deck will become its parent.\n"
                             + "To remove a deck from subdecks, drag and drop it onto its parent again.";
            helpPopup.NextEvent = FirstSubDeckNext;
            helpPopup.ShowWithNext();
        }

        private void FirstSubDeckNext()
        {
            helpPopup.BackEvent = StartSubDeckHelp;
            helpPopup.NextEvent = SecondSubDeckNext;

            helpPopup.Title = "Card Display Order";
            helpPopup.Text = "When learning/reviewing, cards from the parent deck are shown first. "
                              +"If the maximum number of new/review cards has not been reached, cards from subdecks will be shown based on alphabetical order. Ex: cards from subdeck \"A\" are shown before cards from subdeck \"B\".";

            helpPopup.ShowWithNextAndBack();
        }

        private void SecondSubDeckNext()
        {
            helpPopup.BackEvent = FirstSubDeckNext;            

            helpPopup.Title = "WARNING";
            helpPopup.Text = "Delete a parent deck will also remove all of its subdecks." +
                             " Even though you can restore your decks from backups in \"Settings\", mediafiles won't be restored automatically. "
                             + " To restore them, please use \"Insert Media Files\" and choose a zip file that contains the deleted media files.";

            helpPopup.ShowWithBackAndClose();
        }
        #endregion

        private void InitHelpPopupIfNeeded()
        {
            if(helpPopup == null)
            {
                helpPopup = new HelpPopup();
                UIHelper.AddToGridInFull(MainPage.MainGrid, helpPopup);
                helpPopup.CloseXVisibility = Visibility.Visible;      
            }
            helpPopup.ChangeReadMode(isNightMode);
        }

        private bool MakeSureUsedModelWithExistingNote()
        {            
            int cardCount;
            foreach (var id in collection.Deck.AllIds())
            {
                if (id == Constant.DEFAULTDECK_ID)
                    continue;

                cardCount = collection.CardCount(id);
                if (cardCount > 0)
                {
                    collection.Deck.Select(id, false);                    
                    var model = collection.Models.GetCurrent();
                    if (JsonHelper.GetNameNumber(model, "type") == 0)
                    {
                        collection.Models.SetCurrent(model);
                        return true;
                    }
                }
            }
            
            return false;
        }

        private void HideSplitView()
        {
            SplitView.IsPaneOpen = false;
            SplitView.IsHitTestVisible = false;
        }

        private void ReNavigateToDeckSelectPage()
        {
            ContentFrame.Navigate(typeof(DeckSelectPage), MainPage);
            ContentFrame.BackStack.RemoveAt(0);
        }

        private async void UserManualClick(object sender, RoutedEventArgs e)
        {
            Uri uri = new Uri("http://ankisrs.net/docs/manual.html");
            await Windows.System.Launcher.LaunchUriAsync(uri);
        }

        private BitmapImage GetImageSource(string imageName)
        {
            return new BitmapImage(new Uri("ms-appx:///Assets/" + imageName));
        }

        public void ChangeReadMode(bool isNightMode)
        {
            this.isNightMode = isNightMode;
            if(isNightMode)           
                userControl.Foreground = new SolidColorBrush(Windows.UI.Colors.White);            
            else            
                userControl.Foreground = new SolidColorBrush(Windows.UI.Colors.Black);                         

            if (helpPopup != null)            
                helpPopup.ChangeReadMode(isNightMode);

            if (creditPopup != null)
                creditPopup.ChangeReadMode(isNightMode);
        }

        private void HelpPopupCloseHandler()
        {
            try
            {
                helpPopup.Closed -= HelpPopupCloseHandler;
                helpPopup.Image.Visibility = Visibility.Collapsed;
                helpPopup.Image.Source = null;
                HelpClose?.Invoke();
            }
            catch //Make sure no crash occur
            { }
        }

        private void CreditClick(object sender, RoutedEventArgs e)
        {
            if (creditPopup == null)
            {
                creditPopup = new CreditsPopup(MainPage.MainGrid);
                UIHelper.AddToGridInFull(MainPage.MainGrid, creditPopup);
            }
            creditPopup.ChangeReadMode(isNightMode);
            creditPopup.Show();
        }        
    }
}
