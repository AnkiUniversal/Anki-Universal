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

using AnkiU.UIUtilities;
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
using Windows.UI.Xaml.Navigation;

namespace AnkiU.UserControls
{
    public sealed partial class NumbericTextBox : UserControl
    {
        public event TextChangedEventHandler NumberChanged;

        public int Number
        {
            get { return (int)GetValue(NumberProperty); }
            set { SetValue(NumberProperty, value); }
        }

        public static readonly DependencyProperty NumberProperty =
            DependencyProperty.Register("Number", typeof(int), typeof(NumbericTextBox), new PropertyMetadata(0, new PropertyChangedCallback(OnValueChanged)));


        private static async void OnValueChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            NumbericTextBox control = d as NumbericTextBox;
            string content = e.NewValue.ToString();

            //WARNING: We have to use this instead of just databinding like normal
            //or else we get access exception from UI thread because of TextChanging Event
            //This does not apply to TextAlignment as we don't bind it to the data model
            await CoreWindow.GetForCurrentThread().Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => {
                control.textBox.Text = content;
            });
        }

        public TextAlignment TextAlignment
        {
            get { return (TextAlignment)GetValue(TextAligmentPropgerty); }
            set { SetValue(TextAligmentPropgerty, value); }
        }
        
        public static readonly DependencyProperty TextAligmentPropgerty =
            DependencyProperty.Register("TextAlignment", typeof(TextAlignment), typeof(NumbericTextBox), new PropertyMetadata(TextAlignment.Left));

        public int MaxNumber
        {
            get { return (int)GetValue(MaxNumberProperty); }
            set { SetValue(MaxNumberProperty, value); }
        }
        
        public static readonly DependencyProperty MaxNumberProperty =
            DependencyProperty.Register("MaxNumber", typeof(int), typeof(NumbericTextBox), new PropertyMetadata(9999));

        public int MinNumber
        {
            get { return (int)GetValue(MinNumberProperty); }
            set { SetValue(MinNumberProperty, value); }
        }
        
        public static readonly DependencyProperty MinNumberProperty =
            DependencyProperty.Register("MinNumber", typeof(int), typeof(NumbericTextBox), new PropertyMetadata(0));

        public NumbericTextBox()
        {
            this.InitializeComponent();
        }

        private void TextBoxTextChangingHandler(TextBox sender, TextBoxTextChangingEventArgs args)
        {            
            var text = sender.Text;
            text = text.StripNonDigit();
            long value;

            if (String.IsNullOrWhiteSpace(text))
            {
                sender.Text = "";
                return;
            }
            else
                value = long.Parse(text);

            if (value > MaxNumber)
                value = MaxNumber;
            else if (value < MinNumber)
                value = MinNumber;

            Number = (int)value;
            sender.Text = value.ToString();
            NumberChanged?.Invoke(sender, null);
        }
    }
}
