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
using Windows.Data.Json;


namespace AnkiU.AnkiCore
{
    public class Finder
    {
        private static readonly Regex fPropPattern = new Regex("(^.+?)(<=|>=|!=|=|<|>)(.+?$)", RegexOptions.Compiled);
        private static readonly Regex fNidsPattern = new Regex("[^0-9,]", RegexOptions.Compiled);
        private static readonly Regex fMidPattern = new Regex("[^0-9]", RegexOptions.Compiled);

        private Collection collection;

        public Finder(Collection col)
        {
            collection = col;
        }

        /// <summary>
        /// NOTE: The python version of findCards can accept a boolean, a string, or no value for the order parameter. The 
        /// type of order also determines which order() method is used.To maintain type safety, we expose the three valid
        /// options here and safely type-cast accordingly at run-time.
        /// </summary>
        /// <param name="query"></param>
        /// <param name="order"></param>
        /// <returns></returns>
        public List<long> FindCards(string query, string order)
        {
            return FindCardsObject(query, order);
        }

        public List<long> FindCards(string query, bool order = false)
        {
            return FindCardsObject(query, order);
        }

        private List<long> FindCardsObject(string query, object orderObject)
        {
            string[] tokens = Tokenize(query);
            KeyValuePair<string, string[]> res1 = Where(tokens);
            string preds = res1.Key;
            string[] args = res1.Value;
            List<long> res = new List<long>();
            if (preds == null)
            {
                return res;
            }
            KeyValuePair<string, bool> res2 = orderObject is bool ?
                                                Order((bool)orderObject) : Order((string)orderObject);
            string order = res2.Key;
            bool rev = res2.Value;
            string sql = Query(preds, order);
            try
            {
                var list = collection.Database.QueryColumn<CardIdOnlyTable>(sql, args);
                foreach(CardIdOnlyTable c in list)
                {
                    res.Add(c.Id);
                }
            }
            catch (SQLite.Net.SQLiteException)
            {
                // invalid grouping
                return new List<long>();
            }
            if (rev)
            {
                res.Reverse();
            }
            return res;
        }

        public List<long> FindNotes(string query)
        {
            String[] tokens = Tokenize(query);
            KeyValuePair<string, string[]> res1 = Where(tokens);
            string preds = res1.Key;
            string[] args = res1.Value;
            List<long> res = new List<long>();
            if (preds == null)
            {
                return res;
            }
            if (preds.Equals(""))
            {
                preds = "1";
            }
            else
            {
                preds = "(" + preds + ")";
            }
            string sql = "select distinct(n.id) from cards c, notes n where c.nid=n.id and " + preds;
            try
            {
                var list = collection.Database.QueryColumn<NoteTable>(sql, args);
                res.AddRange(from s in list select s.Id);
            }
            catch (SQLite.Net.SQLiteException)
            {
                // invalid grouping
                return new List<long>();
            }
            return res;
        }

        public string[] Tokenize(string query)
        {
            char? inQuote = null;
            List<string> tokens = new List<string>();
            StringBuilder token = new StringBuilder();
            for (int i = 0; i < query.Length; ++i)
            {
                // quoted text
                char c = query[i];
                if (c == '\'' || c == '"')
                {
                    if (inQuote != null)
                    {
                        if (c == inQuote)
                        {
                            inQuote = null;
                        }
                        else
                        {
                            token.Append(c);
                        }
                    }
                    else if (token.Length != 0)
                    {
                        // quotes are allowed to start directly after a :
                        if (token[token.Length - 1] == ':')
                        {
                            inQuote = c;
                        }
                        else
                        {
                            token.Append(c);
                        }
                    }
                    else
                    {
                        inQuote = c;
                    }
                    // separator
                }
                else if (c == ' ')
                {
                    if (inQuote != null)
                    {
                        token.Append(c);
                    }
                    else if (token.Length != 0)
                    {
                        // space marks token finished
                        tokens.Add(token.ToString());
                        token.Clear();
                    }
                    // nesting
                }
                else if (c == '(' || c == ')')
                {
                    if (inQuote != null)
                    {
                        token.Append(c);
                    }
                    else
                    {
                        if (c == ')' && token.Length != 0)
                        {
                            tokens.Add(token.ToString());
                            token.Clear();
                        }
                        tokens.Add(c.ToString());
                    }
                    // negation
                }
                else if (c == '-')
                {
                    if (token.Length != 0)
                    {
                        token.Append(c);
                    }
                    else if (tokens.Count == 0 || !tokens[tokens.Count - 1].Equals("-"))
                    {
                        tokens.Add("-");
                    }
                    // normal character
                }
                else
                {
                    token.Append(c);
                }
            }
            // if we finished in a token, add it
            if (token.Length != 0)
            {
                tokens.Add(token.ToString());
            }
            return tokens.ToArray();
        }

