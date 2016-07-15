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
using System.Text.RegularExpressions;
using Windows.Media.Core;
using Windows.Media.Playback;
using Windows.UI.Xaml.Controls;
using AnkiU.Views;
using Windows.Storage;

namespace AnkiU.AnkiCore
{
    /// <summary>
    /// Name this class as sound as java and python ver
    /// However, this class actually only modifies media tag to html
    /// all the playing control is automatically handle by WebView API
    /// as a result it can play all file types supported by WebView
    /// </summary>
    public static class Sound
    {

        //Pattern used to identify the markers for sound files
        public static Regex soundPattern = new Regex("\\[sound\\:([^\\[\\]]*)\\]", RegexOptions.Compiled);

        //Pattern used to parse URI (according to http://tools.ietf.org/html/rfc3986#page-50)
        private static Regex uriPattern = new Regex("^(([^:/?#]+):)?(//([^/?#]*))?([^?#]*)(\\?([^#]*))?(#(.*))?$", RegexOptions.Compiled);       

        // Whitelist for audio extension, all other files are recognized as video
        private static readonly string[] AUDIO_WHITELIST = { "flac", "mp3", "wav", "wma" };

        /// <summary>
        /// </summary>
        /// <param name="sound">path to the sound file from the card content</param>
        /// <returns>URI to the sound file</returns>
        private static string GetSoundPath(string sound)
        {
            if (HasURIScheme(sound))
            {
                return sound;
            }
            return "\"" + sound.UrlEncode() + "\"";
        }

        private static bool HasURIScheme(string path)
        {
            Match uriMatcher = uriPattern.Match(path.Trim());
            return (uriMatcher.Success) && (uriMatcher.GetGroup(2) != null);
        }

        /// <summary>
        /// Takes content with embedded sound file placeholders and expands them to reference the actual media file
        /// </summary>
        /// <param name="soundDir">the base path of the media files</param>
        /// <param name="content">card content to be rendered that may contain embedded audio</param>
        /// <returns>the same content but in a format that will render working play buttons when audio was embedded</returns>
        public static string ExpandSounds(string content)
        {
            StringBuilder stringBuilder = new StringBuilder();
            string contentLeft = content;

            MatchCollection matches = soundPattern.Matches(content);
            foreach (Match matcher in matches)
            {
                // Get the sound file name
                string sound = matcher.GetGroup(1).Trim();
                var extensionSplit = sound.Split(new char[] { '.' }, StringSplitOptions.RemoveEmptyEntries);
                string fileExtension = extensionSplit[extensionSplit.Length - 1];
                string fileType;
                if (AUDIO_WHITELIST.Contains(fileExtension))
                    fileType = "audio";
                else 
                    fileType = "video";

                // Construct the sound path
                string soundPath = GetSoundPath(sound);

                string soundMarker = matcher.ToString();
                int markerStart = contentLeft.IndexOf(soundMarker);
                stringBuilder.Append(contentLeft.Substring(0, markerStart));
                stringBuilder.Append("<" + fileType + " video controls> \n");

                //Display this line if WebView failed to play the file
                stringBuilder.Append("Not supported file format!");

                stringBuilder.Append("<source src=" + soundPath + "> \n");
                stringBuilder.Append("</" + fileType + ">");
                contentLeft = contentLeft.Substring(markerStart + soundMarker.Length);
            }

            stringBuilder.Append(contentLeft);

            return stringBuilder.ToString();
        }

    }

}
