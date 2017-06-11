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
    public sealed partial class AutoCloseContentDialog : ContentDialog
    {
        public AutoCloseContentDialog()
        {
            this.InitializeComponent();
        }

        public async Task Show(int duration, string content, string title = "")
        {            
            this.Title = title;
            message.Text = content;            
            var task = ShowAsync();
            await Task.Delay(duration);
            Hide();
        }
    }
}
