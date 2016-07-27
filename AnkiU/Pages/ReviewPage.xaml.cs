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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;
using AnkiU.Views;
using System.Text;
using System.Text.RegularExpressions;
using Windows.UI.Input.Inking;
using Windows.UI.Popups;
using AnkiU.Interfaces;
using AnkiU.Anki;
using AnkiU.UIUtilities;
using AnkiU.UserControls;
using Windows.UI.Xaml.Media;
using Windows.System;
using AnkiU.ViewModels;
using Windows.UI.Xaml.Input;
using Windows.UI.Input.Inking.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml.Media.Animation;

namespace AnkiU.Pages
{
    public struct TypeField
    {
        public string CorrectAnswer { get; set; }
        public string Font { get; set; }
        public int Size { get; set; }
    }

    public sealed partial class ReviewPage : INightReadMode
    {
        private const int MAX_RESCHEDULE = 1000;
        private const string NUM_CARDS_STR = "{0} + {1} + {2}";
        public static readonly Regex TypeAnswerRegex = new Regex(@"\[\[type:(.+?)\]\]", RegexOptions.Compiled);
        public const string TypeAnswerContentPattern = @"\{{\{{c{0}::(.+?)\}}\}}";

        private const int CANVAS_INDEX_LOWEST = -1;
        private const int CANVAS_INDEX_HIGHEST = 0;

        private TypeField type = new TypeField() { CorrectAnswer = null, Font = null, Size = 0 };

        private static long? editCardId;
        private static List<long> undoCardQueue = new List<long>();
        private bool isCardFromQueue = false;

        private MainPage mainPage;
        private Collection collection;
        private Card currentCard;
        private string question;
        private string answer;
        private string cardClass;
        private bool isAutoPlayEnable;
        private long currentDeckId;
        private bool isCanNavigateFrom;
        private bool isCustomDueTimeFlyoutOpen = false;
        private bool isCanGoBack = true;
        private bool isNightMode = false;
        private bool isInkSizeChanged = false;

        private HelpPopup helpPopup = null;
        private int numerOfAnswerPress = 0;
        public event NoticeRoutedHandler AnswerButtonsPressEvent;
        public event NoticeRoutedHandler DisplayAnswerEvent;

        private List<CardButtonView> activeAnswerButtons;
        
        private InkCanvas Ink
        {
            get
            {
                //We only init inkCanvas if needed
                if (inkCanvas == null)
                {
                    this.FindName("inkCanvas");
                    inkCanvas.InkPresenter.InputDeviceTypes = CoreInputDeviceTypes.Pen |
                                                              CoreInputDeviceTypes.Touch |
                                                              CoreInputDeviceTypes.Mouse;
                }
                return inkCanvas;
            }
        }

        private IUserInputString userInputGetter;

        private InkToTextRecognizer inkToTextRecognizer = null;
        private InkToTextRecognizer InkToTextRecognizer
        {
            get
            {
                if (inkToTextRecognizer == null)
                    inkToTextRecognizer = new InkToTextRecognizer(mainPage, Ink, showAnswerButton);
                return inkToTextRecognizer;
            }
        }        

        public ReviewPage()
        {
            this.InitializeComponent();
            InitButtonList();
            NavigationCacheMode = NavigationCacheMode.Disabled;
            isCanNavigateFrom = true;
            userInputGetter = cardView;
            //Hook this CardViewLoadedEvent as soon as we init page
            //To avoid missing it and lead to a white page
            cardView.CardHtmlLoadedEvent += CardViewLoadedHandler;            
        }

        public static void ClearReviewUndo()
        {
            undoCardQueue.Clear();            
        }

        private void HookAllEventsExceptCardViewLoaded()
        {
            CoreWindow.GetForCurrentThread().KeyDown += CoreWindowKeyDownHandler;
            InputPane.GetForCurrentView().Showing += TouchKeyboardShowing;
            InputPane.GetForCurrentView().Hiding += TouchKeyboardHidingHandler;

            collection.Sched.NotifyLeechEvent += ScheduleNotifyLeechEventHandler;                     
            cardView.KeyDownMappingEvent += KeyDownHandler;            
            cardView.NavigateToWebsiteStartEvent += NavigateToWebsiteStartEventHandler;

            mainPage.EditButton.Click += EditButtonClickHandler;

            mainPage.ReadModeButton.Click += ReadModeButtonClickHandler;

            mainPage.InkOnOffButton.Click += InkOnOffButtonClickHandler;
            mainPage.InkClearButton.Click += InkClearButtonClickHandler;
            mainPage.InkEraserToggleButtonClick += InkEraserButtonClickHandler;
            mainPage.InkHideToggleButtonClick += InkHideToggleButtonClickHandler;

            mainPage.InkToTextEnableToggled += InkToTextToggleHandler;
            mainPage.ChooseTextManually.Checked += ChooseTextManuallyCheckedHandler;
            mainPage.ChooseTextAutomatically.Checked += ChooseTextAutomaticallyCheckedHandler;

            mainPage.UndoButton.Click += UndoButtonClickHandler;            
        }

        private bool ScheduleNotifyLeechEventHandler(string message, Card card)
        {
            bool isNotDyn = IsCardNotDynamicDeck(card);
            var deck = collection.Deck.Get(currentDeckId);
            var isDefault = deck.GetNamedNumber("conf") == (int)ConfigPresets.Default;

            if (!MainPage.UserPrefs.IsShowLeechActionOnce && isNotDyn && isDefault)
            {                
                isCanGoBack = false;
                var task = mainPage.CurrentDispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                {
                    await LeechCardExplainAndGetDefaultAction(card, deck);
                });
                return false;
            }
            else
            {
                if (collection.Sched.LapseConf(currentCard).GetNamedNumber("leechAction") == 0)
                    popup.ShowAsync(mainPage.CurrentDispatcher, "Leech threshold reached. Card was suspended", 2000);
                else
                    popup.ShowAsync(mainPage.CurrentDispatcher, "Leech threshold reached.", 2000);

                return true;
            }
        }

