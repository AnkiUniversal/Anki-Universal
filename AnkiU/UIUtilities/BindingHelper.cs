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

using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Data;

namespace AnkiU.UIUtilities
{
    public class BindingHelper
    {
        public static readonly DependencyProperty VisibilityBindingPathProperty =
        DependencyProperty.RegisterAttached("VisibilityBindingPath", typeof(string), typeof(BindingHelper),
                                            new PropertyMetadata(null, VisibilityBindingPathPropertyChanged));

        public static string GetVisibilityBindingPath(DependencyObject obj)
        {
            return (string)obj.GetValue(VisibilityBindingPathProperty);
        }

        public static void SetVisibilityBindingPath(DependencyObject obj, string value)
        {
            obj.SetValue(VisibilityBindingPathProperty, value);
        }

        private static void VisibilityBindingPathPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            var propertyPath = e.NewValue as string;

            if (propertyPath != null)
            {
                var property = ListViewItem.VisibilityProperty;

                BindingOperations.SetBinding(
                    obj,
                    property,
                    new Binding { Path = new PropertyPath(propertyPath) });
            }
        }
    }
}