        /// <summary>
        /// NOTE: In the python code, _order() follows a code path based on:
        /// -Empty order string(no order)
        /// -order = False(no order)
        /// -Non - empty order string(custom order)
        /// -order = True(built -in order)
        /// The python code combines all code paths in one function.In Java and C#, we must overload the method
        /// in order to consume either a String(no order, custom order) or a Boolean(no order, built -in order).
        /// </summary>
        /// <param name="order"></param>
        /// <returns></returns>
        private KeyValuePair<string, bool> Order(string order)
        {
            if (String.IsNullOrEmpty(order))
            {
                return Order(false);
            }
            else
            {
                // custom order string provided
                return new KeyValuePair<string, bool>(" order by " + order, false);
            }
        }

        private KeyValuePair<string, bool> Order(bool order)
        {
            if (!order)
            {
                return new KeyValuePair<string, bool>("", false);
            }
            // use deck default
            String type = collection.Conf.GetNamedString("sortType");
            String sort = null;
            if (type.StartsWith("note"))
            {
                if (type.StartsWith("noteCrt"))
                {
                    sort = "n.id, c.ord";
                }
                else if (type.StartsWith("noteMod"))
                {
                    sort = "n.mod, c.ord";
                }
                else if (type.StartsWith("noteFld"))
                {
                    sort = "n.sfld COLLATE NOCASE, c.ord";
                }
            }
            else if (type.StartsWith("card"))
            {
                if (type.StartsWith("cardMod"))
                {
                    sort = "c.mod";
                }
                else if (type.StartsWith("cardReps"))
                {
                    sort = "c.reps";
                }
                else if (type.StartsWith("cardDue"))
                {
                    sort = "c.type, c.due";
                }
                else if (type.StartsWith("cardEase"))
                {
                    sort = "c.factor";
                }
                else if (type.StartsWith("cardLapses"))
                {
                    sort = "c.lapses";
                }
                else if (type.StartsWith("cardIvl"))
                {
                    sort = "c.ivl";
                }
            }
            if (sort == null)
            {
                // deck has invalid sort order; revert to noteCrt
                sort = "n.id, c.ord";
            }
            bool sortBackwards = collection.Conf.GetNamedBoolean("sortBackwards");
            return new KeyValuePair<string, bool>(" ORDER BY " + sort, sortBackwards);
        }

        private string Query(string preds, string order)
        {
            // can we skip the note table?
            string sql;
            if (!preds.Contains("n.") && !order.Contains("n."))
            {
                sql = "select c.id from cards c where ";
            }
            else
            {
                sql = "select c.id from cards c, notes n where c.nid=n.id and ";
            }
            // combine with preds
            if (!String.IsNullOrEmpty(preds))
            {
                sql += "(" + preds + ")";
            }
            else
            {
                sql += "1";
            }
            // order
            if (!String.IsNullOrEmpty(order))
            {
                sql += " " + order;
            }
            return sql;
        }

        ///LibAnki creates a dictionary and operates on it with an inner function inside _where().
        ///Java ver combines the two in this class instead.
        public class SearchState
        {
            public bool IsNot { get; set; }
            public bool IsOr { get; set; }
            public bool IsNeedJoin { get; set; }
            private string q = "";
            public string Q { get { return q; } set { q = value; } }
            public bool IsBad { get; set; }

