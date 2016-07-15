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

using AnkiU.Models;
using AnkiU.UIUtilities;
using AnkiU.UserControls;
using AnkiU.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
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
    public sealed partial class TemplateInformationView : UserControl
    {

        private bool isSuppressComboxSelectionChangeEvent = false;
        private MenuFlyout editMenuFlyout;
        private NameEnterFlyout renameFlyout = null;
        private NameEnterFlyout addNewFlyout = null;
        private IntNumberEnterFlyout respositionFlyout = null;

        public event RoutedEventHandler FlipButtonClick;
        public event RoutedEventHandler SaveEvent;
        public event RoutedEventHandler ComboBoxSelectionChangedEvent;

        public event NoticeRoutedHandler AddTemplateEvent;
        public event NoticeRoutedHandler FlipCardEvent;

        private TemplateInformationViewModel viewModel;
        public TemplateInformationViewModel ViewModel
        {
            get { return viewModel; }
            set
            {
                if (viewModel != null)
                    viewModel.Templates.CollectionChanged -= TemplatesCollectionChangedHandler;

                viewModel = value;
                this.DataContext = viewModel.Templates;
                viewModel.Templates.CollectionChanged += TemplatesCollectionChangedHandler;
                UpdateButtonState();
            }
        }

        private void UpdateButtonState()
        {
            if (viewModel.CurrentModel.GetNamedNumber("type") == (int)AnkiCore.ModelType.CLOZE)
            {
                addTemplateButton.Visibility = Visibility.Collapsed;                
                deleteButton.Visibility = Visibility.Collapsed;
                comboBox.IsEnabled = false;
                return;
            }

            if (viewModel.Templates.Count > 1)
            {
                deleteButton.IsEnabled = true;
                comboBox.IsEnabled = true;
            }
            else
            {
                deleteButton.IsEnabled = false;
                comboBox.IsEnabled = false;
            }
        }

        public TemplateInformationView()
        {
            this.InitializeComponent();            
        }

        public void BeginAnimation()
        {
            NoticeMe.Begin();
        }

        public void StopAnimation()
        {
            NoticeMe.Stop();
        }

        public void SetAnimationOnEdit()
        {
            NoticeMe.Stop();
            UIHelper.SetStoryBoardTarget(BlinkingBlue, editButton.Name);
        }

        private void TemplatesCollectionChangedHandler(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            UpdateButtonState();
        }

        private void ComboBoxSelectionChangedHandler(object sender, SelectionChangedEventArgs e)
        {
            if(!isSuppressComboxSelectionChangeEvent)
                ComboBoxSelectionChangedEvent?.Invoke(sender, e);
        }

        private async void AddButtonClickHandler(object sender, RoutedEventArgs e)
        {
            var isContinue = await MainPage.WarnFullSyncIfNeeded();
            if (!isContinue)
                return;

            if (addNewFlyout == null)
            {
                addNewFlyout = new NameEnterFlyout();
                addNewFlyout.OkButtonClickEvent += NewTemplateFlyoutOKButtonClickHandler;
            }            
            addNewFlyout.Show(sender as Button, "");
        }

        public void ChangeSelectedItem(long ord)
        {
            foreach (var item in comboBox.Items)
            {
                var model = item as TemplateInformation;
                if (model.Ord == ord)
                {
                    comboBox.SelectedItem = item;
                    break;
                }
            }
        }

        private async void NewTemplateFlyoutOKButtonClickHandler(object sender, RoutedEventArgs e)
        {
            var name = addNewFlyout.NewName;
            bool isValid = await CheckNameValid(name);
            if (!isValid)
                return;

            var templateToClone = comboBox.SelectedItem as TemplateInformation;
            ViewModel.AddNewTemplate(name, templateToClone.Ord);            
            comboBox.SelectedItem = viewModel.Templates.Last();
            MainPage.UserPrefs.IsFullSyncRequire = true;
            SaveEvent?.Invoke(sender, null);
            AddTemplateEvent?.Invoke();
        }

        private async Task<bool> CheckNameValid(string name)
        {
            return await UIHelper.CheckValidName(name, viewModel.Templates, 
                         "This note type already has a template with the same name. Please enter another name.");
        }

        private async void DeleteButtonClickHandler(object sender, RoutedEventArgs e)
        {
           if(viewModel.Templates.Count == 1)
            {
                await UIHelper.ShowMessageDialog("One note type needs at least one template!");
                return;
            }

            bool isContinue = await MainPage.WarnFullSyncIfNeeded();
            if (!isContinue)
                return;

            
            var countNote = viewModel.Models.TmplUseCount(viewModel.CurrentModel, comboBox.SelectedIndex);

            string message = String.Format(UIConst.WARN_TEMPLATE_DELETE, countNote);
            isContinue = await UIHelper.AskUserConfirmation(message);
            if (!isContinue)
                return;            

            isSuppressComboxSelectionChangeEvent = true;
            var template = comboBox.SelectedItem as TemplateInformation;
            uint ord = template.Ord;
            viewModel.RemoveTemplate(template.Ord);                
            if (ord > 0)
                comboBox.SelectedItem = viewModel.Templates[(int)ord - 1];     
            else
                comboBox.SelectedItem = viewModel.Templates[0];
            ComboBoxSelectionChangedEvent?.Invoke(comboBox, e);                
            isSuppressComboxSelectionChangeEvent = false;
            MainPage.UserPrefs.IsFullSyncRequire = true;
            SaveEvent?.Invoke(sender, null);            
        }

        private void RenameMenuFlyoutItemClickHandler(object sender, RoutedEventArgs e)
        {
            if (renameFlyout == null)
            {
                renameFlyout = new NameEnterFlyout();
                renameFlyout.OkButtonClickEvent += RenameTemplateFlyoutOKButtonClickHandler;
            }
            renameFlyout.Show(editButton);
        }

        private async void RenameTemplateFlyoutOKButtonClickHandler(object sender, RoutedEventArgs e)
        {
            string name = renameFlyout.NewName;
            bool isValid = await CheckNameValid(name);
            if (!isValid)
            {
                renameFlyout.Show(editButton);
                return;
            }

            isSuppressComboxSelectionChangeEvent = true;
            var template = comboBox.SelectedItem as TemplateInformation;

            viewModel.RenameTemplate(name, template.Ord);

            ChangeSelectedItem(template.Ord);
            isSuppressComboxSelectionChangeEvent = false;
            SaveEvent?.Invoke(sender, null);
        }

        private void EditButtonClickHandler(object sender, RoutedEventArgs e)
        {            
            if (editMenuFlyout == null)
            {
                editMenuFlyout = Resources["EditMenuFlyout"] as MenuFlyout;
                editMenuFlyout.Placement = FlyoutPlacementMode.Bottom;
            }

            editMenuFlyout.ShowAt(editButton);
        }

        private void RepositionMenuFlyoutItemClickHandler(object sender, RoutedEventArgs e)
        {
            if (respositionFlyout == null)
            {
                respositionFlyout = new IntNumberEnterFlyout();
                respositionFlyout.OKButtonClickEvent += ReposOKButtonClickHandler;
            }
            var maxOrder = viewModel.Templates.Count;
            var textToShow = "Enter new position (1..." + maxOrder + ")";
            respositionFlyout.Number = 1;
            respositionFlyout.Show(editButton, textToShow, maxOrder, 1);
        }

        private void ReposOKButtonClickHandler(object sender, RoutedEventArgs e)
        {
            isSuppressComboxSelectionChangeEvent = true;
            var currentOrd = (comboBox.SelectedItem as TemplateInformation).Ord;
            var newOrd = respositionFlyout.Number - 1;
            viewModel.RepositionTemplate(currentOrd, newOrd);            
            ChangeSelectedItem(newOrd);
            isSuppressComboxSelectionChangeEvent = false;
            SaveEvent?.Invoke(sender, null);
        }

        private void FliptMenuFlyoutItemClickHandler(object sender, RoutedEventArgs e)
        {
            FlipButtonClick?.Invoke(sender, e);
            FlipCardEvent?.Invoke();
        }
    }
}