        private async Task LeechCardExplainAndGetDefaultAction(Card card, JsonObject deck)
        {
            var isSuspend = await UIHelper.AskUserConfirmation(UIConst.LEECH_CARD_DETECTED, "Leech card detected!");
            if (!isSuspend)
            {
                await UIHelper.ShowMessageDialog(UIConst.NOT_SUSPEND_ACTION_CHOOSE);
                deck["conf"] = JsonValue.CreateNumberValue((int)ConfigPresets.TagOnLeech);
                collection.Deck.Save(deck);
                mainPage.SaveAndStartNewDatabaseSessionAsync();
            }
            else
            {
                collection.Sched.SuspendCards(card.Id);
                collection.Sched.Reset();
                updateNumberOfCardFirstLeech();
            }

            MainPage.UserPrefs.IsShowLeechActionOnce = true;
            mainPage.UpdateUserPreference();
            isCanGoBack = true;
            if (currentCard == null)
                FrameGoBack();
            if (currentCard.Id == card.Id && isSuspend)
                await GotoNextQuestionWithoutAnswering();
        }

        private void FrameGoBack()
        {            
            Frame.GoBack();
        }

        private void updateNumberOfCardFirstLeech()
        {
            if (CollectionOptionViewModel.IsDueCountEnable(collection.Conf))
            {
                var number = showAnswerButton.Header.Split(new string[] { " + " }, StringSplitOptions.RemoveEmptyEntries);
                number[1] = (int.Parse(number[1]) - 1).ToString();
                showAnswerButton.Header = String.Format(NUM_CARDS_STR, number[0], number[1], number[2]);
            }
        }

        private bool IsCardNotDynamicDeck(Card card)
        {
            return card.OriginalDeckId == 0;
        }

        private void UnhookAllEvents()
        {
            CoreWindow.GetForCurrentThread().KeyDown -= CoreWindowKeyDownHandler;
            InputPane.GetForCurrentView().Showing -= TouchKeyboardShowing;
            InputPane.GetForCurrentView().Hiding -= TouchKeyboardHidingHandler;

            collection.Sched.NotifyLeechEvent -= ScheduleNotifyLeechEventHandler;            
            cardView.KeyDownMappingEvent -= KeyDownHandler;
            cardView.CardHtmlLoadedEvent -= CardViewLoadedHandler;
            cardView.NavigateToWebsiteStartEvent -= NavigateToWebsiteStartEventHandler;

            mainPage.EditButton.Click -= EditButtonClickHandler;

            mainPage.ReadModeButton.Click -= ReadModeButtonClickHandler;

            mainPage.InkOnOffButton.Click -= InkOnOffButtonClickHandler;
            mainPage.InkClearButton.Click -= InkClearButtonClickHandler;
            mainPage.InkEraserToggleButtonClick -= InkEraserButtonClickHandler;
            mainPage.InkHideToggleButtonClick -= InkHideToggleButtonClickHandler;

            mainPage.InkToTextEnableToggled -= InkToTextToggleHandler;
            mainPage.ChooseTextManually.Checked -= ChooseTextManuallyCheckedHandler;
            mainPage.ChooseTextAutomatically.Checked -= ChooseTextAutomaticallyCheckedHandler;

            mainPage.UndoButton.Click -= UndoButtonClickHandler;            
        }

        private void InitButtonList()
        {
            activeAnswerButtons = new List<CardButtonView>();

            showAnswerButton.Click += DisplayAnswerEventHandler;
            againButton.Click += AgainButtonClickHandler;
            hardButton.Click += HardButtonClickHandler;
            goodButton.Click += GoodButtonClickHandler;
            easyButton.Click += EasyButtonClickHandler;
        }

        private void PageSizeChanged(object sender, SizeChangedEventArgs e)
        {
            UpdateAnswerButtonWidth();
        }
        private void UpdateAnswerButtonWidth()
        {
            double width = mainGrid.ActualWidth / activeAnswerButtons.Count;
            foreach (var button in activeAnswerButtons)
            {
                button.Width = width;
            }
        }        

        protected async override void OnNavigatedTo(NavigationEventArgs e)
        {
            GC.Collect();
            base.OnNavigatedTo(e);

            mainPage = e.Parameter as MainPage;
            if (this.mainPage == null)
                throw new Exception("Wrong input parameter!");

            collection = mainPage.Collection;
            currentDeckId = collection.Deck.Selected();

            //WANRING: Run in transaction to ensure performance
            //remember to call commit ro rollback to database
            collection.Database.SaveTransactionPoint();

            EnterTutorialModeIfNeeded();
            await ShowAllButtonOfThisPage();            
            HookAllEventsExceptCardViewLoaded();

            if (mainPage.IsInkOn())
                SwitchToInkCanvasAndInkInput();
        }

        private async Task ShowAllButtonOfThisPage()
        {
            ShowOrHideButtonsHeader();
            mainPage.EnableChangingReadMode(this, cardView);
            ChangeBackgroundColor();
            mainPage.ZoomButtonsSeparator.Visibility = Visibility.Visible;
            mainPage.HookZooming(cardView);
            mainPage.EditButton.Visibility = Visibility.Visible;
            mainPage.UndoButton.Visibility = Visibility.Visible;
            if (!collection.UndoAvailable())
                mainPage.UndoButton.IsEnabled = false;
            await UpdateInkButton();
        }

        private void ShowOrHideButtonsHeader()
        {
            if (!CollectionOptionViewModel.IsDueCountEnable(collection.Conf))
                showAnswerButton.HeaderVisibility = Visibility.Collapsed;
            if (!CollectionOptionViewModel.IsShowNextReviewTimeEnable(collection.Conf))
            {
                againButton.HeaderVisibility = Visibility.Collapsed;
                hardButton.HeaderVisibility = Visibility.Collapsed;
                goodButton.HeaderVisibility = Visibility.Collapsed;
                easyButton.HeaderVisibility = Visibility.Collapsed;
            }
        }

