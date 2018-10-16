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

namespace AnkiU.AnkiCore
{
    /**
    * AnkiU does not support LaTex, this file is written to ensure backward compatibility only.
    */
    public class LaTeX
    {
        public static readonly Regex standardPattern = new Regex(@"(?s)\[latex\](.+?)\[/latex\]",
                                                                    RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static readonly Regex expressionPattern = new Regex(@"(?s)\[\$\](.+?)\[/\$\]",
                                                                    RegexOptions.Compiled | RegexOptions.IgnoreCase);
        public static readonly Regex mathPattern = new Regex(@"(?s)\[\$\$\](.+?)\[/\$\$\]",
                                                                    RegexOptions.Compiled | RegexOptions.IgnoreCase);        
        
        private static readonly Regex startInlinePatternMathJax = new Regex(@"(?s)\[\$\]|\\begin{math}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex endInlinePatternMathJax = new Regex(@"(?s)\[/\$\]|\\end{math}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex mathPatternMathJax = new Regex(@"(?s)\[\$\$\]|\[/\$\$\]|\\begin{displaymath}|\\end{displaymath}", RegexOptions.Compiled | RegexOptions.IgnoreCase);
        private static readonly Regex deleteLatexPattern = new Regex(@"(?s)\\begin{math}|\\end{math}|\\begin{displaymath}|\\end{displaymath}|\$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

        /**
        * Convert HTML with embedded latex tags to image links.
        * NOTE: Unlike the original python version of this method, only two parameters are required
        * in java ver. The omitted parameters are used to generate LaTeX images. java ver does not
        * support the generation of LaTeX media and the provided parameters are sufficient for all
        * other cases.
        * 
        * Anki Universal 1.4.14: We use Mathjax so just replace all Latex with $$
        */
        public static string MungeQA(string html, Collection col)
        {
            try
            {
                string result;
                StringBuilder sb = new StringBuilder();
                MatchCollection matches = standardPattern.Matches(html);
                foreach (Match matcher in matches)
                {
                    result = deleteLatexPattern.Replace(matcher.GetGroup(1), "");
                    sb.AppendAndReplace(@"$$" + result + @"$$", html, matcher);
                }
                if (matches.Count > 0)
                    html = sb.ToString();

                result = mathPatternMathJax.Replace(html, @"$$$");
                result = startInlinePatternMathJax.Replace(result, @"\(");
                result = endInlinePatternMathJax.Replace(result, @"\)");             
                return result;

                //TODO: Delete these after testing
                //StringBuilder sb = new StringBuilder();
                //bool isMatchOne = false;
                //MatchCollection matches = standardPattern.Matches(html);
                //foreach (Match matcher in matches)
                //{
                //    sb.AppendAndReplace(ImgLink(col, matcher.GetGroup(1)), html, matcher);
                //    isMatchOne = true;
                //}
                //if (isMatchOne)
                //    html = sb.ToString();

                //isMatchOne =false;
                //matches = expressionPattern.Matches(html);
                //sb = new StringBuilder();
                //foreach (Match matcher in matches)
                //{
                //    sb.AppendAndReplace(ImgLink(col, "$" + matcher.GetGroup(1) + "$"),
                //                        html, matcher);
                //    isMatchOne = true;
                //}
                //if (isMatchOne)
                //    html = sb.ToString();

                //isMatchOne = false;
                //matches = mathPattern.Matches(html);
                //sb = new StringBuilder();
                //foreach (Match matcher in matches)
                //{
                //    sb.AppendAndReplace(ImgLink(col, "\\begin{displaymath}"
                //                        + matcher.GetGroup(1) + "\\end{displaymath}"),
                //                        html, matcher);
                //    isMatchOne = true;
                //}
                //if (isMatchOne)
                //    return sb.ToString();
                //else
                //    return html;
            }
            catch
            {
                return html;
            }
        }

        /// <summary>
        /// Return an img link for LATEX.
        /// </summary>
        /// <param name="col"></param>
        /// <param name="latex"></param>
        /// <returns></returns>
        private static String ImgLink(Collection col, String latex)
        {
            String txt = LatexFromHtml(col, latex);
            String fname = "latex-" + Utils.Checksum(txt) + ".png";
            return "<img class=latex src=\"" + fname + "\">";
        }

        /// <summary>
        /// Convert entities and fix newlines
        /// </summary>
        /// <param name="col"></param>
        /// <param name="latex"></param>
        /// <returns></returns>
        private static String LatexFromHtml(Collection col, String latex)
        {
            latex = Regex.Replace(latex, "<br( /)?>|<div>", "\n");
            latex = Utils.StripHTML(latex);
            return latex;
        }

        public class LaTeXFilter : Hooks.Hook
        {
            public override object RunFilter(object arg, params object[] args)
            {
                return LaTeX.MungeQA((string)arg, (Collection)args[4]);
            }

            public override void RunHook(params object[] args)
            {
                throw new NotImplementedException();
            }
        }

        public void InstallHook(Hooks.Hooks h)
        {
            h.AddHook("mungeQA", new LaTeXFilter());
        }

    }
}
