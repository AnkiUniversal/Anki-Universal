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
using AnkiU.UIUtilities;
using AnkiU.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace AnkiU.Pages
{
    public sealed partial class DeckOptionsPage : Page, INightReadMode
    {
        public const double DESIGN_WIDTH_SIZE = 450;
        public const double DESIGN_HEIGHT_SIZE = 600;
        public const double DESIGN_DIMENSION = DESIGN_HEIGHT_SIZE/ DESIGN_WIDTH_SIZE;

        private DeckSelectPage deckSelectPage;
        private Collection collection;
        private long currentDeckId;

        private int oldCardOrder;        
        //If user select add or edit then old deck config != selected config
        //Use this variable to dectect order changes
        private int oldCardOrderOfSelectedDeck;

        private JsonObject config;
        private string oldName;
        private bool isExpertMode = false;

        public string CurrentName { get; set; }

        Dictionary<IAnkiDeckOptionsViewModel, IAnkiDeckOptionsView> Options;

        private bool isNightMode = false;

        private DeckSimpleOptionsViewModel simpleConfig;

        private DeckGeneralOptionsViewModel generalConfig = null;
        private DeckNewOptionsViewModel newConfig = null;
        private DeckReviewOptionsViewModel reviewConfig = null;
        private DeckLapseOptionsViewModel lapseConfig = null;        

        public DeckOptionsPage()
        {
            this.InitializeComponent();
            Options = new Dictionary<IAnkiDeckOptionsViewModel, IAnkiDeckOptionsView>();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            deckSelectPage = e.Parameter as DeckSelectPage;
            if (deckSelectPage == null)
                throw new Exception("Wrong input parameter!");

            HookAllEvents();

            collection = deckSelectPage.Collection;
            collection.Database.SaveTransactionPoint();
            currentDeckId = deckSelectPage.SelectedDeckId;
            config = collection.Deck.GetConf(deckSelectPage.SelectedConfigId);
            oldCardOrder = (int)config.GetNamedObject("new").GetNamedNumber("order");
            oldCardOrderOfSelectedDeck = (int)collection.Deck.ConfForDeckId(currentDeckId)
                                              .GetNamedObject("new").GetNamedNumber("order");

            if (!deckSelectPage.IsCreatingNewConfig)
            {
                oldName = config.GetNamedString("name");
                CurrentName = oldName;
            }
            else
            {
                config = JsonObject.Parse(Utils.JsonToString(config));
                CurrentName = "";
            }

            InitSimpleOptionView();
        }

        private void HookAllEvents()
        {
            deckSelectPage.MainPage.SaveButton.Visibility = Visibility.Visible;
            deckSelectPage.MainPage.EnableChangingReadMode(this);
            ChangeBackgroundColor();
            deckSelectPage.MainPage.SaveButton.Click += SaveButtonClickHandler;
            CoreWindow.GetForCurrentThread().KeyDown += OptionPageKeyDownHandler;
        }

        private async void OptionPageKeyDownHandler(CoreWindow sender, KeyEventArgs args)
        {
            await deckSelectPage.MainPage.CurrentDispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                var control = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control);
                if (control.HasFlag(CoreVirtualKeyStates.Down) && args.VirtualKey == VirtualKey.S)
                {
                    deckSelectPage.MainPage.SaveButtonClickAnimateAsync();
                    SaveButtonClickHandler(null, null);
                }
            });
        }

        private void InitSimpleOptionView()
        {
            if (simpleConfig == null)
            {
                simpleConfig = new DeckSimpleOptionsViewModel(config);
                simpleConfigView.SetDataModel(simpleConfig);
            }
            simpleConfig.GetOptionsToView();            
            Options.Add(simpleConfig, simpleConfigView);
        }

        private void InitExpertOptionTabs()
        {
            if (rootTab == null)
            {
                FindName("rootTab");
                generalConfig = new DeckGeneralOptionsViewModel(config);
                newConfig = new DeckNewOptionsViewModel(config);
                reviewConfig = new DeckReviewOptionsViewModel(config);
                lapseConfig = new DeckLapseOptionsViewModel(config);
            }

            Options.Add(generalConfig, generalView);
            Options.Add(newConfig, newView);
            Options.Add(reviewConfig, reviewView);
            Options.Add(lapseConfig, lapseView);

            foreach (var option in Options)
            {
                option.Key.GetOptionsToView();
                option.Value.SetDataModel(option.Key);
            }
        }

        private async void SaveButtonClickHandler(object sender, RoutedEventArgs e)
        {            
            if (await CheckNameEmpty())
                return;

            if (await CheckNoDuplicateName())
                return;

            SaveConfigToDeck();
            deckSelectPage.MainPage.SaveAndStartNewDatabaseSessionAsync();
        }
        private async Task<bool> CheckNameEmpty()
        {
            if (isExpertMode)
                CurrentName = Utils.GetValidName(currentNameExpertView.Text);
            else
                CurrentName = Utils.GetValidName(currentNameSimpleView.Text);

            if (String.IsNullOrWhiteSpace(CurrentName))
            {
                await UIHelper.ShowMessageDialog("Please enter a name for this configuration");                
                return true;
            }
            return false;
        }
        private async Task<bool> CheckNoDuplicateName()
        {           
            if(!deckSelectPage.IsCreatingNewConfig)
                //Is editting a config without revising its name?
                if (CurrentName == oldName)
                    return false;

            //Revise the name of a config or create new config
            //make sure the same name has not already been used
            foreach (var c in collection.Deck.AllConf())
            {
                string exist = c.GetNamedString("name");
                if (exist.Equals(CurrentName, StringComparison.OrdinalIgnoreCase))
                {
                    await UIHelper.ShowMessageDialog("A configuration with the same name already exists.");                    
                    return true;                   
                }
            }
            return false;
        }
        private void SaveConfigToDeck()
        {
            config["name"] = JsonValue.CreateStringValue(CurrentName);

            foreach (var option in Options)
                option.Key.SaveOptionsToJsonConfig();            

            if (deckSelectPage.IsCreatingNewConfig)
            {
                long newConfigId = collection.Deck.CreateNewConfiguration(CurrentName, Utils.JsonToString(config));
                config = collection.Deck.GetConf(newConfigId);
            }
            ResortAllCardsOfDecksIfNeeded();

            var currentDeck = collection.Deck.Get(currentDeckId);
            currentDeck["conf"] = config.GetNamedValue("id");
            collection.Deck.Save(currentDeck);
            collection.Deck.Save(config);
            collection.SaveAndCommitAsync();
            Frame.GoBack();
        }

        private void ResortAllCardsOfDecksIfNeeded()
        {
            var newOrder = (int)config.GetNamedObject("new").GetNamedNumber("order");
            if (oldCardOrder != newOrder)
            {
                //Resort all other decks using this conf first
                if (!deckSelectPage.IsCreatingNewConfig)
                    collection.Sched.ResortConf(config);

                //Make sure current deck is also sorted
                var deckIds = collection.Deck.DeckIdsForConf(config);
                if (!deckIds.Contains(currentDeckId))
                {
                    ResortSelectedDeck(newOrder);
                }
            }
            else if(oldCardOrderOfSelectedDeck != newOrder)
            {
                ResortSelectedDeck(newOrder);
            }
        }

        private void ResortSelectedDeck(int newOrder)
        {
            if (newOrder == (int)NewCardInsertOrder.RANDOM)
                collection.Sched.RandomizeCards(currentDeckId);
            else
                collection.Sched.OrderCards(currentDeckId);
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            UnHookAllEvents();
            deckSelectPage.IsCreatingNewConfig = false;

            base.OnNavigatingFrom(e);
        }

        protected override void OnNavigatedFrom(NavigationEventArgs e)
        {
            base.OnNavigatedFrom(e);
            collection.Database.Commit();            
        }

        private void UnHookAllEvents()
        {
            deckSelectPage.MainPage.DisableChangingReadMode();
            deckSelectPage.MainPage.SaveButton.Visibility = Visibility.Collapsed;
            deckSelectPage.MainPage.SaveButton.Click -= SaveButtonClickHandler;
            CoreWindow.GetForCurrentThread().KeyDown -= OptionPageKeyDownHandler;
        }

        private void ViewModeButtonClickHandler(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            isExpertMode = !isExpertMode;
            Options.Clear();
            if (isExpertMode)
            {                        
                InitExpertOptionTabs();
                rootTab.Visibility = Visibility.Visible;
                simpleViewRootGrid.Visibility = Visibility.Collapsed;
                button.Content = "Simple Mode";                 
            }
            else
            {
                InitSimpleOptionView();
                rootTab.Visibility = Visibility.Collapsed;
                simpleViewRootGrid.Visibility = Visibility.Visible;
                button.Content = "Expert Mode";
            }
        }

        private void WindowSizeChangedHandler(object sender, SizeChangedEventArgs e)
        {
            double width = e.NewSize.Width;
            double height = e.NewSize.Height;
            ScaleWithWindow(width, height, rootGridScale);
        }

        public static void ScaleWithWindow(double width, double height, CompositeTransform rootGridScale)
        {
            double widthScale = (width - DESIGN_WIDTH_SIZE) / (DESIGN_WIDTH_SIZE) + 0.8;
            double heightScale = (height - DESIGN_HEIGHT_SIZE) / (DESIGN_HEIGHT_SIZE) + 0.8;

            double scale = (widthScale > heightScale) ? heightScale : widthScale;
            if (scale >= 1)
            {
                rootGridScale.ScaleX = scale;
                rootGridScale.ScaleY = scale;
            }
            else
            {
                rootGridScale.ScaleX = 1;
                rootGridScale.ScaleY = 1;
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
            if (isNightMode)
            {
                simpleConfigView.Background = new SolidColorBrush(Windows.UI.Colors.Black);
            }
            else
            {
                simpleConfigView.Background = new SolidColorBrush(Windows.UI.Colors.White);
            }
            otherContentBorder.Background = simpleConfigView.Background;
        }
    }
}
