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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

namespace AnkiU.UserControls
{
    public sealed partial class CardButtonView : UserControl
    {
        public event RoutedEventHandler Click;

        public Visibility HeaderVisibility
        {
            get { return (Visibility)GetValue(HeaderVisibilityProperty); }
            set { SetValue(HeaderVisibilityProperty, value); }
        }
       
        public static readonly DependencyProperty HeaderVisibilityProperty =
            DependencyProperty.Register("HeaderVisibility", typeof(Visibility), typeof(CardButtonView), new PropertyMetadata(Visibility.Visible));

        public string Header
        {
            get
            {
                return header.Text;
            }
            set
            {
                header.Text = value;
                if (!String.IsNullOrEmpty(header.Text))
                    header.Visibility = Visibility.Visible;
                else
                    header.Visibility = Visibility.Collapsed;
            }
        }

        public string Body
        {
            get { return (string)GetValue(BodyProperty); }
            set { SetValue(BodyProperty, value); }
        }
        
        public static readonly DependencyProperty BodyProperty =
            DependencyProperty.Register("Body", typeof(string), typeof(CardButtonView), new PropertyMetadata("Body"));

        public CardButtonView()
        {
            this.InitializeComponent();
        }

        private void ButtonClick(object sender, RoutedEventArgs e)
        {
            Click?.Invoke(sender, e);
        }

        private void OnSizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (e.NewSize.Height < 51)
            {
                header.FontSize = 14;
                body.FontSize = 18;
            }
            if (e.NewSize.Height < 61)
            {
                header.FontSize = 15;
                body.FontSize = 18;
            }
            else if(e.NewSize.Height < 71)
            {
                header.FontSize = 16;
                body.FontSize = 19;
            }
            else if (e.NewSize.Height < 81)
            {
                header.FontSize = 17;
                body.FontSize = 20;
            }
            else if (e.NewSize.Height < 91)
            {
                header.FontSize = 18;
                body.FontSize = 21;
            }
            else
            {
                header.FontSize = 19;
                body.FontSize = 22;
            }
        }
    }
}