        private async Task UpdateInkButton()
        {
            if (!mainPage.IsInkOn())
                mainPage.ShowOnlyInkOnOffButton();            
            else
            {
                mainPage.ShowAllInkButtons();
                if (MainPage.DeckInkPrefs.IsEnableInkToText(currentDeckId))
                {
                    mainPage.InkToTextEnable.IsOn = true;
                    UpdateFlyoutButton(true);
                    await mainPage.InitInkRecognizeIfNeeded();
                }
                else
                {
                    mainPage.InkToTextEnable.IsOn = false;
                    UpdateFlyoutButton(false);
                }
            }
        }

        private void ChooseTextAutomaticallyCheckedHandler(object sender, RoutedEventArgs e)
        {
            MainPage.DeckInkPrefs.SetIsAutoInkToTextEnable(currentDeckId, true);
        }
        private void ChooseTextManuallyCheckedHandler(object sender, RoutedEventArgs e)
        {
            MainPage.DeckInkPrefs.SetIsAutoInkToTextEnable(currentDeckId, false);
        }    

        private void ReadModeButtonClickHandler(object sender, RoutedEventArgs e)
        {            
            ChangeInkColorIfNeeded();
        }
        private void ChangeInkColorIfNeeded()
        {
            if (mainPage.IsInkOn())
            {
                if (MainPage.UserPrefs.IsReadNightMode)
                    ChangeInkCorlor(UIHelper.DefaultInkColorNight);
                else
                    ChangeInkCorlor(UIHelper.DefaultInkColorDay);
            }
        }

        private async void InkOnOffButtonClickHandler(object sender, RoutedEventArgs e)
        {
            if (mainPage.IsInkOnState())
            {
                MainPage.DeckInkPrefs.RemoveDeckInkPref(currentDeckId);
                SwitchBackToCardView();
            }
            else
            {
                MainPage.DeckInkPrefs.AddNewDeckPref(currentDeckId);
                SwitchToInkCanvasAndInkInput();
                ClearInkCanvas();
                ChangeInkToPen();
            }
            await UpdateInkButton();
        }
        
        private void SwitchToInkCanvasAndInkInput()
        {
            Ink.Visibility = Visibility.Visible;
            Canvas.SetZIndex(cardView, CANVAS_INDEX_LOWEST);
            Canvas.SetZIndex(Ink, CANVAS_INDEX_HIGHEST);
            ChangeInkSizeIfNeeded();

            if (MainPage.UserPrefs.IsReadNightMode)
                ChangeInkCorlor(UIHelper.DefaultInkColorNight);
            else
                ChangeInkCorlor(UIHelper.DefaultInkColorDay);

            if (MainPage.DeckInkPrefs.IsEnableInkToText(currentDeckId))
                userInputGetter = InkToTextRecognizer;
        }

        private void ChangeInkSizeIfNeeded()
        {
            if (!isInkSizeChanged)
            {
                ChangeInkSize(UIHelper.DEFAULT_INK_SIZE);
                isInkSizeChanged = true;
            }
        }

        private void SwitchBackToCardView()
        {
            Ink.Visibility = Visibility.Collapsed;
            Canvas.SetZIndex(cardView, CANVAS_INDEX_HIGHEST);
            Canvas.SetZIndex(Ink, CANVAS_INDEX_LOWEST);
            userInputGetter = cardView;            
        }

        private void ChangeInkCorlor(Windows.UI.Color color)
        {
            InkDrawingAttributes drawingAttributes = Ink.InkPresenter.CopyDefaultDrawingAttributes();
            drawingAttributes.Color = color;
            Ink.InkPresenter.UpdateDefaultDrawingAttributes(drawingAttributes);
        }
        private void ChangeInkSize(double size)
        {
            InkDrawingAttributes drawingAttributes = Ink.InkPresenter.CopyDefaultDrawingAttributes();
            drawingAttributes.Size = new Windows.Foundation.Size(size, size);
            Ink.InkPresenter.UpdateDefaultDrawingAttributes(drawingAttributes);
        }
        private void InkClearButtonClickHandler(object sender, RoutedEventArgs e)
        {
            ClearInkCanvas();
        }
        private void ClearInkCanvas()
        {
            Ink.InkPresenter.StrokeContainer.Clear();
        }
        private void InkHideToggleButtonClickHandler(object sender, RoutedEventArgs e)
        {
            if(mainPage.IsInkHideState())
                SwitchBackToCardView();
            else
                SwitchToInkCanvasAndInkInput();
            
        }
        private void InkToTextToggleHandler()
        {
            var enable = mainPage.InkToTextEnable.IsOn;
            MainPage.DeckInkPrefs.SetIsEnableInkToText(currentDeckId, enable);
            UpdateFlyoutButton(enable);
            if (enable)                            
                SwitchToInkCanvasAndInkInput();            
            else
                userInputGetter = cardView;
        }
        private void UpdateFlyoutButton(bool enable)
        {
            if (enable)
            {
                mainPage.ShowInkToTextFlyoutToggleOnContent();
                if (MainPage.DeckInkPrefs.IsAutoInkToTextEnable(currentDeckId))
                    mainPage.ChooseTextAutomatically.IsChecked = true;
                else
                    mainPage.ChooseTextManually.IsChecked = true;
            }
            else
                mainPage.HideInkToTextFlyoutToggleOnContent();
        }

        private async void UndoButtonClickHandler(object sender, RoutedEventArgs e)
        {
            if (!collection.UndoAvailable())
                return;

            long cardId = collection.Undo();
            currentCard = collection.GetCard(cardId);
            undoCardQueue.Add(currentCard.Id);            
            collection.Reset();
            await ShowNextQuestion();

            if (!collection.UndoAvailable())
                mainPage.UndoButton.IsEnabled = false;
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            mainPage.ChangeCusorToArrow();
            collection.SaveAndCommit();
            if (!isCanNavigateFrom)
                e.Cancel = true;

            HideAllButtonOfThisPage();
            UnhookAllEvents();                        
            cardView.ClearWebViewControl();
            if (inkToTextRecognizer != null)
                inkToTextRecognizer.Close();

            base.OnNavigatingFrom(e);
        }
        private void HideAllButtonOfThisPage()
        {
            mainPage.DisableChangingReadMode();
            mainPage.UnhookZooming();
            mainPage.EditButton.Visibility = Visibility.Collapsed;
            mainPage.UndoButton.Visibility = Visibility.Collapsed;
            mainPage.UndoButton.IsEnabled = true;          
            mainPage.HideAllInkButtonsExceptOnOffIfNeeded();
            mainPage.InkOnOffButton.Visibility = Visibility.Collapsed;
            mainPage.ZoomButtonsSeparator.Visibility = Visibility.Collapsed;
        }    

