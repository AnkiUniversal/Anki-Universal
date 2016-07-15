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
using System.Text;
using System.Text.RegularExpressions;

namespace AnkiU
{
    public static class StringExtensionMethods
    {
        /// <summary>
        /// Get string value of the specified group 
        /// if the match is success.
        /// If not return null.
        /// </summary>
        /// <returns>The matched string or null if not successs</returns>
        public static string GetGroup(this Match match, int index)
        {
            if (match.Groups[index].Success)
                return match.Groups[index].Value;
            return null;
        }

        public static string EscapeCurlyForStringFormat(this string text)
        {
            char[] array = text.ToCharArray();
            StringBuilder sb = new StringBuilder();
            foreach(char c in array)
            {
                if (c.Equals('{'))
                    sb.Append('{');
                if (c.Equals('}'))
                    sb.Append('}');

                sb.Append(c);
            }
            return sb.ToString();
        }
        public static string ReplaceFirst(this string text, string search, string replace)
        {
            int pos = text.IndexOf(search);
            if (pos < 0)
            {
                return text;
            }
            return text.Substring(0, pos) + replace + text.Substring(pos + search.Length);
        }

        /// <summary>
        /// Append a substring from source and the replace string to the StringBuilder object.
        /// This method combines both apeendReplace & appendTail methods of java.
        /// </summary>
        /// <param name="build">A stringBuilder object to append to</param>
        /// <param name="replace">The replacement string</param>
        /// <param name="source">The original string</param>
        /// <param name="index">Append (index - build.Length) characters</param>
        /// /// <returns>True if not reach the end. False if need to stop</returns>
        public static bool AppendAndReplace(this StringBuilder build, string replace, string source, Match match)
        {
            if (build.Length == 0)
                build.Append(source, 0, match.Index);

            int startpos = match.Index + match.Length;
            build.Append(replace);
            if (match.NextMatch().Length != 0)
            {
                build.Append(source, startpos, match.NextMatch().Index - startpos);
            }
            else
            {
                build.Append(source, startpos, source.Length - startpos);
                return false;
            }
            return true;
        }

        public static string UrlDecode(this string url)
        {
            string newUrl;
            //UrlDecode don't full escape in one loop
            //so we need to repeat it.
            while ((newUrl = System.Net.WebUtility.UrlDecode(url)) != url)
                url = newUrl;
            return url;
        }

        public static string UrlEncode(this string url)
        {
            return Uri.EscapeDataString(url);
        }

        public static string PrintArray<T>(this ICollection<T> array)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var s in array)
                sb.Append(s.ToString());
            return sb.ToString();
        }

        public static string PrintArray<T>(this T[] array)
        {
            StringBuilder sb = new StringBuilder();
            foreach (var s in array)
                sb.Append(s.ToString());
            return sb.ToString();
        }
    }
    
}
