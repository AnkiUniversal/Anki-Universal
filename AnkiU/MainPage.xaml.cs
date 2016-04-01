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
using AnkiU.AnkiCore;
using Windows.Storage;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace AnkiU
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {
        public MainPage()
        {
            this.InitializeComponent();
        }

        private void browseButton_Click(object sender, RoutedEventArgs e)
        {
          
        }
    }

    //private async void AcessFolderAndCreateCopy(object sender, RoutedEventArgs e)
    //{
    //    var folderPicker = new Windows.Storage.Pickers.FolderPicker();
    //    folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
    //    folderPicker.FileTypeFilter.Add(".db");
    //    Windows.Storage.StorageFolder folder = await folderPicker.PickSingleFolderAsync();
    //    if (folder != null)
    //    {
    //        // Application now has read/write access to all contents in the picked folder
    //        // (including other sub-folder contents)
    //        Windows.Storage.AccessCache.StorageApplicationPermissions.
    //        FutureAccessList.AddOrReplace("PickedFolderToken", folder);
    //        this.textBlock.Text = "Picked folder: " + folder.Name;
    //    }
    //    else
    //    {
    //        this.textBlock.Text = "Operation cancelled.";
    //    }

    //    StorageFolder localFolder = ApplicationData.Current.LocalFolder;
    //    string pathToFile = localFolder.Path + "\\testDB.db";
    //    DB testDB = new DB(pathToFile);
    //    StorageFile file = await localFolder.GetFileAsync("testDB.db");
    //    await file.CopyAsync(folder, "testDB.db", NameCollisionOption.ReplaceExisting);
    //}

}