            public void Add(string txt)
            {
                Add(txt, true);
            }

            public void Add(String txt, bool wrap)
            {
                // failed command?
                if (String.IsNullOrEmpty(txt))
                {
                    // if it was to be negated then we can just ignore it
                    if (IsNot)
                    {
                        IsNot = false;
                        return;
                    }
                    else
                    {
                        IsBad = true;
                        return;
                    }
                }
                else if (txt.Equals("skip"))
                {
                    return;
                }
                // do we need a conjunction?
                if (IsNeedJoin)
                {
                    if (IsOr)
                    {
                        q += " or ";
                        IsOr = false;
                    }
                    else
                    {
                        q += " and ";
                    }
                }
                if (IsNot)
                {
                    q += " not ";
                    IsNot = false;
                }
                if (wrap)
                {
                    txt = "(" + txt + ")";
                }
                q += txt;
                IsNeedJoin = true;
            }
        }

        public KeyValuePair<string, string[]> Where(string[] tokens)
        {
            // state and query
            SearchState s = new SearchState();
            List<string> args = new List<string>();
            foreach (string token in tokens)
            {
                if (s.IsBad)
                {
                    return new KeyValuePair<string, string[]>(null, null);
                }
                // special tokens
                if (token.Equals("-"))
                {
                    s.IsNot = true;
                }
                else if (token.Equals("or", StringComparison.OrdinalIgnoreCase))
                {
                    s.IsOr = true;
                }
                else if (token.Equals("("))
                {
                    s.Add(token, false);
                    s.IsNeedJoin = false;
                }
                else if (token.Equals(")"))
                {
                    s.Q += ")";
                    // commands
                }
                else if (token.Contains(":"))
                {
                    string[] spl = token.Split(new char[] {':' }, 2);
                    string cmd = spl[0].ToLower();
                    string val = spl[1];

                    if (cmd.Equals("added"))
                    {
                        s.Add(FindAdded(val)); //Continue
                    }
                    else if (cmd.Equals("card"))
                    {
                        s.Add(FindTemplate(val));
                    }
                    else if (cmd.Equals("deck"))
                    {
                        s.Add(FindDeck(val));
                    }
                    else if (cmd.Equals("mid"))
                    {
                        s.Add(FindMid(val));
                    }
                    else if (cmd.Equals("nid"))
                    {
                        s.Add(FindNids(val));
                    }
                    else if (cmd.Equals("cid"))
                    {
                        s.Add(FindCids(val));
                    }
                    else if (cmd.Equals("note"))
                    {
                        s.Add(FindModel(val));
                    }
                    else if (cmd.Equals("prop"))
                    {
                        s.Add(FindProp(val));
                    }
                    else if (cmd.Equals("rated"))
                    {
                        s.Add(FindRated(val));
                    }
                    else if (cmd.Equals("tag"))
                    {
                        s.Add(FindTag(val, args));
                    }
                    else if (cmd.Equals("dupe"))
                    {
                        s.Add(FindDupes(val));
                    }
                    else if (cmd.Equals("is"))
                    {
                        s.Add(FindCardState(val));
                    }
                    else
                    {
                        s.Add(FindField(cmd, val));
                    }
                    // normal text search
                }
                else
                {
                    s.Add(FindText(token, args));
                }
            }
            if (s.IsBad)
            {
                return new KeyValuePair<string, string[]>(null, null);
            }
            return new KeyValuePair<string, string[]>(s.Q, args.ToArray());
        }

        private string FindAdded(string value)
        {
            int days;
            try
            {
                bool isOk = int.TryParse(value, out days);
                if (!isOk)
                    return null;
            }
            catch (FormatException)
            {
                return null;
            }
            long cutoff = (collection.Sched.DayCutoff - 86400 * days) * 1000;
            return "c.id > " + cutoff;
        }

