using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace AnkiU
{
    static class StringExtensionMethods
    {
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
        /// An extension method to work around the lack of appendReplacement function
        /// in C#.
        /// Should be used with AppendTail.
        /// </summary>
        /// <param name="build">A stringBuilder object to append to</param>
        /// <param name="replace">The replacement string</param>
        /// <param name="source">The original string</param>
        /// <param name="index">Append (index - build.Length) characters</param>
        public static void AppendAndReplace(this StringBuilder build, string replace, string source, int index)
        {
            build.Append(source, build.Length, index - build.Length);
            build.Append(replace);
        }

        /// <summary>
        /// Append the StringBuilder object by extracting a subtring from source.
        /// Substring: start = build.Length, end = source.Length - build.Length.
        /// Should be used with AppendAndReplace.
        /// </summary>
        /// <param name="build">A stringBuilder object to append to</param>
        /// <param name="source">The original string</param>
        public static void AppendTail(this StringBuilder build, string source)
        {
            build.Append(source, build.Length, source.Length - build.Length);
        }

        public static string UrlDecode(this string url)
        {
            string newUrl;
            while ((newUrl = Uri.UnescapeDataString(url)) != url)
                url = newUrl;
            return newUrl;
        }

        public static string UrlEncode(this string url)
        {
            string newUrl;
            while ((newUrl = Uri.EscapeDataString(url)) != url)
                url = newUrl;
            return newUrl;
        }
    }
    
}
