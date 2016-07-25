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

// The Content Dialog item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace AnkiU.UserControls
{
    public sealed partial class ThreeOptionsDialog : ContentDialog
    {
        public string Message { get { return messageTextBlock.Text; }
                                set { messageTextBlock.Text = value; } }

        public Button LeftButton { get { return leftButton; } }
        public Button MiddleButton { get { return middleButton; } }
        public Button RightButton { get { return rightButton; } }

        public Visibility NotAskAgainVisibility { get { return notAskAgainCheckBox.Visibility; }
                                                  set { notAskAgainCheckBox.Visibility = value; } }

        public bool? ThreeStateChoose { get; set; } = null;

        private bool isClosed = false;

        public ThreeOptionsDialog()
        {
            this.InitializeComponent();
            FullSizeDesired = false;
            IsPrimaryButtonEnabled = false;
            IsSecondaryButtonEnabled = false;
            notAskAgainCheckBox.Visibility = Visibility.Collapsed;
            Opened += ThreeOptionsDialogOpened;
            Closed += ThreeOptionsDialogClosed;
        }

        private void ThreeOptionsDialogOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            isClosed = false;
        }

        private void ThreeOptionsDialogClosed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            isClosed = true;
        }
        
        public async Task WaitForDialogClosed()
        {
            while (!isClosed)
                await Task.Delay(50);
        }

        private void LeftButtonClick(object sender, RoutedEventArgs e)
        {
            ThreeStateChoose = true;
            this.Hide();
        }

        private void MiddleButtonClick(object sender, RoutedEventArgs e)
        {
            ThreeStateChoose = false;
            this.Hide();
        }

        private void RightButtonClick(object sender, RoutedEventArgs e)
        {
            ThreeStateChoose = null;
            this.Hide();
        }

        public bool IsLeftButtonClick()
        {
            if (ThreeStateChoose == true)
                return true;
            return false;
        }

        public bool IsMiddleButtonClick()
        {
            if (ThreeStateChoose == false)
                return true;

            return false;
        }

        public bool IsRightButtonClick()
        {
            if (ThreeStateChoose == null)
                return true;

            return false;
        }

        public bool IsNotAskAgain()
        {
            if (notAskAgainCheckBox.IsChecked == true)
                return true;

            return false;
        }

    }
}