        private string FindTemplate(string value)
        {
            // were we given an ordinal number?
            int? num = null;
            try
            {
                num = int.Parse(value) - 1;
            }
            catch (FormatException)
            {
                num = null;
            }
            if (num != null)
            {
                return "c.ord = " + num;
            }
            // search for template names
            List<string> lims = new List<string>();
            foreach (JsonObject m in collection.Models.All())
            {
                JsonArray tmpls = m.GetNamedArray("tmpls");
                for (uint ti = 0; ti < tmpls.Count; ++ti)
                {
                    JsonObject t = tmpls.GetObjectAt(ti);
                    if (t.GetNamedString("name").Equals(value, StringComparison.OrdinalIgnoreCase))
                    {
                        if (m.GetNamedNumber("type") == (double)ModelType.CLOZE)
                        {
                            // if the user has asked for a cloze card, we want
                            // to give all ordinals, so we just limit to the
                            // model instead
                            lims.Add("(n.mid = " + (long)m.GetNamedNumber("id") + ")");
                        }
                        else
                        {
                            lims.Add("(n.mid = " + (long)m.GetNamedNumber("id") + " and c.ord = " +
                                    (int)t.GetNamedNumber("ord") + ")");
                        }
                    }
                }
            } 
            return String.Join(" or ", lims.ToArray());
        }

        public string FindDeck(string value)
        {
            // if searching for all decks, skip
            if (value.Equals("*"))
            {
                return "skip";
                // deck types
            }
            else if (value.Equals("filtered"))
            {
                return "c.odid";
            }
            List<long> ids = null;
            // current deck?
            if (value.Equals("current", StringComparison.OrdinalIgnoreCase))
            {
                ids = GetChildDeckIds((long)collection.Deck.Current().GetNamedNumber("id"));
            }
            else if (!value.Contains("*"))
            {
                // single deck
                ids = GetChildDeckIds(collection.Deck.AddOrResuedDeck(value, false));
            }
            else
            {
                // wildcard
                ids = new List<long>();
                value = value.Replace("*", ".*");
                value = value.Replace("+", "\\+");
                foreach (JsonObject d in collection.Deck.All())
                {
                    
                    if (Regex.IsMatch(d.GetNamedString("name"), "(?i)" + value))
                    {
                        foreach (long id in GetChildDeckIds((long)d.GetNamedNumber("id")))
                        {
                            if (!ids.Contains(id))
                            {
                                ids.Add(id);
                            }
                        }
                    }
                }
            }
            if (ids == null || ids.Count == 0)
            {
                return null;
            }
            string sids = Utils.Ids2str(ids.ToArray());
            return "c.did in " + sids + " or c.odid in " + sids;
        }

        private List<long> GetChildDeckIds(long? deckId)
        {
            if (deckId == null)
            {
                return null;
            }
            long lDid = (long)deckId;
            Dictionary<string, long> children = collection.Deck.Children(lDid);
            List<long> res = new List<long>();
            res.Add(lDid);
            res.AddRange(children.Values);
            return res;
        }

        private string FindMid(string value)
        {
            if (fMidPattern.IsMatch(value))
            {
                return null;
            }
            return "n.mid = " + value;
        }

        private string FindNids(string value)
        {
            if (fNidsPattern.IsMatch(value))
            {
                return null;
            }
            return "n.id in (" + value + ")";
        }

        private string FindCids(string value)
        {
            if (fNidsPattern.IsMatch(value))
            {
                return null;
            }
            return "c.id in (" + value + ")";
        }

        private string FindModel(string value)
        {
            LinkedList<long> ids = new LinkedList<long>();
            foreach (JsonObject m in collection.Models.All())
            {
                if (m.GetNamedString("name").Equals(value, StringComparison.OrdinalIgnoreCase))
                {
                    ids.AddLast((long)m.GetNamedNumber("id"));
                }
            }
            return "n.mid in " + Utils.Ids2str(ids.ToArray());
        }

