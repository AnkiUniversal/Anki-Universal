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
using Windows.UI.Core;
using Windows.UI.ViewManagement;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace AnkiU.UserControls
{
    public sealed partial class CreateNewDeckFlyout : UserControl
    {        
        private AnkiModelInfomartionViewModel modelViewModel;
        private Collection collection;
        private FrameworkElement placeToShow;
        CoreDispatcher dispatcher;
        public delegate void NewDeckCreatedHandler(long deckId);
        public event NewDeckCreatedHandler NewDeckCreatedEvent;
        public event NoticeRoutedHandler ClosedWithoutCreatingDeckEvent;

        private bool isOkPress = false;
        private bool isError = false;

        public CreateNewDeckFlyout(Collection collection, CoreDispatcher dispatcher)
        {
            this.InitializeComponent();
            this.collection = collection;
            this.dispatcher = dispatcher;

            modelViewModel = new AnkiModelInfomartionViewModel(collection.Models.All());
            modelView.DataContext = modelViewModel.Models;
            modelView.Label = "";
            modelView.ChangeSelectedIndex(0);

            addDeckFlyout.Closed += AddDeckFlyoutClosed;

            //A little hack to make sure combobox won't show when touchkey board is showing
            //If this is not done, white out error will happne on combobox
            InputPane.GetForCurrentView().Showing += TouchKeyboardShowingHandler;
            InputPane.GetForCurrentView().Hiding += TouchKeyboardHidingHandler;
        }

        private void AddDeckFlyoutClosed(object sender, object e)
        {
            if (!isOkPress)
            {
                if(isError)
                {
                    isError = false;
                    return;
                }

                ClosedWithoutCreatingDeckEvent?.Invoke();
            }
        }

        private void TouchKeyboardHidingHandler(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            modelView.ModelComboBox.IsHitTestVisible = true;
        }

        private void TouchKeyboardShowingHandler(InputPane sender, InputPaneVisibilityEventArgs args)
        {
            modelView.ModelComboBox.IsHitTestVisible = false;
        }

        public void ShowFlyout(FrameworkElement element, FlyoutPlacementMode placement)
        {
            isOkPress = false;
            this.placeToShow = element;
            addDeckFlyout.Placement = placement;
            addDeckFlyout.ShowAt(placeToShow);
        }

        private void CancelButtonClickHandler(object sender, RoutedEventArgs e)
        {
            isOkPress = false;
            addDeckFlyout.Hide();            
        }

        private async void OkButtonClick(object sender, RoutedEventArgs e)
        {            
            string deckName = Utils.GetValidName(deckNameTextBox.Text);
            string noteName = Utils.GetValidName(noteTypeNameTextBox.Text);

            bool isValid = await CheckDeckAndNoteName(deckName, noteName);
            if (!isValid)
                return;

            isOkPress = true;
            isError = false;
            addDeckFlyout.Hide();
            long? deckId = collection.Deck.AddOrResuedDeck(deckName, true);
            if (deckId == null)
            {
                await UIHelper.ShowMessageDialog("Unexpected error!");                
                return;
            }

            long modelCopyFromID = modelView.GetSelectedModelId();            
            ProgressDialog dialog = new ProgressDialog();
            dialog.ProgressBarLabel = "";
            dialog.ShowInDeterminateStateNoStopAsync("Add new deck");            

            var task = Task.Run(async () =>
            {
                var modelCloneFrom = collection.Models.Get(modelCopyFromID);
                var model = collection.Models.Copy(modelCloneFrom);
                model["name"] = JsonValue.CreateStringValue(noteName);

                var deckJson = collection.Deck.Get(deckId);
                deckJson["mid"] = model["id"];
                model["did"] = JsonValue.CreateNumberValue((long)deckId);

                collection.Models.Save(model);
                collection.Deck.Save(deckJson);
                collection.SaveAndCommit();

                await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
                {
                    dialog.Hide();
                    NewDeckCreatedEvent?.Invoke((long)deckId);
                });
            });
        }

        private async Task<bool> CheckDeckAndNoteName(string deckName, string noteName)
        {
            if (string.IsNullOrWhiteSpace(deckName))
            {
                isError = true;
                await UIHelper.ShowMessageDialog("Please enter a valid deck name.");
                addDeckFlyout.ShowAt(placeToShow);
                return false;
            }

            bool isValid = await CheckIfNameValid(deckName, collection.Deck.AllNames(),
                                                 "A deck with the same name already exists. Please enter a different one.");
            if (!isValid)
                return false;
            
            if (string.IsNullOrWhiteSpace(noteName))
            {
                isError = true;
                await UIHelper.ShowMessageDialog("Please enter a valid note type name.");
                addDeckFlyout.ShowAt(placeToShow);
                return false;
            }
            isValid = await CheckIfNameValid(noteName, collection.Models.AllNames(),
                                     "A note type with the same name already exists. Please enter a different one.");
            if (!isValid)
                return false;

            return true;
        }

        private async Task<bool> CheckIfNameValid(string newName, List<string> existsName, string errorMessage)
        {            
            foreach (var name in existsName)
            {
                if (name.Equals(newName, StringComparison.OrdinalIgnoreCase))
                {
                    isError = true;
                    await UIHelper.ShowMessageDialog(errorMessage);
                    addDeckFlyout.ShowAt(placeToShow);
                    return false;
                }
            }

            return true;
        }

        private async void KeyUpEventHandler(object sender, KeyRoutedEventArgs e)
        {
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (e.Key == Windows.System.VirtualKey.Enter)
                    OkButtonClick(null, null);
            });
        }
    }
}
