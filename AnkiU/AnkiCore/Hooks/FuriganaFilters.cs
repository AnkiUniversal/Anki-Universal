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

namespace AnkiU.AnkiCore.Hooks
{
    public class FuriganaFilters
    {
        private static readonly Regex regex = new Regex(" ?([^ >]+?)\\[(.+?)\\]", RegexOptions.Compiled);

        //Java ver comment:
        // Since there is no ruby tag support in Android before 3.0 (SDK version 11), we must use an alternative
        // approach to align the elements. Anki does the same thing in aqt/qt.py for earlier versions of qt.
        // The fallback approach relies on CSS in the file /assets/ruby.css
        //private static final String RUBY = CompatHelper.isHoneycomb() ? "<ruby><rb>$1</rb><rt>$2</rt></ruby>"
        //        : "<span class='legacy_ruby_rb'><span class='legacy_ruby_rt'>$2</span>$1</span>";
        //WARNING: Have no idead if we can do this in UWA or not!
        private static string RUBY = "<ruby><rb>$1</rb><rt>$2</rt></ruby>";

        public void Install(Hooks h)
        {
            h.AddHook("fmod_kanji", new Kanji());
            h.AddHook("fmod_kana", new Kana());
            h.AddHook("fmod_furigana", new Furigana());
        }

        private static string NoSound(Match match, string repl)
        {
            if (match.GetGroup(2).StartsWith("sound:"))
            {
                // return without modification
                return match.GetGroup(0);
            }
            else
            {
                return regex.Replace(match.GetGroup(0), repl);
            }
        }

        public class Kanji : Hook
        {
            public override object RunFilter(object arg, params object[] args)
            {
                string str = Convert.ToString(arg);
                string newStr = AppendAndReplace(str, "$1");
                if (newStr != null)
                    return newStr;
                else
                    return str;
            }

            public override void RunHook(params object[] args)
            {
                throw new NotImplementedException();
            }
        }

        public class Kana : Hook
        {
            public override object RunFilter(object arg, params object[] args)
            {
                string str = Convert.ToString(arg);
                string newStr = AppendAndReplace(str, "$2");
                if (newStr != null)
                    return newStr;
                else
                    return str;
            }

            public override void RunHook(params object[] args)
            {
                throw new NotImplementedException();
            }
        }

        private static string AppendAndReplace(string source, string replace)
        {
            MatchCollection match = regex.Matches(source);
            if (match.Count != 0)
            {
                StringBuilder sb = new StringBuilder();
                foreach (Match m in match)
                {
                    sb.AppendAndReplace(NoSound(m, replace), source, m);
                }
                return sb.ToString();
            }
            return null;
        }

        public class Furigana : Hook
        {
            public override object RunFilter(object arg, params object[] args)
            {
                string str = Convert.ToString(arg);
                string newStr = AppendAndReplace(str, RUBY);
                if (newStr != null)
                    return newStr;
                else
                    return str;
            }

            public override void RunHook(params object[] args)
            {
                throw new NotImplementedException();
            }
        }

    }
}