        private async void CardViewLoadedHandler()
        {
            cardView.ZoomLevel = MainPage.UserPrefs.ZoomLevel;
            await cardView.ChangeZoomLevel(MainPage.UserPrefs.ZoomLevel);

            PopNextCard();
            await ChangeHtmlheader();
            IsAutoPlay();
            await GetContentAndDisplayQuestion();
        }

        private async Task ChangeHtmlheader()
        {
            await ChangeDeckMediaFolder();
            await ChangeCardStyle();
        }

        private async Task ChangeDeckMediaFolder()
        {
            long deckMediaId;
            if (collection.Deck.IsDyn(currentDeckId))
                deckMediaId = currentCard.OriginalDeckId;
            else
                deckMediaId = currentDeckId;
            string deckMediaFolder = "/" + collection.Media.MediaFolder.Name + "/" + deckMediaId + "/";
            await cardView.ChangeDeckMediaFolder(deckMediaFolder);
        }
        private void PopNextCard()
        {
            if(editCardId != null)
            {
                currentCard = collection.GetCard((long)editCardId);
                currentCard.StartTimer();
                if (undoCardQueue.Contains((long)editCardId))
                    isCardFromQueue = true;

                editCardId = null;                          
            }
            else if (undoCardQueue.Count != 0)
            {
                long id = undoCardQueue.Last();
                currentCard = collection.GetCard(id);
                currentCard.StartTimer();
                isCardFromQueue = true;
            }
            else
            {
                if (isCardFromQueue)
                {
                    //The undone/edited cards may be sitting in the regular queue
                    //need to reset
                    collection.Reset();
                    isCardFromQueue = false;
                }
                currentCard = collection.Sched.PopCard();
            }
        }
        private async Task ChangeCardStyle()
        {
            string cardStyle = currentCard.CssWithoutStyleTag();
            await cardView.ChangeCardStyle(cardStyle);
        }
        private void IsAutoPlay()
        {
            if (currentCard.OriginalDeckId > 0)
            {
                isAutoPlayEnable = collection.Deck.ConfForDeckId(currentCard.OriginalDeckId)["autoplay"].GetBoolean();
                return;
            }
            JsonObject json = collection.Deck.ConfForDeckId(currentCard.DeckId);
            isAutoPlayEnable = json.GetNamedBoolean("autoplay");
        }
        private async Task GetContentAndDisplayQuestion()
        {
            GetQuestionAndAnswer();
            await DisplayQuestionView();
        }
        private void GetQuestionAndAnswer()
        {
            if (currentCard.IsEmpty())
            {
                question = "The front of this card is empty. Please edit it.";
            }
            else
            {
                var questionAndAnswer = currentCard.GetQuestionAndAnswer();
                question = Sound.ExpandSounds(questionAndAnswer["q"]);
                answer = Sound.ExpandSounds(questionAndAnswer["a"]);
            }
        }
        private async Task DisplayQuestionView()
        {
            question = MungeQuestion(question);
            cardClass = $"card card{currentCard.Ord + 1}";

            await cardView.ChangeCardContent(question, cardClass);
            DisplayShowAnswerButton();

            mainPage.SaveAndStartNewDatabaseSessionAsync();

            PlayMediaIfNeeded();
        }

