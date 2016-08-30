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

namespace AnkiU.UserControls
{
    public sealed partial class ColorPicker : UserControl
    {
        public delegate void ColorChooseHandler(Brush color);
        public event ColorChooseHandler ColorChoose;

        public ColorPicker()
        {
            this.InitializeComponent();
        }

        public void ShowFlyout(FrameworkElement target, FlyoutPlacementMode placement)
        {
            colorPick.Placement = placement;
            colorPick.ShowAt(target);
        }

        public void HideFlyout()
        {
            colorPick.Hide();
        }

        private void OnColorChoose(object sender, RoutedEventArgs e)
        {
            var button = sender as Button;
            if (sender == null)
                return;

            ColorChoose?.Invoke(button.Background);
        }
    }
}
