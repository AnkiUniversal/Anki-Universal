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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Foundation;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.Web;

namespace AnkiU.Anki
{
    /// <summary>
    /// As UWP does not allow webview control to load image source directly 
    /// from application local folder, we have to use this class to resolve
    /// image source path in our card.
    /// </summary>
    class CardStreamUriWinRTResolver : IUriToStreamResolver
    {

        public IAsyncOperation<IInputStream> UriToStreamAsync(Uri uri)
        {
            if (uri == null)
            {
                throw new Exception();
            }
            string path = uri.AbsolutePath;
            return getContent(path).AsAsyncOperation();
        }

        private async Task<IInputStream> getContent(string path)
        {
            path = path.Replace("/", "\\");
            path = path.Substring(1, path.Length - 1);
            // HTML, JavaScript, and CSS are within the application package
            if (path.EndsWith(".js") || path.EndsWith(".html") || path.EndsWith(".css"))
            {
                string storageFolderPath = Windows.ApplicationModel.Package.Current.InstalledLocation.Path;
                StorageFile f = await Windows.ApplicationModel.Package.Current.InstalledLocation.GetFileAsync(path);
                IRandomAccessStream stream = await f.OpenAsync(FileAccessMode.Read);
                return stream;
            }
            else // Images are loaded from application data
            {                
                var item = await ApplicationData.Current.LocalFolder.TryGetItemAsync(path);
                if (item == null)                
                    item = await TryGetUnescapeUriPath(path);                

                if (item == null)
                {
                    int nameStartIndex = path.LastIndexOf('\\');
                    path = "collection.media" + path.Substring(nameStartIndex);
                    item = await ApplicationData.Current.LocalFolder.TryGetItemAsync(path);
                    if(item == null)
                        item = await TryGetUnescapeUriPath(path);
                }
                if (item != null)
                {
                    var file = item as StorageFile;
                    IRandomAccessStream stream = await file.OpenAsync(FileAccessMode.Read);
                    return stream;
                }

                throw new Exception("Invalid Source");
            }
        }

        private static async Task<IStorageItem> TryGetUnescapeUriPath(string path)
        {
            var decodePath = Uri.UnescapeDataString(path);
            return await ApplicationData.Current.LocalFolder.TryGetItemAsync(decodePath);            
        }
    }
}