        private string FindProp(string value)
        {
            // extract
            //WANRING: Not clear if _val will only has
            //one match pattern or not.
            Match match = fPropPattern.Match(value);
            if (!match.Success)
            {
                return null;
            }
            string prop = match.GetGroup(1).ToLower();
            string cmp = match.GetGroup(2);
            string sval = match.GetGroup(3);
            int val;
            // is val valid?
            try
            {
                if (prop.Equals("ease"))
                {
                    // LibAnki does this below, but we do it here to avoid keeping a separate float value.
                    val = (int)(double.Parse(sval) * 1000);
                }
                else
                {
                    val = int.Parse(sval);
                }
            }
            catch (FormatException)
            {
                return null;
            }
            // is prop valid?
            if (!(new string[] { "due", "ivl", "reps", "lapses", "ease" }.Contains(prop)))
            {
                return null;
            }
            // query
            string q = "";
            if (prop.Equals("due"))
            {
                val += collection.Sched.Today;
                // only valid for review/daily learning
                q = "(c.queue in (2,3)) and ";
            }
            else if (prop.Equals("ease"))
            {
                prop = "factor";
                // already done: val = int(val*1000)
            }
            q += "(" + prop + " " + cmp + " " + val + ")";
            return q;
        }

        private string FindRated(string val)
        {
            // days(:optional_ease)
            string[] r = val.Split(new string[] { ":" }, StringSplitOptions.None);
            int days;
            try
            {
                days = int.Parse(r[0]);
            }
            catch (FormatException)
            {
                return null;
            }
            days = Math.Min(days, 31);
            // ease
            string ease = "";
            if (r.Length > 1)
            {
                if (!(new string[] { "1", "2", "3", "4" }.Contains(r[1])))
                {
                    return null;
                }
                ease = "and ease=" + r[1];
            }
            long cutoff = (collection.Sched.DayCutoff - 86400 * days) * 1000;
            return "c.id in (select cid from revlog where id>" + cutoff + " " + ease + ")";
        }

        private string FindTag(string val, List<string> args)
        {
            if (val.Equals("none"))
            {
                return "n.tags = \"\"";
            }
            val = val.Replace("*", "%");
            if (!val.StartsWith("%"))
            {
                val = "% " + val;
            }
            if (!val.EndsWith("%"))
            {
                val += " %";
            }
            args.Add(val);
            return "n.tags like ?";
        }

        private string FindDupes(string val)
        {
            // caller must call stripHTMLMedia on passed val
            String[] split = val.Split( new char[] { ',' }, 1);
            if (split.Length != 2)
            {
                return null;
            }
            string mid = split[0];
            val = split[1];
            string csum = (Utils.FieldChecksum(val)).ToString();
            List<long> nids = new List<long>();
            
            var list = collection.Database.QueryColumn<NoteTable>(
                    "select id, flds from notes where mid=? and csum=?",
                    new string[] { mid, csum });
            long nid = list[0].Id;
            string flds = list[0].Fields;
            if (Utils.StripHTMLMedia(Utils.SplitFields(flds)[0]) == val)
            {
                nids.Add(nid);
            }
            return "n.id in " + Utils.Ids2str(nids.ToArray());
        }

        private string FindCardState(string val)
        {
            int n;
            if (val.Equals("review") || val.Equals("new") || val.Equals("learn"))
            {
                if (val.Equals("review"))
                {
                    n = 2;
                }
                else if (val.Equals("new"))
                {
                    n = 0;
                }
                else
                {
                    return "queue IN (1, 3)";
                }
                return "type = " + n;
            }
            else if (val.Equals("suspended"))
            {
                return "c.queue = -1";
            }
            else if (val.Equals("buried"))
            {
                return "c.queue = -2";
            }
            else if (val.Equals("due"))
            {
                return "(c.queue in (2,3) and c.due <= " + collection.Sched.Today +
                        ") or (c.queue = 1 and c.due <= " + collection.Sched.DayCutoff + ")";
            }
            else
            {
                return null;
            }
        }

