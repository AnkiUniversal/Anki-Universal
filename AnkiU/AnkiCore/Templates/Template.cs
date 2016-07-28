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
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AnkiU.AnkiCore.Templates
{
    /**Comment from java ver:
    * This class renders the card content by parsing the card template and replacing all marked sections
    * and tags with their respective data. The data is derived from a context object that is given to
    * the class when constructed which maps tags to the data that they should be replaced with.
    * <p/>
    * The java version: This class makes some assumptions about the valid data types that flow
    * through it and is thus simplified. Namely, the context is assumed to always be a Map<String, String>,
    * and sections are only ever considered to be String objects. Tests have shown that strings are the
    * only data type used, and thus code that handles anything else has been omitted.
    */
    public class Template
    {
        public static readonly Regex TypeRegex = new Regex(@"{{(.*:)*type:", RegexOptions.Compiled);
        public static readonly Regex ClozeRegex = new Regex(@"{{(.*:)*cloze:", RegexOptions.Compiled);
        public static readonly Regex ClozeCountRegex = new Regex(@"{{c(\d+)::", RegexOptions.Compiled);

        //WARNING: We need two {{ or }} to represent a literal { or } in C#
        public const  string clozeReg = "(?s)\\{{\\{{c{0}::(.*?)(::(.*?))?\\}}\\}}";
        private static readonly Regex fHookFieldMod = new Regex("^(.*?)(?:\\((.*)\\))?$", RegexOptions.Compiled); 
        private static readonly Regex clozeSection = new Regex(@"c[qa]:(\d+):(.+)", RegexOptions.Compiled);

        private Regex sectionRegex = null;
        private Regex tagRegex = null;
        private string openTagDelimiter = "{{";
        private string closingTagDelimiter = "}}";
        private string thisTemplate;
        private Dictionary<string, string> thisContext;

        private static string GetOrAttr(Dictionary<string, string> obj, string name)
        {
            return GetOrAttr(obj, name, null);
        }

        private static string GetOrAttr(Dictionary<string, string> obj, string name, string defaultStr)
        {
            if (obj.ContainsKey(name))
            {
                return obj[name];
            }
            else
            {
                return defaultStr;
            }
        }

        public Template(string template, Dictionary<string, string> context)
        {
            thisTemplate = template;
            thisContext = context == null ? new Dictionary<string, string>() : context;
            CompileRegExpressions();
        }

        /// <summary>
        /// Turns a Mustache template into something wonderful
        /// </summary>
        /// <returns></returns>
        public string Render()
        {
            string template = RenderSections(thisTemplate, thisContext);
            return RenderTags(template, thisContext);
        }

        /// <summary>
        /// Compiles our section and tag regular expressions
        /// </summary>
        private void CompileRegExpressions()
        {
            string otag = Regex.Escape(openTagDelimiter);
            string ctag = Regex.Escape(closingTagDelimiter);

            string section = String.Format(Media.locale,
                    "(?s){0}[\\#|^]([^\\}}]*){1}(.+?){0}/\\1{1}", otag, ctag);

            sectionRegex = new Regex(section, RegexOptions.Multiline | RegexOptions.Compiled);

            string tag = String.Format(Media.locale, "{0}(#|=|&|!|>|\\{{)?(.+?)\\1?{1}+", otag, ctag);
            
            tagRegex = new Regex(tag, RegexOptions.Compiled);
        }

        private string RenderSections(string template, Dictionary<string, string> context)
        {
            while (true)
            {
                Match match = sectionRegex.Match(template);
                if (!match.Success)
                {
                    break;
                }

                string section = match.GetGroup(0);
                string section_name = match.GetGroup(1);
                string inner = match.GetGroup(2);
                section_name = section_name.Trim();
                string it;

                // check for cloze
                Match m = clozeSection.Match(section_name);
                if (m.Success)
                {
                    // get full field text
                    string txt = GetOrAttr(context, m.GetGroup(2), null);
                    if (txt == null)
                        it = null;
                    else
                    {
                        Regex pattern = new Regex(String.Format(clozeReg, m.GetGroup(1)), RegexOptions.Compiled);
                        Match mm = pattern.Match(txt);
                        if (mm.Success)
                        {
                            it = mm.GetGroup(1);
                        }
                        else
                        {
                            it = null;
                        }
                    }
                }
                else
                {
                    it = GetOrAttr(context, section_name, null);
                }
                string replacer = "";
                if (!String.IsNullOrEmpty(it))
                {
                    it = Utils.StripHTMLMedia(it).Trim();
                }
                if (!String.IsNullOrEmpty(it))
                {
                    if (section[2] != '^')
                    {
                        replacer = inner;
                    }
                }
                else if (String.IsNullOrEmpty(it) && section[2] == '^')
                {
                    replacer = inner;
                }
                template = template.Replace(section, replacer);
            }
            return template;
        }

        private string RenderTags(string template, Dictionary<string, string> context)
        {
            while (true)
            {
                Match match = tagRegex.Match(template);
                if (!match.Success)
                {
                    break;
                }

                string tag = match.GetGroup(0);
                string tag_type = match.GetGroup(1);
                string tag_name = match.GetGroup(2).Trim();
                string replacement;
                if (tag_type == null)
                {
                    replacement = RenderUnescaped(tag_name, context);
                }
                else if (tag_type.Equals("{"))
                {
                    replacement = RenderTag(tag_name, context);
                }
                else if (tag_type.Equals("!"))
                {
                    replacement = RenderComment();
                }
                else if (tag_type.Equals("="))
                {
                    replacement = RenderDelimiter(tag_name);
                }
                else
                {
                    return "{{invalid template}}";
                }
                template = template.Replace(tag, replacement);
            }
            return template;
        }

        /// <summary>
        /// {{{ functions just like {{ in anki
        /// </summary>
        /// <param name="tag_name"></param>
        /// <param name="context"></param>
        /// <returns></returns>
        private string RenderTag(string tag_name, Dictionary<string, string> context)
        {
            return RenderUnescaped(tag_name, context);
        }

        /// <summary>
        /// Rendering a comment always returns nothing.
        /// </summary>
        /// <returns></returns>
        private string RenderComment()
        {
            return "";
        }

        private string RenderUnescaped(string tag_name, Dictionary<string, string> context)
        {
            string txt = GetOrAttr(context, tag_name);
            if (txt != null)
            {
                // some field names could have colons in them
                // avoid interpreting these as field modifiers
                // better would probably be to put some restrictions on field names
                return txt;
            }

            // field modifiers
            List<string> parts = new List<string>(tag_name.Split(new string[] { ":" }, StringSplitOptions.None));
            string extra = null;
            string[] mods;
            string tag;
            if (parts.Count == 1 || parts[0].Equals(""))
            {
                return String.Format("{{unknown field {0}}}", tag_name);
            }
            else
            {
                mods = new string[parts.Count - 1];
                parts.CopyTo(0, mods, 0, parts.Count - 1);
                tag = parts[parts.Count - 1];
            }

            txt = GetOrAttr(context, tag);

            // Since 'text:' and other mods can affect html on which Anki relies to
            // process clozes, we need to make sure clozes are always
            // treated after all the other mods, regardless of how they're specified
            // in the template, so that {{cloze:text: == {{text:cloze:
            // For type:, we return directly since no other mod than cloze (or other
            // pre-defined mods) can be present and those are treated separately
            Array.Reverse(mods);
            Array.Sort(mods, (string lhs, string rhs) =>
            {
                // This comparator ensures "type:" mods are ordered first in the list. The rest of
                // the list remains in the same order.
                if (lhs.Equals("type"))
                {
                    return 1;
                }
                else
                {
                    return 0;
                }
            });

            for (int i = 0; i < mods.Length; i++)
            {
                // built-in modifiers
                if (mods[i].Equals("text"))
                {
                    // strip html
                    if (!String.IsNullOrEmpty(txt))
                    {
                        txt = Utils.StripHTML(txt);
                    }
                    else
                    {
                        txt = "";
                    }
                }
                else if (mods[i].Equals("type"))
                {
                    // type answer field; convert it to [[type:...]] for the gui code
                    // to process
                    return String.Format(Media.locale, "[[{0}]]", tag_name);
                }
                else if (mods[i].StartsWith("cq-") || mods[i].StartsWith("ca-"))
                {
                    // cloze deletion
                    string[] split = mods[i].Split(new string[] { "-" }, StringSplitOptions.None);
                    mods[i] = split[0];
                    extra = split[1];
                    if (!String.IsNullOrEmpty(txt) && !String.IsNullOrEmpty(extra))
                    {
                        txt = ClozeText(txt, extra, mods[i][1]);
                    }
                    else
                    {
                        txt = "";
                    }
                }
                else
                {
                    // hook-based field modifier
                    Match m = fHookFieldMod.Match(mods[i]);
                    if (m.Success)
                    {
                        mods[i] = m.GetGroup(1);
                        extra = m.GetGroup(2);
                    }

                    txt = (string)Hooks.Hooks.RunFilter("fmod_" + mods[i],
                            txt == null ? "" : txt,
                            extra == null ? "" : extra,
                            context, tag, tag_name);
                    if (txt == null)
                    {
                        return String.Format("{{unknown field {0}}}", tag_name);
                    }
                }
            }
            return txt;
        }

        private static string ClozeText(string txt, string ord, char type)
        {
            Regex pattern = new Regex(String.Format(Media.locale, clozeReg, ord));
            MatchCollection matches = pattern.Matches(txt);
            if (matches == null)
            {
                return "";
            }
            StringBuilder repl = new StringBuilder();
            string str;
            foreach (Match m in matches)
            {
                // replace chosen cloze with type
                if (type == 'q')
                {
                    //WANRING: different with java ver but nearly the same as python ver
                    //Need testing
                    str = m.GetGroup(3);
                    if (!String.IsNullOrEmpty(str))
                    {
                        str = String.Format("<span class=cloze>[{0}]</span>", str);
                        repl.AppendAndReplace(str, txt, m);
                    }
                    else
                    {
                        repl.AppendAndReplace("<span class=cloze>[...]</span>", txt, m);
                    }
                }
                else
                {
                    str = String.Format("<span class=cloze>{0}</span>", m.Groups[1].Value);
                    repl.AppendAndReplace(str, txt, m);
                }
            }
            txt = repl.ToString();
            // and display other clozes normally
            //WARNING: python ver use "\\1" while java ver use "$1"
            return Regex.Replace(txt, String.Format(Media.locale, clozeReg, @"\d+"), "$1");
        }

        /// <summary>
        /// Changes the Mustache delimiter
        /// </summary>
        /// <param name="tagName"></param>
        /// <returns></returns>
        private String RenderDelimiter(string tagName)
        {
            try
            {
                string[] split = tagName.Split(new string[] { " " }, StringSplitOptions.None);
                openTagDelimiter = split[0];
                closingTagDelimiter = split[1];
            }
            catch (IndexOutOfRangeException)
            {
                // invalid
                return null;
            }
            CompileRegExpressions();
            return "";
        }
    }
}
