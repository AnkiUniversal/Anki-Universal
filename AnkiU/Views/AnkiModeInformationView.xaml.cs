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
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace AnkiU.Views
{
    public sealed partial class AnkiModeInformationView : UserControl
    {       
        public string Label { get { return label.Text; } set { label.Text = value; } }

        public event SelectionChangedEventHandler ComboBoxSelectionChangedEvent;

        public ComboBox ModelComboBox { get { return comboBox; } }

        public SolidColorBrush TextForeGround
        {
            get { return (SolidColorBrush)GetValue(TextForeGroundProperty); }
            set { SetValue(TextForeGroundProperty, value); }
        }
        
        public static readonly DependencyProperty TextForeGroundProperty =
            DependencyProperty.Register("TextForeGround", typeof(SolidColorBrush), typeof(AnkiModeInformationView), new PropertyMetadata(new SolidColorBrush(Windows.UI.Colors.Black)));


        public AnkiModeInformationView()
        {
            this.InitializeComponent();            
        }        

        private void ComboBoxSelectionChangedHandler(object sender, SelectionChangedEventArgs e)
        {
            ComboBoxSelectionChangedEvent?.Invoke(sender, e);
        }


        public long GetSelectedModelId()
        {
            var model = comboBox.SelectedItem as AnkiModelInformation;
            return model.Id;
        }

        public void ChangeSelectedIndex(int index)
        {
            comboBox.SelectedIndex = index;
        }

        public void ChangeSelectedItem(long id)
        {            
            foreach(var item in comboBox.Items)
            {
                var model = item as AnkiModelInformation;
                if (model.Id == id)
                {
                    comboBox.SelectedItem = item;
                    break;
                }
            }
        }

        public string CurrentName()
        {
            var item = comboBox.SelectedItem as AnkiModelInformation;
            return item.Name;
        }

        public void ChangeSelectedItemName(string name)
        {            
            var data = comboBox.SelectedItem as AnkiModelInformation;
            data.Name = name;                                               
        }

        public void DisableModelSelection()
        {
            comboBox.IsEnabled = false;
        }

        public void EnableModelSelection()
        {
            comboBox.IsEnabled = true;
        }

        //WARNING: This is the only way to solve random null reference in data binding with textblock
        //There must be an error in Data Binding framework that TextBlock is GB collected and causes null reference
        private Dictionary<TextBlock, bool> textBlockDict = new Dictionary<TextBlock, bool>();
        private void TextBlock_Loaded(object sender, RoutedEventArgs e)
        {
            textBlockDict[sender as TextBlock] = true;                        
        }
    }
}