        private string FindField(string field, string val)
        {

            //We need two expressions to query the cards: One that will use REGEX syntax and another
            //that should use SQLITE LIKE clause syntax.
            string sqlVal = val
                    .Replace("%", "\\%") // For SQLITE, we escape all % signs
                    .Replace("*", "%"); // And then convert the * into non-escaped % signs


            //The following three lines make sure that only _ and * are valid wildcards.
            string regexVal = Regex.Escape(sqlVal).Replace("\\_", ".").Replace("%", ".*");

            //For the pattern, we use the javaVal expression that uses REGEX syntax
            //Regex pattern = new Regex("(?s)\\Q" + regexVal + "\\E", RegexOptions.Compiled | RegexOptions.IgnoreCase);
            Regex pattern = new Regex("(?s)^" + regexVal + "$", RegexOptions.Compiled | RegexOptions.IgnoreCase);

            // find models that have that field
            Dictionary<long, object[]> mods = new Dictionary<long, object[]>();
            foreach (JsonObject m in collection.Models.All())
            {
                JsonArray flds = m.GetNamedArray("flds");
                for (uint fi = 0; fi < flds.Count; ++fi)
                {
                    JsonObject f = flds.GetObjectAt(fi);
                    if (f.GetNamedString("name").Equals(field, StringComparison.OrdinalIgnoreCase))
                    {
                        mods.Add((long)m.GetNamedNumber("id"), new object[] { m, f.GetNamedNumber("ord") });
                    }
                }
            }
            if (mods.Count == 0)
            {
                // nothing has that field
                return null;
            }

            LinkedList<long> nids = new LinkedList<long>();
            // Here we use the sqlVal expression, that is required for LIKE syntax in sqllite.
            // There is no problem with special characters, because only % and _ are special
            // characters in this syntax.
            var list = collection.Database.QueryColumn<NoteTable>(
                            "select id, mid, flds from notes where mid in " +
                            Utils.Ids2str(mods.Keys.ToArray()) +
                            " and flds like ? escape '\\'", new string[] { "%" + sqlVal + "%" });

            foreach (NoteTable n in list)
            {
                string[] flds = Utils.SplitFields(n.Fields);
                int ord = Convert.ToInt32(mods[n.Mid][1]);
                string strg = flds[ord];
                if (pattern.IsMatch(strg))
                {
                    nids.AddLast(n.Id);
                }
            }
            if (nids.Count == 0)
            {
                return "0";
            }
            return "n.id in " + Utils.Ids2str(nids.ToArray());
        }

        private string FindText(string val, List<string> args)
        {
            val = val.Replace("*", "%");
            args.Add("%" + val + "%");
            args.Add("%" + val + "%");
            return "(n.sfld like ? escape '\\' or n.flds like ? escape '\\')";
        }

        public static int FindReplace(Collection collection, List<long> nids, string src, string dst)
        {
            return FindReplace(collection, nids, src, dst, false, null, true);
        }

        public static int FindReplace(Collection collection, List<long> nids, string src, string dst, bool regex)
        {
            return FindReplace(collection, nids, src, dst, regex, null, true);
        }

        public static int FindReplace(Collection collection, List<long> nids, string src, string dst, string field)
        {
            return FindReplace(collection, nids, src, dst, false, field, true);
        }

