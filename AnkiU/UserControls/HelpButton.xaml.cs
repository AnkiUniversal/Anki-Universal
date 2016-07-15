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
using System.IO;
using System.Linq;
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
    public sealed partial class HelpButton : UserControl
    {
        public string HelpText
        {
            get { return (string)GetValue(HelpTextProperty); }
            set { SetValue(HelpTextProperty, value); }
        }
        
        public static readonly DependencyProperty HelpTextProperty =
            DependencyProperty.Register("HelpText", typeof(string), typeof(HelpButton), new PropertyMetadata(null));


        public FrameworkElement PlaceAt
        {
            get { return (FrameworkElement)GetValue(PlaceAtProperty); }
            set { SetValue(PlaceAtProperty, value); }
        }

        // Using a DependencyProperty as the backing store for PlaceAt.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty PlaceAtProperty =
            DependencyProperty.Register("PlaceAt", typeof(FrameworkElement), typeof(HelpButton), new PropertyMetadata(null));

        public Flyout ParentFlyout
        {
            get { return (Flyout)GetValue(ParentFlyoutProperty); }
            set { SetValue(ParentFlyoutProperty, value); }
        }

        // Using a DependencyProperty as the backing store for ParentFlyout.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty ParentFlyoutProperty =
            DependencyProperty.Register("ParentFlyout", typeof(Flyout), typeof(HelpButton), new PropertyMetadata(null));


        public HelpButton()
        {
            this.InitializeComponent();            
            helpFlyout.Closed += HelpFlyoutClosedHandler;
        }

        private void HelpFlyoutClosedHandler(object sender, object e)
        {
            if (ParentFlyout != null)
                ParentFlyout.ShowAt(PlaceAt);
        }

        private void ButtonClickHanlder(object sender, RoutedEventArgs e)
        {
            textFlyout.Text = HelpText;
            if (PlaceAt != null)                
                helpFlyout.ShowAt(PlaceAt);
            else
                helpFlyout.ShowAt(e.OriginalSource as FrameworkElement);
        }
    }
}
