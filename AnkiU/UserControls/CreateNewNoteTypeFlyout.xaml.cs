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

// The User Control item template is documented at https://go.microsoft.com/fwlink/?LinkId=234236

namespace AnkiU.UserControls
{
    public sealed partial class CreateNewNoteTypeFlyout : UserControl
    {
        private AnkiModelInfomartionViewModel modelViewModel;
        private Collection collection;
        private FrameworkElement placeToShow;
        CoreDispatcher dispatcher;
        public delegate void NewNoteTypeCreatedHandler(string name, long id);
        public event NewNoteTypeCreatedHandler NewNoteTypeCreatedEvent;

        private bool isOkPress = false;
        private bool isError = false;

        public CreateNewNoteTypeFlyout(Collection collection, CoreDispatcher dispatcher)
        {
            this.InitializeComponent();

            this.InitializeComponent();
            this.collection = collection;
            this.dispatcher = dispatcher;

            modelViewModel = new AnkiModelInfomartionViewModel(collection.Models.All());
            modelView.DataContext = modelViewModel.Models;
            modelView.Label = "";
            modelView.ChangeSelectedIndex(0);

            addNoteTypeFlyout.Closed += AddNoteFlyoutClosed;

            //A little hack to make sure combobox won't show when touchkey board is showing
            //If this is not done, white out error will happne on combobox
            InputPane.GetForCurrentView().Showing += TouchKeyboardShowingHandler;
            InputPane.GetForCurrentView().Hiding += TouchKeyboardHidingHandler;
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
            addNoteTypeFlyout.Placement = placement;
            addNoteTypeFlyout.ShowAt(placeToShow);
        }

        private void CancelButtonClickHandler(object sender, RoutedEventArgs e)
        {
            isOkPress = false;
            addNoteTypeFlyout.Hide();
        }

        private void AddNoteFlyoutClosed(object sender, object e)
        {
            if (!isOkPress)
            {
                if (isError)
                {
                    isError = false;
                    return;
                }
            }
        }

        private async void KeyUpEventHandler(object sender, KeyRoutedEventArgs e)
        {
            await dispatcher.RunAsync(CoreDispatcherPriority.Normal, () =>
            {
                if (e.Key == Windows.System.VirtualKey.Enter)
                    OkButtonClick(null, null);
            });
        }

        private async void OkButtonClick(object sender, RoutedEventArgs e)
        {
            string noteTypeName = Utils.GetValidName(noteTypeNameTextBox.Text);

            bool isValid = await CheckDeckAndNoteName(noteTypeName);
            if (!isValid)
                return;

            isOkPress = true;
            isError = false;
            addNoteTypeFlyout.Hide();

            long modelCopyFromID = modelView.GetSelectedModelId();

            JsonObject modelCloneFrom = collection.Models.Get(modelCopyFromID);
            var model = collection.Models.Copy(modelCloneFrom);
            model["name"] = JsonValue.CreateStringValue(noteTypeName);
            
            collection.Models.Save(model);            
            collection.SaveAndCommit();

            NewNoteTypeCreatedEvent?.Invoke(model.GetNamedString("name"), (long)model.GetNamedNumber("id"));
        }

        private async Task<bool> CheckDeckAndNoteName(string noteName)
        {     
            if (string.IsNullOrWhiteSpace(noteName))
            {
                isError = true;
                await UIHelper.ShowMessageDialog("Please enter a valid name.");
                addNoteTypeFlyout.ShowAt(placeToShow);
                return false;
            }

            bool isValid = await CheckIfNameValid(noteName, collection.Models.AllNames(),
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
                    addNoteTypeFlyout.ShowAt(placeToShow);
                    return false;
                }
            }

            return true;
        }
    }
}