        public static int FindReplace(Collection collection, List<long> nids, string src, string dst, bool isRegex, string field, bool fold)
        {
            Dictionary<long, int> mmap = new Dictionary<long, int>();
            if (field != null)
            {
                foreach (JsonObject m in collection.Models.All())
                {
                    JsonArray flds = m.GetNamedArray("flds");
                    for (uint fi = 0; fi < flds.Count; ++fi)
                    {
                        JsonObject f = flds.GetObjectAt(fi);
                        if (f.GetNamedString("name").Equals(field))
                        {
                            mmap.Add((long)m.GetNamedNumber("id"), (int)f.GetNamedNumber("ord"));
                        }
                    }
                }
                if (mmap.Count == 0)
                {
                    return 0;
                }
            }
            // find and gather replacements
            if (!isRegex)
            {
                src = Regex.Escape(src);
            }
            if (fold)
            {
                src = "(?i)" + src;
            }
            Regex regex = new Regex(src, RegexOptions.Compiled);

            List<object[]> d = new List<object[]>();
            string snids = Utils.Ids2str(nids);
            nids = new List<long>();
            
            var list = collection.Database.QueryColumn<NoteTable>( "select id, mid, flds from notes where id in " + snids);
            foreach (NoteTable n in list)
            {
                string flds = n.Fields;
                string origFlds = flds;
                // does it match?
                string[] sflds = Utils.SplitFields(flds);
                if (field != null)
                {
                    long mid = n.Mid;
                    if (!mmap.ContainsKey(mid))
                    {
                        // note doesn't have that field
                        continue;
                    }
                    int ord = mmap[mid];
                    sflds[ord] = regex.Replace(sflds[ord], dst);
                }
                else
                {
                    for (int i = 0; i < sflds.Length; ++i)
                    {
                        sflds[i] = regex.Replace(sflds[i], dst);
                    }
                }
                flds = Utils.JoinFields(sflds);
                if (!flds.Equals(origFlds))
                {
                    long nid = n.Id;
                    nids.Add(nid);
                    d.Add(new object[] { flds, DateTimeOffset.Now.ToUnixTimeSeconds(), collection.Usn, nid }); // order based on query below
                }
            }
        
            if (d.Count == 0)
            {
                return 0;
            }
            // replace
            collection.Database.ExecuteMany("update notes set flds=?,mod=?,usn=? where id=?", d);
            long[] pnids = nids.ToArray();
            collection.UpdateFieldCache(pnids);
            collection.GenCards(pnids);
            return d.Count;
        }

        public List<string> FieldNames(Collection collection, bool downcase = true)
        {
            HashSet<string> fields = new HashSet<string>();
            List<string> names = new List<string>();
            foreach (JsonObject m in collection.Models.All())
            {
                JsonArray flds = m.GetNamedArray("flds");
                for (uint fi = 0; fi < flds.Count; ++fi)
                {
                    JsonObject f = flds.GetObjectAt(fi);
                    if (!fields.Contains(f.GetNamedString("name").ToLower()))
                    {
                        names.Add(f.GetNamedString("name"));
                        fields.Add(f.GetNamedString("name").ToLower());
                    }
                }
            }
            if (downcase)
            {
                return new List<string>(fields);
            }
            return names;
        }

        public static int OrdForMid(Collection collection, Dictionary<long, int> fields, long mid, string fieldName)
        {
            if (!fields.ContainsKey(mid))
            {
                JsonObject model = collection.Models.Get(mid);
                JsonArray flds = model.GetNamedArray("flds");
                for (uint c = 0; c < flds.Count; c++)
                {
                    JsonObject f = flds.GetObjectAt(c);
                    if (f.GetNamedString("name").Equals(fieldName, StringComparison.OrdinalIgnoreCase))
                    {
                        fields.Add(mid, (int)c);
                        break;
                    }
                }
            }
            return fields[mid];
        }

        public static List<KeyValuePair<string, List<long>>> FindDupes(Collection collection, string fieldName, string search = "")
        {
            // limit search to notes with applicable field name
            if (!String.IsNullOrEmpty(search))
            {
                search = "(" + search + ") ";
            }
            search += "'" + fieldName + ":*'";
            // go through notes
            var vals = new Dictionary<string, List<long>>();
            var dupes = new List<KeyValuePair<string, List<long>>>();
            Dictionary<long, int> fields = new Dictionary<long, int>();
            var list = collection.Database.QueryColumn<NoteTable>(
                    "select id, mid, flds from notes where id in " + Utils.Ids2str(collection.FindNotes(search)));
            foreach (NoteTable n in list)
            {
                long nid = n.Id;
                long mid = n.Mid;
                string[] flds = Utils.SplitFields(n.Fields);
                int? ord = OrdForMid(collection, fields, mid, fieldName);
                //TODO: this condition will never be reached
                //because OrdForMid cannot return null
                if (ord == null)
                {
                    continue;
                }
                string val = flds[fields[mid]];
                val = Utils.StripHTMLMedia(val);
                // empty does not count as duplicate
                if (String.IsNullOrEmpty(val))
                {
                    continue;
                }
                if (!vals.ContainsKey(val))
                {
                    vals.Add(val, new List<long>());
                }
                vals[val].Add(nid);
                if (vals[val].Count == 2)
                {
                    dupes.Add(new KeyValuePair<string, List<long>>(val, vals[val]));
                }
            }
            return dupes;
        }

