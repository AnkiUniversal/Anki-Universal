using AnkiU.Models;
using AnkiU.ViewModels;
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

// The User Control item template is documented at http://go.microsoft.com/fwlink/?LinkId=234236

namespace AnkiU.Views
{
    public sealed partial class FieldMultiSelect : UserControl
    {
        private FrameworkElement placeToShow;

        public List<string> SelectedFields { get; set; } = new List<string>();

        public event RoutedEventHandler FlyoutClosed;

        public FieldMultiSelect(MultiNoteFieldsSelectViewModel viewModel)
        {
            this.InitializeComponent();
            fieldListView.DataContext = viewModel.Fields;
        }

        private void CheckBoxCheckedHandler(object sender, RoutedEventArgs e)
        {
            var data = (sender as CheckBox).DataContext as NoteField;
            SelectedFields.Add(data.Name);
        }

        private void CheckBoxUncheckedHandler(object sender, RoutedEventArgs e)
        {
            var data = (sender as CheckBox).DataContext as NoteField;
            SelectedFields.Remove(data.Name);
        }

        public void ShowFlyout(FrameworkElement element, FlyoutPlacementMode placement)
        {
            flyout.Placement = placement;
            flyout.ShowAt(element);
            placeToShow = element;
        }

        private void FlyoutClosedEvent(object sender, object e)
        {
            FlyoutClosed?.Invoke(sender, null);
        }
    }
}
