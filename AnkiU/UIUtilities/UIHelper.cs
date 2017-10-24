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

using AnkiU.Interfaces;
using AnkiU.Models;
using AnkiU.UserControls;
using OxyPlot;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Windows.ApplicationModel.Contacts;
using Windows.Devices.Input;
using Windows.Graphics.Display;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Pickers;
using Windows.Storage.Streams;
using Windows.UI.Core;
using Windows.UI.Popups;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Media.Imaging;

namespace AnkiU.UIUtilities
{
    static class UIHelper
    {
        private static readonly Regex DigitsOnly = new Regex(@"[^\d]", RegexOptions.Compiled);
        private static readonly Regex DigitsAndWhitespaceOnly = new Regex(@"[^\d ]", RegexOptions.Compiled);

        public static readonly char[] ILLEGAL_NAME_CHAR = new char[] { '`', '~', '!', '@', '#', '$', '%', '^', '&', '*',
                                                                        '(', ')', '/', '<', '>', '.', ':', ';', '"', '\'',
                                                                        '[', ']', '{', '}', '=', '-', '+', ',', '|', '\\' };

        private const int numberRangeMin = (int)Windows.System.VirtualKey.Number0;
        private const int numberRangeMax = (int)Windows.System.VirtualKey.Number9;

        private const int numberPadRangeMin = (int)Windows.System.VirtualKey.NumberPad0;
        private const int numberPadRangeMax = (int)Windows.System.VirtualKey.NumberPad9;

        private static AutoCloseContentDialog autoCloseDialog;
        
        public static readonly CoreCursor HandCursor = new CoreCursor(CoreCursorType.Hand, 1);
        public static readonly CoreCursor ArrowCursor = new CoreCursor(CoreCursorType.Arrow, 1);

        public static SolidColorBrush ButtonBackGroundNormal { get; private set; }
                     = Application.Current.Resources["ButtonBackGroundNormal"] as SolidColorBrush;

        public static SolidColorBrush WhiteBrush { get; private set; } = new SolidColorBrush(Windows.UI.Colors.White);
        public static SolidColorBrush BlackBrush { get; private set; } = new SolidColorBrush(Windows.UI.Colors.Black);

