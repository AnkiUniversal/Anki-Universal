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
    public sealed partial class FourOptionsDialog : ContentDialog
    {
        public event RoutedEventHandler OpenButtonClicked;

        public string Message
        {
            get { return messageTextBlock.Text; }
            set { messageTextBlock.Text = value; }
        }

        public Button FirstButton { get { return firstButton; } }
        public Button SecondButton { get { return secondButton; } }
        public Button ThirdButton { get { return thirdButton; } }
        public Button FourthButton { get { return fourthButton; } }
        
        public enum ButtonIndex
        {
            first,
            second,
            third,
            fourth
        }

        private ButtonIndex index;
        public ButtonIndex ButtonClicked { get { return index; } }

        public Visibility NotAskAgainVisibility
        {
            get { return notAskAgainCheckBox.Visibility; }
            set { notAskAgainCheckBox.Visibility = value; }
        }

        public bool? ThreeStateChoose { get; set; } = null;

        private bool isClosed = false;

        public FourOptionsDialog()
        {
            this.InitializeComponent();
            FullSizeDesired = false;
            IsPrimaryButtonEnabled = false;
            IsSecondaryButtonEnabled = false;
            notAskAgainCheckBox.Visibility = Visibility.Collapsed;
            Opened += DialogOpened;
            Closed += DialogClosed;
        }

        private void DialogOpened(ContentDialog sender, ContentDialogOpenedEventArgs args)
        {
            isClosed = false;
        }

        private void DialogClosed(ContentDialog sender, ContentDialogClosedEventArgs args)
        {
            isClosed = true;
        }

        public async Task WaitForDialogClosed()
        {
            while (!isClosed)
                await Task.Delay(50);
        }

        private void FirstButtonClick(object sender, RoutedEventArgs e)
        {
            index = ButtonIndex.first;
            this.Hide();
        }

        private void SecondButtonClick(object sender, RoutedEventArgs e)
        {
            index = ButtonIndex.second;
            this.Hide();
        }

        private void ThirdButtonClick(object sender, RoutedEventArgs e)
        {
            index = ButtonIndex.third;
            this.Hide();
        }

        private void FourthButtonClick(object sender, RoutedEventArgs e)
        {
            index = ButtonIndex.fourth;
            this.Hide();
        }

        public bool IsFirstButtonClick()
        {
            if (index == ButtonIndex.first)
                return true;
            return false;
        }

        public bool IsSecondButtonClick()
        {
            if (index == ButtonIndex.second)
                return true;

            return false;
        }

        public bool IsThirdButtonClick()
        {
            if (index == ButtonIndex.third)
                return true;

            return false;
        }

        public bool IsFourthButtonClick()
        {
            if (index == ButtonIndex.fourth)
                return true;

            return false;
        }

        public bool IsNotAskAgain()
        {
            if (notAskAgainCheckBox.IsChecked == true)
                return true;

            return false;
        }

        private void OpenButtonClick(object sender, RoutedEventArgs e)
        {
            OpenButtonClicked?.Invoke(sender, e);
        }
    }
}