        private string MungeQuestion(string questionFromCard)
        {
            return TypeAnsQuestionFilter(LaTeX.MungeQA(questionFromCard, collection), currentCard, ref type);
        }
        /// <summary>
        /// Filter answer from type field of a question if has.
        /// </summary>
        /// <param name="question"></param>
        /// <param name="currentCard"></param>
        /// <param name="type"></param>
        /// <returns>Return the filter question and a struct TypeField</returns>
        public static string TypeAnsQuestionFilter(string question, Card currentCard, ref TypeField type)
        {
            type.CorrectAnswer = null;
            int clozeIdx = 0;

            var match = TypeAnswerRegex.Match(question);
            if (!match.Success)
                return question;
            string field = match.Groups[1].ToString();
            //If it's a cloze, extract data
            if (field.StartsWith("cloze:"))
            {
                //get field and cloze position
                clozeIdx = currentCard.Ord + 1;
                field = field.Split(':')[1];
            }

            //Loop through fields for a match
            foreach (var f in currentCard.GetModel().GetNamedArray("flds"))
            {
                string name = f.GetObject().GetNamedString("name");
                if (name == field)
                {
                    type.CorrectAnswer = currentCard.LoadNote().GetItem(name);
                    if (clozeIdx != 0)
                    {
                        //Narrow to cloze
                        type.CorrectAnswer = ContentForCloze(type.CorrectAnswer, clozeIdx);
                    }
                    type.Font = f.GetObject().GetNamedString("font");
                    type.Size = (int)f.GetObject().GetNamedNumber("size");
                    break;
                }
            }
            if (type.CorrectAnswer == null)
            {
                string warn;
                if (clozeIdx != 0)
                    warn = "Please edit this card.";
                else
                    warn = String.Format("Type answer: unknown field {0}", field);
                return TypeAnswerRegex.Replace(question, warn);
            }
            else if (type.CorrectAnswer.Equals(""))
            {
                //Empty field, remove type answer pattern
                return TypeAnswerRegex.Replace(question, "");
            }

            string htmlTag = @"<center>
                                <input type=text id=typeAns placeholder=""Answer""
                                onclick=""OnTextBoxClick()"" 
                                onfocusout=""OnTextBoxFocusOut()"" 
                                onkeydown=""TypeAnsKeyDown(event)""
                                style = ""font-family: '{0}'; font-size: {1}px;"">
                                </center>";
            string html = String.Format(htmlTag, type.Font, type.Size);
            return TypeAnswerRegex.Replace(question, html);
        }
        private static string ContentForCloze(string typeCorrect, int clozeIdx)
        {
            string pattern = String.Format(TypeAnswerContentPattern, clozeIdx);
            var matches = Regex.Matches(typeCorrect, pattern);
            if (matches.Count == 0)
                return null;

            //We use dictionary to ensure uniqueness instead of hash set 
            //this is done for performance purpose since we don't need advance features of C# HashSet
            Dictionary<string, bool> matchesContent = new Dictionary<string, bool>();
            string groupOne;
            foreach (Match match in matches)
            {
                groupOne = match.Groups[1].ToString();
                groupOne = StripHint(groupOne);
                matchesContent[groupOne] = false;
            }
            string[] contentArray = matchesContent.Keys.ToArray();

            if (contentArray.Length == 1)
                return contentArray[0];

            StringBuilder resultBuilder = new StringBuilder();
            resultBuilder.Append(contentArray[0]);
            for (int i = 1; i < contentArray.Length; i++)
            {
                resultBuilder.Append(", ");
                resultBuilder.Append(contentArray[i]);
            }
            return resultBuilder.ToString();
        }
        private static string StripHint(string txt)
        {
            if (txt.Contains("::"))
                return txt.Split(new string[] { "::" }, StringSplitOptions.None)[0];
            return txt;
        }
        private void DisplayShowAnswerButton()
        {
            DisplayNumberOfAllCardTypes();
            DisplayOnlyShowAnswerButton();
        }
        private void DisplayNumberOfAllCardTypes()
        {
            CardTypeCounts typeCounts;
            //If it's come from the undo queue, don't count it separately
            if (isCardFromQueue)
                typeCounts = collection.Sched.AllCardTypeCounts();
            else
                typeCounts = collection.Sched.AllCardTypeCounts(currentCard);
            string text = String.Format(NUM_CARDS_STR, typeCounts.New, typeCounts.Learn, typeCounts.Review);        
            if(CollectionOptionViewModel.IsDueCountEnable(collection.Conf))    
                showAnswerButton.Header = text;
            showAnswerButton.Body = "Show Answer";
        }
        private void DisplayOnlyShowAnswerButton()
        {
            HideAnswerButtons();
            showAnswerButton.Visibility = Visibility.Visible;
        }
        private void HideAnswerButtons()
        {
            foreach (var button in activeAnswerButtons)
            {
                button.Visibility = Visibility.Collapsed;
            }
        }
        private void PlayMediaIfNeeded()
        {
            var task = Task.Run(async () =>
            {
                if (isAutoPlayEnable)
                {
                    await mainPage.CurrentDispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
                    {
                        await cardView.PlayAllMedia();
                    });
                }
            });
        }

        private bool isInProcessing = false;
        private void CoreWindowKeyDownHandler(CoreWindow sender, KeyEventArgs e)
        {
            KeyDownHandler(e.VirtualKey);
        }
        private async void KeyDownHandler(Windows.System.VirtualKey e)
        {
            if (isInProcessing)
                return;

            isInProcessing = true;
            //WARNING: We need to do this to ensure all code run on our application UI thread
            //This is done because touch keyboard of mobile devices runs on a different thread
            //therefore an exception will be thrown if we don't use Dispatcher
            await mainPage.CurrentDispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                var control = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control);
                if (control.HasFlag(CoreVirtualKeyStates.Down))
                {
                    switch (e)
                    {
                        case VirtualKey.E:
                            if (currentCard != null) 
                                EditNoteButtonClickHandler(null, null);
                            break;
                        case VirtualKey.D:
                            DeleteButtonClickHandler(null, null);
                            break;
                        case VirtualKey.R:
                            RescheduleButtonClickHandler(null, null);
                            break;
                        case VirtualKey.P:
                            SuspendButtonClickHandler(null, null);
                            break;
                        case VirtualKey.Z:
                            UndoButtonClickHandler(null, null);
                            break;
                        default:
                            break;
                    }
                }
                else if (e == VirtualKey.R)
                {
                    await cardView.PlayAllMedia();                    
                }
                else if(isCustomDueTimeFlyoutOpen)
                {
                    if(e == VirtualKey.Enter)                   
                        await SaveRescheduleAndShowNextQuestion();                    
                }
                else if (showAnswerButton.Visibility == Visibility.Visible)
                {
                    if (e == VirtualKey.Enter)                    
                        await DisplayAnswer();                    
                }
                else
                {
                    if (e == VirtualKey.Number1 ||
                       e == VirtualKey.NumberPad1)
                    {
                        await CardButtonClickAnimateAsync(againButton);
                        await ShowNextCardAndSaveAnswer(Sched.AnswerEase.Again);                        
                    }
                    else if (e == VirtualKey.Number2 ||
                        e == VirtualKey.NumberPad2)
                    {
                        await CardButtonClickAnimateAsync(hardButton);
                        await ShowNextCardAndSaveAnswer(Sched.AnswerEase.Hard);                        
                    }
                    else if ((goodButton.Visibility == Visibility.Visible) && 
                        (e == VirtualKey.Number3 || e == VirtualKey.NumberPad3))
                    {
                        await CardButtonClickAnimateAsync(goodButton);
                        await ShowNextCardAndSaveAnswer(Sched.AnswerEase.Good);
                    }
                    else if ((easyButton.Visibility == Visibility.Visible) && 
                       (e == VirtualKey.Number4 || e == VirtualKey.NumberPad4))
                    {
                        await CardButtonClickAnimateAsync(easyButton);
                        await ShowNextCardAndSaveAnswer(Sched.AnswerEase.Easy);                        
                    }                    
                }
                isInProcessing = false;
                return;
            });
        }

        private async Task CardButtonClickAnimateAsync(CardButtonView button)
        {
            //WARNING: Always make sure storyboard has stopped
            fadeOutBoard.Stop();
            UIHelper.SetStoryBoardTarget(fadeOutAnimate, button.Name);
            fadeOutBoard.Begin();            
            await Task.Delay(200);
        }

        private async void DisplayAnswerEventHandler(object sender, RoutedEventArgs e)
        {
            await DisplayAnswer();
        }
        private async Task DisplayAnswer()
        {
            answer = await MungeAnswer(answer);
            await cardView.ChangeCardContent(answer, cardClass);
            ChangeToAnswerButtons();
            PlayMediaIfNeeded();

            DisplayAnswerEvent?.Invoke();
        }
        private async Task<string> MungeAnswer(string answerFromCard)
        {
            //WARNING: Not allow user to navigate from this page when
            //processing answer to avoid current thread being stuck in a loop
            isCanNavigateFrom = false;

            string answer = LaTeX.MungeQA(answerFromCard, collection);
            var result = await TypeAnsAnswerFilter(answer);

            isCanNavigateFrom = true;

            return result;
        }
        private async Task<string> TypeAnsAnswerFilter(string answer)
        {
            if (String.IsNullOrWhiteSpace(type.CorrectAnswer))
                return TypeAnswerRegex.Replace(answer, "");
            string userAnswer = await userInputGetter.GetInput();
            int origSize = answer.Length;
            answer =  CardInformationViewModel.AnswerRegex.Replace(answer, "");
            bool hadHR = answer.Length != origSize;
            //Munge Correct value
            var correctAnswer = Utils.StripHTML(collection.Media.Strip(type.CorrectAnswer));

            //Ensure we don't chomp multiple whitespace
            correctAnswer = correctAnswer.Replace("\xa0", " ");
            correctAnswer = EraseRedundantWhiteSpaces(correctAnswer);
            userAnswer = EraseRedundantWhiteSpaces(userAnswer);
            AnswerComparer comparer = new AnswerComparer(userAnswer, correctAnswer);
            string compareResult = comparer.GetResult();
            string htmlTag = @"<span style = ""font-family: '{0}'; font-size: {1}px""> {2} </span>";
            string html = String.Format(htmlTag, type.Font, type.Size, compareResult);
            if (hadHR)
            {
                // A hack to ensure the q/a separator falls before the answer
                // comparison when user is using {{FrontSide}}
                html = "<hr id=\"answer\">" + html;
            }
            return TypeAnswerRegex.Replace(answer, html);
        }       

        private static string EraseRedundantWhiteSpaces(string text)
        {
            text = text.Trim();
            text = Regex.Replace(text, " {2,}", " ");
            return text;
        }
        private void ChangeToAnswerButtons()
        {
            showAnswerButton.Visibility = Visibility.Collapsed;
            UpdateActiveAnswerButtons();
            UpdateAnswerButtonWidth();
            UnhideAnswerButtons();
            DisplayNextDueTime();
        }
        private void UpdateActiveAnswerButtons()
        {
            int count = collection.Sched.AnswerButtons(currentCard);
            activeAnswerButtons.Clear();

            againButton.Body = "AGAIN";
            activeAnswerButtons.Add(againButton);
            activeAnswerButtons.Add(hardButton);

            if (count >= 2 && count < 4)
            {
                hardButton.Body = "GOOD";
                if (count == 3)
                {
                    goodButton.Body = "EASY";
                    activeAnswerButtons.Add(goodButton);
                }
            }
            else if (count == 4)
            {
                hardButton.Body = "HARD";
                goodButton.Body = "GOOD";
                activeAnswerButtons.Add(goodButton);

                easyButton.Body = "EASY";
                activeAnswerButtons.Add(easyButton);
            }
        }
        private void UnhideAnswerButtons()
        {
            foreach (var button in activeAnswerButtons)
            {
                button.Visibility = Visibility.Visible;
            }
        }
        private void DisplayNextDueTime()
        {                        
            if (!CollectionOptionViewModel.IsShowNextReviewTimeEnable(collection.Conf))
                return;

            int i = 1;
            foreach (var button in activeAnswerButtons)
            {
                button.Header = collection.Sched.NextIntervalString(currentCard, (Sched.AnswerEase)(i));
                i++;
            }
        }

        private async void AgainButtonClickHandler(object sender, RoutedEventArgs e)
        {
            TouchTextPopupAnimation(againButton, 0);
            await ShowNextCardAndSaveAnswer(Sched.AnswerEase.Again);
        }
        private async void HardButtonClickHandler(object sender, RoutedEventArgs e)
        {
            TouchTextPopupAnimation(hardButton, 1);
            await ShowNextCardAndSaveAnswer(Sched.AnswerEase.Hard);
        }
        private async void GoodButtonClickHandler(object sender, RoutedEventArgs e)
        {
            TouchTextPopupAnimation(goodButton, 2);
            await ShowNextCardAndSaveAnswer(Sched.AnswerEase.Good);
        }
        private async void EasyButtonClickHandler(object sender, RoutedEventArgs e)
        {
            TouchTextPopupAnimation(easyButton, 3);
            await ShowNextCardAndSaveAnswer(Sched.AnswerEase.Easy);
        }

        private void TouchTextPopupAnimation(CardButtonView button, int offsetMultiplier)
        {
            if (UIHelper.IsHasPhysicalMouse())
                return;

            var task = mainPage.CurrentDispatcher.RunAsync(CoreDispatcherPriority.Normal, async () =>
            {
                buttonPopupText.Text = button.Body;
                buttonPopup.HorizontalOffset = (button.ActualWidth * offsetMultiplier) + button.ActualWidth / 2
                                                 - buttonPopup.ActualWidth / 2;
                buttonPopup.IsOpen = true;
                TextFadeInOut.Begin();
                await Task.Delay(200);
                buttonPopup.IsOpen = false;
            });
        }

        private async Task ShowNextCardAndSaveAnswer(Sched.AnswerEase ease)
        {
            PopCardFromUndoQueueIfNeeded();
            collection.Sched.AnswerCard(currentCard, ease);
            await ShowNextQuestion();
            mainPage.UndoButton.IsEnabled = true;
            AnswerButtonsPressEvent?.Invoke();
        }

        private async Task GotoNextQuestionWithoutAnswering()
        {            
            PopCardFromUndoQueueIfNeeded();
            await ShowNextQuestion();
            mainPage.UndoButton.IsEnabled = true;
        }

        private void PopCardFromUndoQueueIfNeeded()
        {
            if (isCardFromQueue)
            {
                if (undoCardQueue.Count != 0)
                    undoCardQueue.RemoveAt(undoCardQueue.Count - 1);                
                collection.Sched.DecrementCounts(currentCard);
            }
        }

        private void RemoveDuplicateInUndoQueueIfHas(long cardId)
        {
            int countExcludelast = undoCardQueue.Count - 1;
            if(countExcludelast > 0)
            {
                for(int  i = 0; i < countExcludelast; i++)
                {
                    if(undoCardQueue[i] == cardId)                    
                        undoCardQueue.RemoveAt(i);                    
                }
            }
        }

        private async Task ShowNextQuestion()
        {
            if (TryPopNextCard())
            {
                await ChangeHtmlHeaderIfNeeded();
                await GetContentAndDisplayQuestion();
                ClearInkCanVasIfNeeded();                
            }
        }
        private async Task ChangeHtmlHeaderIfNeeded()
        {
            if(currentDeckId != currentCard.DeckId)
            {
                currentDeckId = currentCard.DeckId;
                await ChangeHtmlheader();
            }
        }
        private bool TryPopNextCard()
        {            
            PopNextCard();
            if (currentCard == null)
            {
                if (collection.Deck.IsDyn(currentDeckId))                                    
                    collection.Deck.Remove(currentDeckId);                
                if (isCanGoBack && Frame.CanGoBack)
                {
                    FrameGoBack();
                    return false;
                }
            }
            return true;
        }
        private void ClearInkCanVasIfNeeded()
        {
            if (mainPage.IsInkOn())
                ClearInkCanvas();
        }

        private void NavigateToWebsiteStartEventHandler()
        {
            DisplayOnlyShowAnswerButton();
            showAnswerButton.Header = "";
            showAnswerButton.Body = "Go back to card view";

            cardView.CardHtmlLoadedEvent -= CardViewLoadedHandler;
            showAnswerButton.Click -= DisplayAnswerEventHandler;
            
            //Unhook to make sure we won't hook this event twice
            showAnswerButton.Click -= GoBackToCardViewFromWebsite;
            showAnswerButton.Click += GoBackToCardViewFromWebsite;
        }

        private void GoBackToCardViewFromWebsite(object sender, RoutedEventArgs e)
        {
            showAnswerButton.Click -= GoBackToCardViewFromWebsite;
            showAnswerButton.Click += DisplayAnswerEventHandler;
            cardView.CardHtmlLoadedEvent += GobackToCardViewHtmlLoadedEvent;
            cardView.ClearWebViewControl();
            cardView.ReloadCardView();
        }

        private async void GobackToCardViewHtmlLoadedEvent()
        {
            cardView.CardHtmlLoadedEvent -= GobackToCardViewHtmlLoadedEvent;
            cardView.CardHtmlLoadedEvent += CardViewLoadedHandler;
            await ChangeCardStyle();
            await cardView.ChangeCardContent(question);
            DisplayShowAnswerButton();
        }

        private void InkEraserButtonClickHandler(object sender, RoutedEventArgs e)
        {
            if (mainPage.IsInkEraserState())
            {
                ChangeInkToEraser();
            }
            else
            {
                ChangeInkToPen();
            }
        }

        private void ChangeInkToPen()
        {
            Ink.InkPresenter.InputProcessingConfiguration.Mode = InkInputProcessingMode.Inking;
        }

        private void ChangeInkToEraser()
        {
            Ink.InkPresenter.InputProcessingConfiguration.Mode = InkInputProcessingMode.Erasing;
        }

        private async void RescheduleButtonClickHandler(object sender, RoutedEventArgs e)
        {
            if (currentCard.Type != CardType.Review)
            {
                await UIHelper.ShowMessageDialog("You can't reschedule a new or in learning card.");                                
                return;
            }
            OpenRescheduleFlyout();
        }

        private void OpenRescheduleFlyout()
        {
            rescheduleFlyout.ShowAt(customDueTimeShowPoint);
            isCustomDueTimeFlyoutOpen = true;
        }

        private void NextIntervalTextBoxTextChangingHandler(TextBox sender, TextBoxTextChangingEventArgs args)
        {
            sender.Text = sender.Text.StripNonDigit();
            if (String.IsNullOrWhiteSpace(sender.Text))
                return;

            var number = int.Parse(sender.Text);
            if (number > MAX_RESCHEDULE)
                number = MAX_RESCHEDULE;
            sender.Text = number.ToString();
        }

        private void NextDueTimeCancelButtonClickHandler(object sender, RoutedEventArgs e)
        {
            CloseRescheduleFlyout();
        }

        private void CloseRescheduleFlyout()
        {
            rescheduleFlyout.Hide();
            isCustomDueTimeFlyoutOpen = false;
        }

        private async void NextDueTimeOkButtonClickHandler(object sender, RoutedEventArgs e)
        {
            await SaveRescheduleAndShowNextQuestion();
        }

        private async Task SaveRescheduleAndShowNextQuestion()
        {
            var text = nextDuetimeTextBox.Text;
            if (String.IsNullOrWhiteSpace(text))
            {
                CloseRescheduleFlyout();
                return;
            }

            var due = int.Parse(text);
            if (due <= 0)
            {
                await HandlerSmallRescheduleDueTime();
                return;
            }

            if (due > 999)
            {
                await HandleLargeRescheduleDueTime();
                return;
            }
            
            RescheduleReviewCard(due);            
            CloseRescheduleFlyout();
            await GotoNextQuestionWithoutAnswering();
        }

        private static async Task HandlerSmallRescheduleDueTime()
        {
            await UIHelper.ShowMessageDialog("If you want to re-learn this card please choose \"AGAIN\" option on the answer side");            
        }

        private static async Task HandleLargeRescheduleDueTime()
        {
            await UIHelper.ShowMessageDialog("If you don't want to review this card again you can suspend it instead of rescheduling.\n",
                                                                      "It's over 999+!");
        }

        private void RescheduleReviewCard(int due)
        {
            collection.MarkReview(currentCard);
            collection.Sched.RescheduleIntoReviewCards(new long[] { currentCard.Id }, due, due);
            UpdateScheduleWithoutAnswering();
        }

        private void EditButtonClickHandler(object sender, RoutedEventArgs e)
        {
            editFlyout.ShowAt(sender as AppBarButton);
        }

        private void EditNoteButtonClickHandler(object sender, RoutedEventArgs e)
        {
            editFlyout.Hide();

            editCardId = currentCard.Id;
            var currentNote = collection.GetNote(currentCard.NoteId);
            NoteEditorPageParameter param = new NoteEditorPageParameter() { Mainpage = mainPage, CurrentNote = currentNote };
            Frame.Navigate(typeof(NoteEditor), param);
        }

        private async void DeleteButtonClickHandler(object sender, RoutedEventArgs e)
        {
            bool isContinue = await UIHelper.AskUserConfirmation("This will permanently delete your card and its note if it has no cards left. Continue?");
            if (!isContinue)
                return;

            MarkUndoDelete();
            UpdateScheduleWithoutAnswering();
            collection.RemoveCardsAndNoteIfNoCardsLeft(new long[] { currentCard.Id });
            RemoveDuplicateInUndoQueueIfHas(currentCard.Id);
            await GotoNextQuestionWithoutAnswering();
        }

        private void MarkUndoDelete()
        {                        
            Note note = currentCard.LoadNote();
            List<Card> card = note.Cards();            
            long id = currentCard.Id;
            if (card.Count <= 1)
                collection.MarkUndo(Collection.UNDO_DELETE_NOTE, new object[] { note, card, id });
            else
                collection.MarkReview(currentCard);
        }

        private async void SuspendButtonClickHandler(object sender, RoutedEventArgs e)
        {
            bool isContinue = await UIHelper.AskUserConfirmation("You won't see this card again until you unsuspend it. Continue?");
            if (!isContinue)
                return;

            collection.MarkReview(currentCard);
            UpdateScheduleWithoutAnswering();
            collection.Sched.SuspendCards(currentCard.Id);
            RemoveDuplicateInUndoQueueIfHas(currentCard.Id);
            await GotoNextQuestionWithoutAnswering();
        }

        private void UpdateScheduleWithoutAnswering()
        {
            string type = null;
            switch(currentCard.Type)
            {
                case CardType.New:
                    type = "new";
                    break;
                case CardType.Learn:
                    type = "lrn";
                    break;
                case CardType.Review:
                    type = "rev";
                    break;
            }
            collection.Sched.UpdateTodayStats(currentCard, type);
            collection.Sched.UpdateTodayStats(currentCard, "time", currentCard.TimeTaken());
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

        private void TouchKeyboardShowing(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            mainPage.CommanBar.ClosedDisplayMode = AppBarClosedDisplayMode.Minimal;
        }

        private void TouchKeyboardHidingHandler(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            mainPage.CommanBar.ClosedDisplayMode = AppBarClosedDisplayMode.Compact;
        }

        private void EnterTutorialModeIfNeeded()
        {
            if (AllHelps.Tutorial == AllHelps.TutorialState.ViewCard)
            {
                DisplayAnswerEvent += TuTorialShowAnswerButtonClick;
                AnswerButtonsPressEvent += TutorialAnswerButtonsPressHandler;
                helpPopup = new HelpPopup();
                UIHelper.AddToGridInFull(mainGrid, helpPopup);                
                helpPopup.Title = "New + Learn + Review";
                helpPopup.SubtitleVisibility = Visibility.Collapsed;
                helpPopup.Text = "The numbers on \"Show Answer\" button display today's cards" + 
                                " of this deck that you need to view.";
                helpPopup.ShowWithClose();
            }
        }

        private void TutorialAnswerButtonsPressHandler()
        {
            if (numerOfAnswerPress == 0)
            {                
                helpPopup.Title = "New -> Learn -> Review";
                helpPopup.SubtitleVisibility = Visibility.Collapsed;
                helpPopup.Text = "A new card will become a learn card after you answer it."
                                 + " Answering it again with \"GOOD\" or \"EASY\" will make it become a review card.";
                helpPopup.ShowWithClose();
            }
            else
            {
                AnswerButtonsPressEvent -= TutorialAnswerButtonsPressHandler;
                DisplayAnswerEvent -= TuTorialShowAnswerButtonClick;
                helpPopup.Hide();                
            }
            numerOfAnswerPress++;
        }

        private void TuTorialShowAnswerButtonClick()
        {
            if (currentCard.Type == CardType.New && numerOfAnswerPress == 0)
            {
                helpPopup.Title = "Choose an Answer";
                helpPopup.SubTitle = "(Keyboard shortcuts: 1 -> 4)";
                helpPopup.SubtitleVisibility = Visibility.Visible;
                helpPopup.Text = "Choosing \"AGAIN\" will let you relearn forgotten cards.\n"
                      + "Other buttons will schedule the next time you see this card (due time) based on SuperMemo algorithm.\n"
                      + "Please choose \"GOOD\" to continue.";
                helpPopup.ShowWithClose();
            }
            else if(numerOfAnswerPress == 1)
            {                
                helpPopup.Title = "Next View Time";
                helpPopup.SubTitle = "(min)ute. (d)ay. (mon)th. (y)ear.";
                helpPopup.SubtitleVisibility = Visibility.Visible;
                helpPopup.Text = "You can manually set the next due time by pressing on the \"Edit\" icon and choose \"Reschedule\".\n" +
                    "However please avoiding doing that if possible, as it may affect your retention rate.";
                helpPopup.Show();

                AllHelps.Tutorial = AllHelps.TutorialState.SharedDeck;
            }
        }

        private void StoryboardOutBoardCompletedHandler(object sender, object e)
        {
            var storyBoard = sender as Storyboard;
            storyBoard.Stop();
        }
        
    }
}
