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

using AnkiU.Views;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text.RegularExpressions;
using Windows.ApplicationModel.DataTransfer;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.System;
using Windows.UI.Core;
using Windows.UI.Text;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Documents;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Media.Imaging;
using Windows.UI.Xaml.Navigation;

namespace AnkiU.UserControls
{
    public sealed partial class RichEditBoxExtend : UserControl
    {
        private static readonly Regex htmlBoldTag = new Regex("(?is)<b>(.*)</b>", RegexOptions.Compiled);
        private static readonly Regex htmlItalicTag = new Regex("(?is)<i>(.*)</i>", RegexOptions.Compiled);
        private static readonly Regex htmlUnderlineTag = new Regex("(?is)<u>(.*)</u>", RegexOptions.Compiled);
        private static readonly Regex htmlDivTag = new Regex("(?is)<div>(.*)</div>", RegexOptions.Compiled);

        private bool isChangeFromRichEditBox = false;

        public RichEditBoxExtend()
        {
            this.InitializeComponent();
        }

        public string RichText
        {
            get { return (string)GetValue(RichTextProperty); }
            set { SetValue(RichTextProperty, value); }
        }

        public bool IsEnableHtmlConverter
        {
            get { return (bool)GetValue(IsEnableHtmlConverterProperty); }
            set { SetValue(IsEnableHtmlConverterProperty, value); }
        }

        // Using a DependencyProperty as the backing store for IsEnableHtmlConverter.  This enables animation, styling, binding, etc...
        public static readonly DependencyProperty IsEnableHtmlConverterProperty =
            DependencyProperty.Register("IsEnableHtmlConverter", typeof(bool), typeof(RichEditBoxExtend), new PropertyMetadata(false));


        public static readonly DependencyProperty RichTextProperty =
            DependencyProperty.Register("RichText", typeof(string), typeof(NoteFieldView), new PropertyMetadata(string.Empty, callback));

        public TextAlignment TextAligment
        {
            get { return (TextAlignment)GetValue(TextAligmentProperty); }
            set { SetValue(TextAligmentProperty, value); }
        }
        
        public static readonly DependencyProperty TextAligmentProperty =
            DependencyProperty.Register("TextAligment", typeof(TextAlignment), typeof(RichEditBox), new PropertyMetadata(TextAlignment.Left));

        public SolidColorBrush TextBoxBackGround
        {
            get { return (SolidColorBrush)GetValue(TextBoxBackGroundProperty); }
            set { SetValue(TextBoxBackGroundProperty, value); }
        }
        
        public static readonly DependencyProperty TextBoxBackGroundProperty =
            DependencyProperty.Register("TextBoxBackGround", typeof(SolidColorBrush), typeof(RichEditBoxExtend), new PropertyMetadata(new SolidColorBrush(Windows.UI.Colors.White)));

        private static void callback(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var reb = d as RichEditBoxExtend;

            if (reb.isChangeFromRichEditBox)
            {
                reb.isChangeFromRichEditBox = false;
                return;
            }

            if (reb.IsEnableHtmlConverter)
            {
                reb.richEditBox.Document.SetText(TextSetOptions.None, (string)e.NewValue);
            }
            {
                reb.richEditBox.Document.SetText(TextSetOptions.FormatRtf, (string)e.NewValue);
            }
        }              

        private void RichEditBoxKeyDownHandler(object sender, KeyRoutedEventArgs e)
        {
            try
            {
                var control = Window.Current.CoreWindow.GetKeyState(VirtualKey.Control);
                if(control.HasFlag(CoreVirtualKeyStates.Down))
                {
                    switch(e.Key)
                    {
                        case VirtualKey.B:
                            BoldSelection(e);
                            break;
                        case VirtualKey.I:
                            ItalicSelection(e);
                            break;
                        case VirtualKey.U:
                            UnderlineSelection(e);
                            break;
                        default:
                            break;
                    }
                }                     
            }
            catch
            {
                Debug.WriteLine("RichEditBoxExtend: Can't paste data");
            }
        }

        private void BoldSelection(KeyRoutedEventArgs e)
        {            
            ITextSelection selectedText = richEditBox.Document.Selection;            
            if (selectedText != null)
            {                                
                ITextCharacterFormat charFormatting = selectedText.CharacterFormat;                
                charFormatting.Bold = FormatEffect.Toggle;
                selectedText.CharacterFormat = charFormatting;
                e.Handled = true;
            }            
        }

        private void ItalicSelection(KeyRoutedEventArgs e)
        {
            ITextSelection selectedText = richEditBox.Document.Selection;
            if (selectedText != null)
            {
                ITextCharacterFormat charFormatting = selectedText.CharacterFormat;
                charFormatting.Italic = FormatEffect.Toggle;
                selectedText.CharacterFormat = charFormatting;
                e.Handled = true;
            }
        }

        private void UnderlineSelection(KeyRoutedEventArgs e)
        {
           ITextSelection selectedText = richEditBox.Document.Selection;
            if (selectedText != null)
            {
               ITextCharacterFormat charFormatting = selectedText.CharacterFormat;
                if (charFormatting.Underline == UnderlineType.None)
                {
                    charFormatting.Underline = UnderlineType.Single;
                }
                else
                {
                    charFormatting.Underline = UnderlineType.None;
                }
                selectedText.CharacterFormat = charFormatting;
                e.Handled = true;
            }
        }

        private async void RichEditBoxPasteHandler(object sender, TextControlPasteEventArgs e)
        {            
            await PasteContentHandler(e);            
        }

        private async System.Threading.Tasks.Task PasteContentHandler(TextControlPasteEventArgs e)
        {            
            var dataPackageView = Clipboard.GetContent();
            if (!dataPackageView.Contains(StandardDataFormats.StorageItems))
                return;

            var pasteContent = await dataPackageView.GetStorageItemsAsync();
            foreach (var content in pasteContent)
            {
                var file = content as StorageFile;
                if (file == null)
                    continue;

                if (file.ContentType.Contains("image"))
                {
                    using (var imageStream = await file.OpenReadAsync())
                    {
                        richEditBox.Document.Selection.InsertImage(50, 50,
                            0, VerticalCharacterAlignment.Baseline, "Image", imageStream);                                                
                    }
                    continue;
                }

                if (file.ContentType.Contains("video") || file.ContentType.Contains("audio"))
                {
                    richEditBox.Document.Selection.SetText(TextSetOptions.None, file.Name);
                    richEditBox.Document.Selection.Move(TextRangeUnit.Word, file.Name.Length);
                }
                
                
            }

            string text;
            richEditBox.Document.GetText(TextGetOptions.None, out text);

            e.Handled = true;
                        
        }

        private void RichEditBoxLostFocusHanlder(object sender, RoutedEventArgs e)
        {
            string text;
            if (IsEnableHtmlConverter)
            {
                richEditBox.Document.GetText(TextGetOptions.FormatRtf, out text);                
            }
            else
            {
                richEditBox.Document.GetText(TextGetOptions.FormatRtf, out text);
                RichText = text;
            }
            isChangeFromRichEditBox = true;
        }
    }
}