        public static SolidColorBrush DeckWithNewOrDueCardsBrush { get; private set; }
                       = Application.Current.Resources["DeckWithNewOrDueCards"] as SolidColorBrush;
        public static SolidColorBrush AppDefaultTileBackgroundBrush { get; private set; }
                        = Application.Current.Resources["AppDefaultTileBackgroundBrush"] as SolidColorBrush;
        public static SolidColorBrush BackgroundWhiteNormal { get; private set; } 
                       = Application.Current.Resources["BackgroundNormal"] as SolidColorBrush;
        public static SolidColorBrush ForeGroundLight { get; private set; }
               = Application.Current.Resources["ForeGroundLight"] as SolidColorBrush;
        public static SolidColorBrush DarkerBrush { get; private set; } 
                       = Application.Current.Resources["DarkerGray"] as SolidColorBrush;        
        public static SolidColorBrush ContentNightModeBrush { get; private set; } 
                       = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 68, 68, 68));
        public static SolidColorBrush Transparent = new SolidColorBrush(Windows.UI.Colors.Transparent);
        public static SolidColorBrush IndioBrush = new SolidColorBrush(Windows.UI.Color.FromArgb(255, 8, 141, 199));

        public static AcrylicBrush CommandBarAcrylicLightBrush { get; private set; }
                     = Application.Current.Resources["CommandBarAcrylicLightBrush"] as AcrylicBrush;
        public static AcrylicBrush CommandBarAcrylicDarkBrush { get; private set; }
                = Application.Current.Resources["CommandBarAcrylicDarkBrush"] as AcrylicBrush;

        public static AcrylicBrush BackgroundAcrylicLightBrush { get; private set; }
              = Application.Current.Resources["DefaultBackgroundAcrylicLightBrush"] as AcrylicBrush;
        public static AcrylicBrush BackgroundAcrylicDarkBrush { get; private set; }
           = Application.Current.Resources["DefaultBackgroundAcrylicDarkBrush"] as AcrylicBrush;

        public static Windows.UI.Color ContentNightModeColor { get { return ContentNightModeBrush.Color; } }
        private static Windows.UI.Color defaultInkColorDay = Windows.UI.Color.FromArgb(255, 11, 96, 181);
        public static Windows.UI.Color DefaultInkColorDay { get { return defaultInkColorDay; } }
        private static Windows.UI.Color defaultInkColorNight = Windows.UI.Color.FromArgb(255, 2, 155, 191);
        public static Windows.UI.Color DefaultInkColorNight { get { return defaultInkColorNight; }  }
        public const double DEFAULT_INK_SIZE = 3;

        private static bool IsNumberKey(int key)
        {
            return (key <= numberRangeMax && key >= numberRangeMin) ||
                           (key <= numberPadRangeMax && key >= numberPadRangeMin);
        }

        public static string StripNonDigit(this string text)
        {
            return DigitsOnly.Replace(text, "");
        }

        public static string StripNonDigitOrWhiteSpace(this string text)
        {
            return DigitsAndWhitespaceOnly.Replace(text, "");
        }

        public static childItem GetChildrenInDataTemplate<childItem>(DependencyObject containter, string templateItemName) where childItem : FrameworkElement
        {
            var children = AllChildren<childItem>(containter);
            var child = children.OfType<childItem>().First(x => x.Name.Equals(templateItemName));
            return child;
        }

        public static Parent GetParent<Parent>(FrameworkElement element) where Parent : FrameworkElement
        {
            var parent = VisualTreeHelper.GetParent(element) as Parent;
            if (parent == null)
                throw new Exception("The parent is not stored in the specified parent type");
            return parent;
        }

        public static List<childItem> AllChildren<childItem>(DependencyObject parent) where childItem : DependencyObject
        {
            var childList = new List<childItem>();
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                var item = child as childItem;
                if (item != null)
                    childList.Add(item);

                childList.AddRange(AllChildren<childItem>(child));
            }
            return childList;
        }

        public static async Task<StorageFile> OpenFilePicker(string tokenName, params string[] fileTypes)
        {
            return await OpenFilePicker(tokenName, PickerLocationId.DocumentsLibrary, fileTypes);
        }

        public static async Task<StorageFile> OpenFilePicker(string tokenName, PickerLocationId location, params string[] fileTypes)
        {
            try
            {
                var filePicker = new FileOpenPicker();
                filePicker.SuggestedStartLocation = location;
                foreach (var type in fileTypes)
                    filePicker.FileTypeFilter.Add(type);
                var filePick = await filePicker.PickSingleFileAsync();
                if (filePick != null)
                {
                    Windows.Storage.AccessCache.StorageApplicationPermissions.
                    FutureAccessList.AddOrReplace(tokenName, filePick);
                    return filePick;
                }
                else
                    return null;
            }
            catch(Exception ex)
            {
                ThrowUnHandleException(ex);
                await UIHelper.ShowMessageDialog("Unable to open the specified file.");
                return null;
            }
        }

        [Conditional("DEBUG")]
        public static void ThrowJavascriptError(int HResult, [CallerMemberName] string functionName = null)
        {
            switch (HResult)
            {
                case unchecked((int)0x80020006):
                    throw new Exception("JavaScript: There is no function called " + functionName);

                case unchecked((int)0x80020101):
                    throw new Exception("JavaScript: A JavaScript error or exception occured while executing the function " + functionName);

                case unchecked((int)0x800a138a):
                    throw new Exception("JavaScript: " + functionName + " is not a function");
                default:
                    throw new Exception("JavaScript: " + functionName + ": Unknown error! " + HResult);
            }
        }

        public static async Task ShowMessageDialog(string content, string title = "")
        {
            MessageDialog dialog = new MessageDialog(content, title);
            await dialog.ShowAsync();
        }

        public static async Task ShowContentDialog(int duration, string content, string title = "")
        {
            if (autoCloseDialog == null)
            {
                autoCloseDialog = new AutoCloseContentDialog();                
                autoCloseDialog.IsPrimaryButtonEnabled = false;
                autoCloseDialog.IsSecondaryButtonEnabled = false;
            }

            await autoCloseDialog.Show(duration, content, title);            
        }

        public static async Task<bool> AskUserConfirmation(string content, string title = "")
        {
            try
            {
                ConfirmDialog diaglog = new ConfirmDialog();
                diaglog.Message = content;
                diaglog.Title = title;
                await diaglog.ShowAsync();
                return diaglog.IsContinue;
            }
            catch(Exception ex)
            {
                ShowDebugException(ex);
                return false;
            }
        }

        public static void ShowDebugException(Exception ex)
        {
            Debug.WriteLine(ex.Message);
            Debug.WriteLine(ex.StackTrace);
        }

        public static string GetDeviceFamily()
        {
            return Windows.System.Profile.AnalyticsInfo.VersionInfo.DeviceFamily;
        }

        public static bool IsMobileDevice()
        {
            return Windows.System.Profile.AnalyticsInfo.VersionInfo.DeviceFamily == "Windows.Mobile";
        }

        public static bool IsHasPhysicalKeyboard()
        {
            KeyboardCapabilities keyboardCapabilities = new Windows.Devices.Input.KeyboardCapabilities();            
            return keyboardCapabilities.KeyboardPresent != 0 ;
        }

        public static bool IsHasPhysicalMouse()
        {
            MouseCapabilities mouseCap = new Windows.Devices.Input.MouseCapabilities();            
            return mouseCap.MousePresent != 0;
        }

        public static bool IsHasPen()
        {
            var pointerDevices = Windows.Devices.Input.PointerDevice.GetPointerDevices();
            foreach(var pointer in pointerDevices)
            {
                if (pointer.PointerDeviceType == PointerDeviceType.Pen)
                    return true;
            }
            return false;
        }

        public static StringBuilder GetDateTimeStringForName()
        {
            StringBuilder name = new StringBuilder(DateTimeOffset.Now.UtcDateTime.ToString());
            name = name.Replace("\\", "_");
            name = name.Replace("/", "_");
            name = name.Replace(":", "_");
            name = name.Replace(".", "_");
            return name;
        }

        public static async Task<bool> CheckValidName(string name, IEnumerable<IName> existing, string errorMessage)
        {
            if (String.IsNullOrWhiteSpace(name))
            {
                await ShowMessageDialog("Name cannot be empty. Please enter a valid name.");
                return false;
            }
            foreach (var ex in existing)
            {
                if (ex.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    await ShowMessageDialog(errorMessage);
                    return false;
                }
            }
            return true;
        }

        public static void ToggleNightLight(bool isNight, Page userControl)
        {            
            if (isNight)
            {
                userControl.Background = Application.Current.Resources["DarkerGray"] as SolidColorBrush;
                userControl.Foreground = Application.Current.Resources["ForeGroundLight"] as SolidColorBrush;
            }
            else
            {
                userControl.Background = Application.Current.Resources["BackgroundNormal"] as SolidColorBrush;
                userControl.Foreground = new SolidColorBrush(Windows.UI.Colors.Black);
            }
        }

        public static void ChangePlotModelToNight(PlotModel ChartModel)
        {
            foreach (var a in ChartModel.Axes)
            {
                a.TextColor = OxyColors.White;
                a.AxislineColor = OxyColors.White;
                a.TicklineColor = OxyColors.White;
                a.MajorGridlineColor = OxyColors.White;
                a.MinorGridlineColor = OxyColors.White;
            }
            ChartModel.TextColor = OxyColors.White;
            ChartModel.TitleColor = OxyColors.White;
            ChartModel.SubtitleColor = OxyColors.White;
            ChartModel.PlotAreaBorderColor = OxyColors.White;

            ChartModel.PlotAreaBackground = OxyColor.FromRgb(ContentNightModeColor.R, ContentNightModeColor.G, ContentNightModeColor.B);            
            ChartModel.Background = OxyColor.FromRgb(ContentNightModeColor.R, ContentNightModeColor.G, ContentNightModeColor.B);
        }

        public static void ChangePlotModelToDay(PlotModel ChartModel)
        {
            foreach (var a in ChartModel.Axes)
            {
                a.TextColor = OxyColors.Black;
                a.AxislineColor = OxyColors.Black;
                a.TicklineColor = OxyColors.Black;
                a.MajorGridlineColor = OxyColors.Black;
                a.MinorGridlineColor = OxyColors.Black;
            }
            ChartModel.TextColor = OxyColors.Black;
            ChartModel.TitleColor = OxyColors.Black;
            ChartModel.SubtitleColor = OxyColors.Black;
            ChartModel.PlotAreaBorderColor = OxyColors.Black;

            ChartModel.PlotAreaBackground = OxyColors.White;            
            ChartModel.Background = OxyColors.White;
        }

        public static async Task<IRandomAccessStream> RenderToRandomAccessStream(this Windows.UI.Xaml.UIElement element)
        {
            RenderTargetBitmap rtb = new RenderTargetBitmap();
            await rtb.RenderAsync(element);

            var pixelBuffer = await rtb.GetPixelsAsync();
            var pixels = pixelBuffer.ToArray();

            // Useful for rendering in the correct DPI
            var displayInformation = DisplayInformation.GetForCurrentView();

            var stream = new InMemoryRandomAccessStream();
            var encoder = await BitmapEncoder.CreateAsync(BitmapEncoder.PngEncoderId, stream);
            encoder.SetPixelData(BitmapPixelFormat.Bgra8,
                                 BitmapAlphaMode.Premultiplied,
                                 (uint)rtb.PixelWidth,
                                 (uint)rtb.PixelHeight,
                                 displayInformation.RawDpiX,
                                 displayInformation.RawDpiY,
                                 pixels);

            await encoder.FlushAsync();
            stream.Seek(0);

            return stream;
        }

        public static bool IsDeskTop()
        {
            return GetDeviceFamily() == "Windows.Desktop";
        }

        public static async Task<StorageFolder> OpenFolderPicker(string token)
        {
            try
            {
                var folderPicker = new Windows.Storage.Pickers.FolderPicker();
                folderPicker.SuggestedStartLocation = Windows.Storage.Pickers.PickerLocationId.DocumentsLibrary;
                folderPicker.FileTypeFilter.Add("*");
                var folder = await folderPicker.PickSingleFolderAsync();
                if (folder != null)
                {
                    Windows.Storage.AccessCache.StorageApplicationPermissions.
                    FutureAccessList.AddOrReplace(token, folder);
                }
                return folder;
            }
            catch(Exception ex)
            {
                ThrowUnHandleException(ex);
                await UIHelper.ShowMessageDialog("Unable to open the specified folder.");
                return null;
            }
        }

        [Conditional("DEBUG")]
        private static void ThrowUnHandleException(Exception ex)
        {
            throw ex;
        }

        public static void AddToGridInFull(Grid mainGrid, FrameworkElement control)
        {
            control.VerticalAlignment = Windows.UI.Xaml.VerticalAlignment.Stretch;
            control.HorizontalAlignment = Windows.UI.Xaml.HorizontalAlignment.Stretch;
            if(mainGrid.ColumnDefinitions.Count > 0)
                Grid.SetColumnSpan(control, mainGrid.ColumnDefinitions.Count);
            if(mainGrid.RowDefinitions.Count > 0)
                Grid.SetRowSpan(control, mainGrid.RowDefinitions.Count);
            Grid.SetRow(control, 0);
            Grid.SetColumn(control, 0);
            mainGrid.Children.Add(control);
        }

        public static void SetStoryBoardTarget(ColorAnimationUsingKeyFrames animation, string targetName)
        {            
            animation.SetValue(Storyboard.TargetNameProperty, targetName);
        }

        public static void SetStoryBoardTarget(DoubleAnimation animation, string targetName)
        {
            animation.SetValue(Storyboard.TargetNameProperty, targetName);
        }

        public static void SetStoryBoardTarget(DoubleAnimationUsingKeyFrames animation, string targetName)
        {
            animation.SetValue(Storyboard.TargetNameProperty, targetName);
        }

        public static async Task LaunchEmailApp(string emailAddress, string messageBody)
        {            
            var message = new Windows.ApplicationModel.Email.EmailMessage();
            message.Body = messageBody;

            var emailRecipient = new Windows.ApplicationModel.Email.EmailRecipient(emailAddress);
            message.To.Add(emailRecipient);            

            await Windows.ApplicationModel.Email.EmailManager.ShowComposeNewEmailAsync(message);
        }

        public static DeckInformation GetDeck(object frameWorkElement)
        {
            var element = frameWorkElement as FrameworkElement;
            if (element == null)
                return null;

            var deck = element.DataContext as DeckInformation;
            if (deck == null)
                return null;

            return deck;
        }

        public static void ChangeToHandCusor()
        {
            Window.Current.CoreWindow.PointerCursor = HandCursor;
        }

        public static void ChangeToArrowCusor()
        {
            Window.Current.CoreWindow.PointerCursor = ArrowCursor;
        }

        public static string GetHexColor(Brush brush)
        {
            var color = (brush as SolidColorBrush).Color;
            string hex = "#" + color.R.ToString("X2") + color.G.ToString("X2") + color.B.ToString("X2");
            return hex;
        }
    }
}