        public List<Dictionary<string, string>> FindCardsForCardBrowser(string query, bool _order,
            Dictionary<string, string> deckNames)
        {
            return FindCardsForCardBrowserObject(query, _order, deckNames);
        }


        public List<Dictionary<string, string>> FindCardsForCardBrowser(string query, string _order,
                Dictionary<string, string> deckNames)
        {
            return FindCardsForCardBrowserObject(query, _order, deckNames);
        }

        /// <summary>
        /// Return a list of card ids for QUERY
        /// </summary>
        /// <param name="query"></param>
        /// <param name="orderObj"></param>
        /// <param name="deckNames"></param>
        /// <returns></returns>
        private List<Dictionary<string, string>> FindCardsForCardBrowserObject(string query, object orderObj,
            Dictionary<string, string> deckNames)
        {
            string[] tokens = Tokenize(query);
            KeyValuePair<string, string[]> res1 = Where(tokens);
            string preds = res1.Key;
            string[] args = res1.Value;
            var res = new List<Dictionary<string, string>>();
            if (preds == null)
            {
                return res;
            }
            KeyValuePair<string, bool> res2 = orderObj is bool ? Order((bool)orderObj) : Order((string)orderObj);
            string order = res2.Key;
            bool rev = res2.Value;
            
            CardBrowserQuery sql = QueryForCardBrowser(preds, order);
            try
            {
                var listCard = collection.Database.QueryColumn<CardTable>(sql.Card, args);
                var listNote = collection.Database.QueryColumn<NoteTable>(sql.Note, args);
                for (int i = 0; i < listCard.Count; i++)
                {
                    Dictionary<string, string> map = new Dictionary<string, string>();
                    map.Add("id", listCard[i].Id.ToString());
                    map.Add("sfld", listNote[i].Sortfields);
                    map.Add("deck", deckNames[listCard[i].Did.ToString()]);
                    int queue = listCard[i].Queue;
                    String tags = listNote[i].Tags;
                    map.Add("flags", ((queue == -1 ? 1 : 0) 
                                    + (Regex.IsMatch(tags, ".*[Mm]arked.*") ? 2 : 0))
                                    .ToString());
                    map.Add("tags", tags);
                    res.Add(map);
                    // add placeholder for question and answer
                    map.Add("question", "");
                    map.Add("answer", "");
                }
            }
            catch (SQLite.Net.SQLiteException)
            {
                // invalid grouping
                return new List<Dictionary<string, string>>();
            }
            if (rev)
            {
                res.Reverse();
            }
            return res;
        }

        private CardBrowserQuery QueryForCardBrowser(string preds, string order)
        {
            CardBrowserQuery sql;
            sql.Card = "select c.id, c.did, c.queue from cards c, notes n where c.nid=n.id and ";
            sql.Note = "select n.sfld, n.tags from notes n, cards c where c.nid=n.id and ";
            // combine with preds
            if (!String.IsNullOrEmpty(preds))
            {
                sql.Card += "(" + preds + ")";
                sql.Note += "(" + preds + ")";
            }
            else
            {
                sql.Card += "1";
                sql.Note += "(" + preds + ")";
            }
            // order
            if (!String.IsNullOrEmpty(order))
            {
                sql.Card += " " + order;
                sql.Note += "(" + preds + ")";
            }
            return sql;
        }

        private struct CardBrowserQuery
        {
            public string Card;
            public string Note;
        }

    }
}
