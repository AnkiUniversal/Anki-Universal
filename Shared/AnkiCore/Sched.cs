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
using Windows.Data.Json;
using System.Threading;

namespace Shared.AnkiCore
{
    public struct CardTypeCounts
    {
        public int New { get; set; }
        public int Learn { get; set; }
        public int Review { get; set; }
    }

    public class Sched
    {
        private static int[] FACTOR_ADD_VALUES = new int[]
        {
            -150,
            0,
            150
        };

        private bool burySiblingsOnAnswer = true;

        private Collection collection;
        private int queueLimit;
        private int reportLimit;
        private int reps;
        private int today;
        private long dayCutoff;

        private int newCount;
        private int learnCount;
        private int reviewCount;

        private int newCardModulus;

        private double[] mEtaCache = new double[] { -1, -1, -1, -1 };

        // Queues
        private LinkedList<long> newQueue = new LinkedList<long>();
        private LinkedList<long[]> learnQueue = new LinkedList<long[]>();
        private LinkedList<long> learnDayQueue = new LinkedList<long>();
        private LinkedList<long> reviewQueue = new LinkedList<long>();

        private LinkedList<long> newDids;
        private LinkedList<long> learnDids;

        private LinkedList<long> reviewDids;

        public bool BurySiblingsOnAnswer { get { return burySiblingsOnAnswer; } set { burySiblingsOnAnswer = value; } }

        public int NewCount { get { return newCount; } }
        public int LearnCount { get { return learnCount; } }
        public int ReviewCount { get { return reviewCount; } }

        public int Today { get { return today; } set { today = value;} }
        public long DayCutoff { get { return dayCutoff; } }
        public int Reps { get { return reps; } set { reps = value;} }

        public delegate int InJsonOutInt(JsonObject obj);
        public delegate int InLongIntOutInt(long x, int y);
        public delegate bool Notify(string message, Card c);

        //queue types: 0=new/cram, 1=lrn, 2=rev, 3=day lrn, -1=suspended, -2=buried
        //revlog types: 0=lrn, 1=rev, 2=relrn, 3=cram
        //positive revlog intervals are in days (rev), negative in seconds (lrn)
        public Sched(Collection col)
        {
            collection = col;
            queueLimit = 50;
            reportLimit = 1000;
            reps = 0;
            UpdateCutoff();
        }

        public void Reset()
        {
            UpdateCutoff();
            ResetLearn();
            ResetRev();
            ResetNew();
        }

        /// <summary>
        /// This function is added in ankiU to avoid having to
        /// remember the order of return values of Counts()
        /// </summary>
        /// <param name="card"></param>
        /// <returns></returns>
        public CardTypeCounts AllCardTypeCounts(Card card = null)
        {
            int[] counts = Counts(card);
            CardTypeCounts typeCounts = new CardTypeCounts();
            typeCounts.New = counts[0];
            typeCounts.Learn = counts[1];
            typeCounts.Review = counts[2];
            return typeCounts;
        }

        public int[] Counts(Card card = null)
        {
            int[] counts = new int[] { newCount, learnCount, reviewCount };
            if (card != null)
            {
                int idx = CountIdx(card);
                if (idx == 1)
                {
                    counts[1] += card.Left / 1000;
                }
                else
                {
                    counts[idx] += 1;
                }
            }
            return counts;
        }

        public int CountIdx(Card card)
        {
            if (card.Queue == 3)
            {
                return 1;
            }
            return card.Queue;
        }

        public int AnswerButtons(Card card)
        {
            if (card.OriginalDue != 0)
            {
                // normal review in dyn deck?
                if (card.OriginalDeckId != 0 && card.Queue == 2)
                {
                    return 4;
                }
                JsonObject conf = LearnConf(card);
                if (card.Type == 0 || card.Type == CardType.Learn || conf.GetNamedArray("delays").Count > 1)
                {
                    return 3;
                }
                return 2;
            }
            else if (card.Queue == 2)
            {
                return 4;
            }
            else
            {
                return 3;
            }
        }

        public void UpdateTodayStats(Card card, string type, long cnt = 1)
        {
            string key = type + "Today";
            long did = card.DeckId;
            List<JsonObject> list = collection.Deck.Parents(did);
            list.Add(collection.Deck.Get(did));
            foreach (JsonObject g in list)
            {
                JsonArray a = g.GetNamedArray(key);
                // add
                a.Insert(1, JsonValue.CreateNumberValue(a.GetNumberAt(1) + cnt));
            }
        }

        public void ExtendLimits(int newc, int rev)
        {
            JsonObject cur = collection.Deck.Current();
            List<JsonObject> decks = new List<JsonObject>();
            decks.Add(cur);
            decks.AddRange(collection.Deck.Parents((long)JsonHelper.GetNameNumber(cur,"id")));
            foreach (long did in collection.Deck.Children((long)JsonHelper.GetNameNumber(cur,"id")).Values)
            {
                decks.Add(collection.Deck.Get(did));
            }
            foreach (JsonObject g in decks)
            {                
                JsonArray ja = g.GetNamedArray("newToday");
                ja.Insert(1, JsonValue.CreateNumberValue(ja.GetNumberAt(1) - newc));
                g["newToday"] =  ja;
                ja = g.GetNamedArray("revToday");
                ja.Insert(1, JsonValue.CreateNumberValue(ja.GetNumberAt(1) - rev));
                g["revToday"] = ja;
            }
        }

        private int WalkingCount(InJsonOutInt GetIndividualDeckLimit, InLongIntOutInt cntFn)
        {
            int tot = 0;
            Dictionary<long, int> pcounts = new Dictionary<long, int>();
            foreach (long did in collection.Deck.Active())
            {
                int lim = GetIndividualDeckLimit(collection.Deck.Get(did));
                if (lim == 0)
                {
                    continue;
                }
                // check the parents
                List<JsonObject> parents = collection.Deck.Parents(did);
                foreach (JsonObject p in parents)
                {
                    // add if missing
                    long id = (long)JsonHelper.GetNameNumber(p,"id");
                    if (!pcounts.ContainsKey(id))
                    {
                        pcounts.Add(id, GetIndividualDeckLimit(p));
                    }
                    // take minimum of child and parent
                    lim = Math.Min(pcounts[id], lim);
                }
                // see how many cards we actually have
                int cnt = cntFn(did, lim);
                // if non-zero, decrement from parents counts
                foreach (JsonObject p in parents)
                {
                    long id = (long)JsonHelper.GetNameNumber(p,"id");
                    pcounts[id] = pcounts[id] - cnt;
                }
                // we may also be a parent
                pcounts[did] = lim - cnt;
                // and add to running total
                tot += cnt;
            }
            return tot;
        }

        /// <summary>
        /// Returns [deckname, did, rev, lrn, new]
        /// </summary>
        /// <returns></returns>
        public List<DeckDueNode> DeckDueList()
        {
            CheckDay();
            List<JsonObject> decks = collection.Deck.AllSorted();
            var lims = new Dictionary<string, int[]>();
            List<DeckDueNode> data = new List<DeckDueNode>();
            foreach (JsonObject deck in decks)
            {
                // if we've already seen the exact same deck name, remove the
                // invalid duplicate and reload
                if (lims.ContainsKey(deck.GetNamedString("name")))
                {
                    collection.Deck.Remove((long)JsonHelper.GetNameNumber(deck,"id"), false, true);
                    return DeckDueList();
                }
                string p;
                List<string> parts = deck.GetNamedString("name").
                                        Split(new string[] { "::" },
                                        StringSplitOptions.None).ToList();
                if (parts.Count < 2)
                {
                    p = null;
                }
                else
                {
                    parts.RemoveAt(parts.Count - 1);
                    p = String.Join("::", parts);
                }
                // new
                int nlim = DeckNewLimitSingle(deck);
                if (!String.IsNullOrEmpty(p))
                {
                    if (!lims.ContainsKey(p))
                    {
                        // if parent was missing, this deck is invalid, and we need to reload the deck list
                        collection.Deck.Remove((long)JsonHelper.GetNameNumber(deck,"id"), false, true);
                        return DeckDueList();
                    }
                    nlim = Math.Min(nlim, lims[p][0]);
                }
                int _new = NewCountForDeck((long)JsonHelper.GetNameNumber(deck,"id"), nlim);
                // learning
                int lrn = LearnCountForDeck((long)JsonHelper.GetNameNumber(deck,"id"));
                // reviews
                int rlim = DeckReviewLimitSingle(deck);
                if (!String.IsNullOrEmpty(p))
                {
                    rlim = Math.Min(rlim, lims[p][1]);
                }
                int rev = ReviewCountForDeck((long)JsonHelper.GetNameNumber(deck,"id"), rlim);
                // save to list
                data.Add(new DeckDueNode(deck.GetNamedString("name"), (long)JsonHelper.GetNameNumber(deck,"id"), rev, lrn, _new));
                // add deck as a parent
                lims.Add(deck.GetNamedString("name"), new int[] { nlim, rlim });
            }
            return data;
        }

        public List<DeckDueNode> DeckDueTree()
        {
            return GroupChildren(DeckDueList());
        }

        private List<DeckDueNode> GroupChildren(List<DeckDueNode> grps)
        {
            // first, split the group names into components
            foreach (DeckDueNode g in grps)
            {
                g.Names = g.Names[0].Split(new string[] { "::" }, StringSplitOptions.None);
            }
            // and sort based on those components
            grps.Sort();
            // then run main function
            return GroupChildrenMain(grps);
        }

        private List<DeckDueNode> GroupChildrenMain(List<DeckDueNode> grps)
        {
            List<DeckDueNode> tree = new List<DeckDueNode>();
            for (int i = 0; i < grps.Count; i++)
            {
                DeckDueNode node = grps[i];

                string head = node.Names[0];
                // Compose the "tail" node list. The tail is a list of all the nodes that proceed
                // the current one that contain the same name[0]. I.e., they are subdecks that stem
                // from this node. This is our version of python's itertools.groupby.
                List<DeckDueNode> tail = new List<DeckDueNode>();
                tail.Add(node);
                while (i < grps.Count-1)
                {
                    i++;
                    DeckDueNode next = grps[i];
                    if (head.Equals(next.Names[0]))
                    {
                        // Same head - add to tail of current head.
                        tail.Add(next);
                    }
                    else
                    {
                        // We've iterated past this head, so step back in order to use this node as the
                        // head in the next iteration of the outer loop.
                        i--;
                        break;
                    }
                }
                long? did = null;
                int rev = 0;
                int _new = 0;
                int lrn = 0;
                List<DeckDueNode> children = new List<DeckDueNode>();
                foreach (DeckDueNode c in tail)
                {
                    if (c.Names.Length == 1)
                    {
                        // current node
                        did = c.DeckId;
                        rev += c.ReviewCount;
                        lrn += c.LearnCount;
                        _new += c.NewCount;
                    }
                    else
                    {
                        // set new string to tail
                        string[] newTail = new string[c.Names.Length - 1];
                        Array.Copy(c.Names, 1, newTail, 0, c.Names.Length - 1);
                        c.Names = newTail;
                        children.Add(c);
                    }
                }
                children = GroupChildrenMain(children);
                // tally up children counts
                foreach (DeckDueNode ch in children)
                {
                    rev += ch.ReviewCount;
                    lrn += ch.LearnCount;
                    _new += ch.NewCount;
                }
                // limit the counts to the deck's limits
                if (did == null)
                    throw new Exception("Schedule.GroupChildrenMain Did is null");
                JsonObject conf = collection.Deck.ConfForDeckId((long)did);
                JsonObject deck = collection.Deck.Get(did);
                if (JsonHelper.GetNameNumber(conf,"dyn") == 0)
                {
                    rev = Math.Max(0, Math.Min(rev, (int)(JsonHelper.GetNameNumber(conf.GetNamedObject("rev"),"perDay")
                                                    - deck.GetNamedArray("revToday").GetNumberAt(1))));
                    _new = Math.Max(0, Math.Min(_new, (int)(JsonHelper.GetNameNumber(conf.GetNamedObject("new"),"perDay")
                                                    - deck.GetNamedArray("newToday").GetNumberAt(1))));
                }
                tree.Add(new DeckDueNode(head, (long)did, rev, lrn, _new, children));
            }
            return tree;
        }  

        private void ResetNewCount()
        {
            newCount = WalkingCount(DeckNewLimitSingle, (long did, int lim) =>
            {
                return collection.Database.QueryScalar<int>(
                "select count() from(select 1 from cards where did = ? "
                + "and queue = 0 limit ?)", did, lim);
            });
        }

        private void ResetNew()
        {
            ResetNewCount();
            newDids = new LinkedList<long>(collection.Deck.Active());
            newQueue.Clear();
            UpdateNewCardRatio();
        }

        private bool FillNew()
        {
            if (newQueue.Count > 0)
            {
                return true;
            }
            if (newCount == 0)
            {
                return false;
            }
            while (newDids.Count != 0)
            {
                long did = newDids.First();
                int lim = Math.Min(queueLimit, DeckNewLimit(did));
                if (lim != 0)
                {
                    newQueue.Clear();
                    // fill the queue with the current did
                    var list = collection.Database.QueryColumn<CardIdOnlyTable>
                                ("SELECT id FROM cards WHERE did = "
                                + did + " AND queue = 0 order by due LIMIT " + lim);
                    foreach (CardIdOnlyTable c in list)
                    {
                        newQueue.AddLast(c.Id);
                    }

                    if (newQueue.Count != 0)
                    {
                        // Note: python ver reverses mNewQueue and returns the last element in _getNewCard().
                        // java ver differs by leaving the queue intact and returning the *first* element
                        // in _getNewCard().
                        return true;
                    }
                }
                // nothing left in the deck; move to next
                newDids.RemoveFirst();
            }
            if (newCount != 0)
            {
                // if we didn't get a card but the count is non-zero,
                // we need to check again for any cards that were
                // removed from the queue but not buried
                ResetNew();
                return FillNew();
            }
            return false;
        }

        private void UpdateNewCardRatio()
        {
            if (JsonHelper.GetNameNumber(collection.Conf,"newSpread") == (double)ReviewType.DISTRIBUTE)
            {
                if (newCount != 0)
                {
                    newCardModulus = (newCount + reviewCount) / newCount;
                    // if there are cards to review, ensure modulo >= 2
                    if (reviewCount != 0)
                    {
                        newCardModulus = Math.Max(2, newCardModulus);
                    }
                    return;
                }
            }
            newCardModulus = 0;
        }

        private bool TimeForNewCard()
        {
            if (newCount == 0)
            {
                return false;
            }
            ReviewType spread = (ReviewType)JsonHelper.GetNameNumber(collection.Conf,"newSpread");

            if (spread == ReviewType.LAST)
            {
                return false;
            }
            else if (spread == ReviewType.FIRST)
            {
                return true;
            }
            else if (newCardModulus != 0)
            {
                return (reps != 0 && (reps % newCardModulus == 0));
            }
            else
            {
                return false;
            }
        }

        private int DeckNewLimit(long did, InJsonOutInt fn = null)
        {
            if (fn == null)
            {
                fn = this.DeckNewLimitSingle;
            }
            List<JsonObject> decks = collection.Deck.Parents(did);
            decks.Add(collection.Deck.Get(did));
            int lim = -1;
            // for the deck and each of its parents
            int rem = 0;
            foreach (JsonObject g in decks)
            {
                rem = (int)fn(g);
                if (lim == -1)
                {
                    lim = rem;
                }
                else
                {
                    lim = Math.Min(rem, lim);
                }
            }
            return lim;
        }

        /// <summary>
        /// Limit for deck without parent limits
        /// </summary>
        /// <param name="g"></param>
        /// <returns></returns>
        public int DeckNewLimitSingle(JsonObject g)
        {
            if (JsonHelper.GetNameNumber(g,"dyn") != 0)
            {
                return reportLimit;
            }
            JsonObject c = collection.Deck.ConfForDeckId((long)JsonHelper.GetNameNumber(g,"id"));
            return (int)Math.Max(0, JsonHelper.GetNameNumber(c.GetNamedObject("new"),"perDay")
                                        - g.GetNamedArray("newToday").GetNumberAt(1));
        }

        /// <summary>
        /// New count for a single deck.
        /// </summary>
        /// <param name="did"></param>
        /// <param name="lim"></param>
        /// <returns></returns>
        public int NewCountForDeck(long did, int lim)
        {
            if (lim == 0)
            {
                return 0;
            }
            lim = Math.Min(lim, reportLimit);
            return collection.Database.QueryScalar<int>("SELECT count() FROM (SELECT 1 FROM cards WHERE did = "
                                                            + did + " AND queue = 0 LIMIT " + lim + ")");
        }

        public int TotalNewCountForCurrentDecks()
        {
            return collection.Database.QueryScalar<int>("SELECT count() FROM cards WHERE id " +
                                                        "IN (SELECT id FROM cards WHERE did IN " +
                                                        Utils.Ids2str(collection.Deck.Active().ToArray()) +
                                                        " AND queue = 0 )");
        }

        public int TotalNewCountForCurrentDecksWithLimit()
        {
            return collection.Database.QueryScalar<int>("SELECT count() FROM cards WHERE id " +
                                                        "IN (SELECT id FROM cards WHERE did IN " +
                                                        Utils.Ids2str(collection.Deck.Active().ToArray()) +
                                                        " AND queue = 0 LIMIT " + reportLimit + ")");
        }

        private int LearnCountForDeck(long did)
        {
            int cnt = collection.Database.QueryScalar<int>(
                    "SELECT sum(left / 1000) FROM (SELECT left FROM cards WHERE did = " + did
                            + " AND queue = 1 AND due < "
                            + (DateTimeOffset.Now.ToUnixTimeSeconds()
                            + JsonHelper.GetNameNumber(collection.Conf,"collapseTime"))
                            + " LIMIT " + reportLimit + ")");

            return cnt + collection.Database.QueryScalar<int>(
                    "SELECT count() FROM (SELECT 1 FROM cards WHERE did = " + did
                            + " AND queue = 3 AND due <= " + today
                            + " LIMIT " + reportLimit + ")");
        }

        public int ReviewCountForDeck(long did, int lim)
        {
            lim = Math.Min(lim, reportLimit);
            return collection.Database.QueryScalar<int>("SELECT count() FROM (SELECT 1 FROM cards WHERE did = "
                                            + did + " AND queue = 2 AND due <= "
                                            + today + " LIMIT " + lim + ")");
        }

        public int TotalReviewForCurrentDecks()
        {
            return collection.Database.QueryScalar<int>(String.Format("SELECT count() FROM cards "
                + "WHERE id IN (SELECT id FROM cards WHERE did IN {0} AND queue = 2 AND due <= {1})",
                    Utils.Ids2str(collection.Deck.Active().ToArray()), today));
        }

        public int TotalReviewForCurrentDecksWithLimit()
        {
            return collection.Database.QueryScalar<int>(String.Format("SELECT count() FROM cards "
                + "WHERE id IN (SELECT id FROM cards WHERE did IN {0} AND queue = 2 AND due <= {1} LIMIT {2})",
                    Utils.Ids2str(collection.Deck.Active().ToArray()), today, reportLimit));
        }

        private void ResetLearnCount()
        {
            //WARNING Different with java and python ver
            //we do not get whole day
            long maxTimeToGetDueCards = DateTimeOffset.Now.ToUnixTimeSeconds() + (long)JsonHelper.GetNameNumber(collection.Conf,"collapseTime");
            if (dayCutoff < maxTimeToGetDueCards)
                maxTimeToGetDueCards = dayCutoff;

            // sub-day
            learnCount = collection.Database.QueryScalar<int>(
                    "SELECT sum(left / 1000) FROM (SELECT left FROM cards WHERE did IN " + DeckLimit()
                    + " AND queue = 1 AND due < " + dayCutoff + " LIMIT " + reportLimit + ")");

            // day
            learnCount += collection.Database.QueryScalar<int>(
                    "SELECT count() FROM cards WHERE did IN " + DeckLimit() + " AND queue = 3 AND due <= " + today
                            + " LIMIT " + reportLimit);
        }

        private void ResetLearn()
        {
            ResetLearnCount();
            learnQueue.Clear();
            learnDayQueue.Clear();
            learnDids = collection.Deck.Active();
        }

        /// <summary>
        /// Sub-day learning
        /// </summary>
        /// <returns></returns>
        private bool FillLearn()
        {
            if (learnCount == 0)
            {
                return false;
            }
            if (!(learnQueue.Count == 0))
            {
                return true;
            }
            var list = collection.Database.QueryColumn<CardTable>(
                          "SELECT due, id FROM cards WHERE did IN " + DeckLimit() + " AND queue = 1 AND due < "
                                        + dayCutoff + " LIMIT " + reportLimit);
            List<long[]> didSortList = new List<long[]>();
            foreach (CardTable c in list)
                didSortList.Add(new long[] { c.Due, c.Id });

            // as it arrives sorted by did first, we need to sort it
            didSortList.Sort((long[] x, long[] y) =>
            {
                return x[0].CompareTo(y[0]);
            });
            learnQueue = new LinkedList<long[]>(didSortList);

            return !(learnQueue.Count == 0);
        }

        /// <summary>
        /// Daily learning
        /// </summary>
        /// <returns></returns>
        private bool FillLearnDay()
        {
            if (learnCount == 0)
            {
                return false;
            }
            if (!(learnDayQueue.Count == 0))
            {
                return true;
            }
            while (learnDids.Count > 0)
            {
                long did = learnDids.First();
                // fill the queue with the current did
                learnDayQueue.Clear();


                var array = from s in collection.Database.QueryColumn<CardIdOnlyTable>(
                                "SELECT id FROM cards WHERE did = "
                                + did + " AND queue = 3 AND due <= "
                                + today + " LIMIT " + queueLimit)
                            select s.Id;

                foreach (long s in array)
                {
                    learnDayQueue.AddLast(s);
                }
                if (learnDayQueue.Count > 0)
                {
                    // order
                    Random r = new Random(today);
                    learnDayQueue = Utils.Shuffle<long>(learnDayQueue, r);
                    // is the current did empty?
                    if (learnDayQueue.Count < queueLimit)
                    {
                        learnDids.RemoveFirst();
                    }
                    return true;
                }
                // nothing left in the deck; move to next
                learnDids.RemoveFirst();
            }
            return false;
        }

        private int DelayForGrade(JsonObject conf, int left)
        {
            left = left % 1000;
            JsonArray ja = conf.GetNamedArray("delays");
            int len = ja.Count;
            double delay;
            //WARNING: Different with python and java ver
            //We use if-else clause instead of try-catch block
            int index = len - left;
            if(index >= 0 && index < len)
                delay = ja.GetNumberAt((uint)index);
            else
            {
                if (len > 0)
                {
                    delay = ja.GetNumberAt(0);
                }
                else
                {
                    // user deleted final step; use dummy value
                    delay = 1.0;
                }
            }
            return (int)(delay * 60.0);
        }

        private JsonObject LearnConf(Card card)
        {
            if (card.Type == CardType.Review)
            {
                return LapseConf(card);
            }
            else
            {
                return NewConf(card);
            }
        }   

        private int StartingLeft(Card card)
        {
            JsonObject conf;
            if (card.Type == CardType.Review)
            {
                conf = LapseConf(card);
            }
            else
            {
                conf = LearnConf(card);
            }
            int tot = conf.GetNamedArray("delays").Count;
            int tod = LeftToday(conf.GetNamedArray("delays"), tot);
            return tot + tod * 1000;
        }

        private int LeftToday(JsonArray delays, int left, long now = 0)
        {
            if (now == 0)
            {
                now = DateTimeOffset.Now.ToUnixTimeSeconds();
            }
            int ok = 0;
            int offset = Math.Min(left, delays.Count);
            for (int i = 0; i < offset; i++)
            {
                now += (int)(delays.GetNumberAt((uint)(delays.Count - offset + i)) * 60);
                if (now > dayCutoff)
                {
                    break;
                }
                ok = i;
            }
            return ok + 1;
        }     

        private int DeckReviewLimit(long did)
        {
            return DeckNewLimit(did, this.DeckReviewLimitSingle);
        }

        private int DeckReviewLimitSingle(JsonObject d)
        {
            if (JsonHelper.GetNameNumber(d,"dyn") != 0)
            {
                return reportLimit;
            }
            JsonObject c = collection.Deck.ConfForDeckId((long)JsonHelper.GetNameNumber(d,"id"));
            return (int)Math.Max(0, JsonHelper.GetNameNumber(c.GetNamedObject("rev"),"perDay")
                        - d.GetNamedArray("revToday").GetNumberAt(1));
        }

        private void ResetReviewCount()
        {
            reviewCount = WalkingCount(DeckReviewLimitSingle, (long did, int lim) =>
            {
                return collection.Database.QueryScalar<int>(
                "select count() from(select id from cards where did = ? "
                + "and queue = 2 and due <= ? limit " + lim + ")", did, today);
            });
        }

        private void ResetRev()
        {
            ResetReviewCount();
            reviewQueue.Clear();
            reviewDids = collection.Deck.Active();
        }

        private bool FillRev()
        {
            if (reviewQueue.Count != 0)
                return true;
            if (reviewCount == 0)
                return false;

            while (reviewDids.Count > 0)
            {
                long did = reviewDids.First();
                int lim = Math.Min(queueLimit, DeckReviewLimit(did));
                if (lim != 0)
                {
                    reviewQueue.Clear();
                    var list = collection.Database.QueryColumn<CardIdOnlyTable>(
                                "SELECT id FROM cards WHERE did = " + did
                                + " AND queue = 2 AND due <= " + today
                                + " LIMIT " + lim);
                    foreach (CardIdOnlyTable c in list)
                        reviewQueue.AddLast(c.Id);

                    if (reviewQueue.Count != 0)
                    {
                        // ordering
                        if (JsonHelper.GetNameNumber(collection.Deck.Get(did),"dyn") != 0)
                        {
                            // dynamic decks need due order preserved
                            // Note: python ver reverses mRevQueue and returns the last element in _getRevCard().
                            // java ver differs by leaving the queue intact and returning the *first* element
                            // in _getRevCard().
                        }
                        else
                        {
                            Random r = new Random(today);
                            reviewQueue = Utils.Shuffle<long>(reviewQueue, r);
                        }
                        // is the current did empty?
                        if (reviewQueue.Count < lim)
                        {
                            reviewDids.RemoveFirst();
                        }
                        return true;
                    }
                }
                // nothing left in the deck; move to next
                reviewDids.RemoveFirst();
            }
            if (reviewCount != 0)
            {
                // if we didn't get a card but the count is non-zero,
                // we need to check again for any cards that were
                // removed from the queue but not buried
                ResetRev();
                return FillRev();
            }
            return false;
        }

        private int NextLapseInterval(Card card, JsonObject conf)
        {
            return Math.Max((int)JsonHelper.GetNameNumber(conf,"minInt"), (int)(card.Interval * JsonHelper.GetNameNumber(conf,"mult")));
        }

        public int[] FuzzedIvlRange(int ivl)
        {
            int fuzz;
            if (ivl < 2)
            {
                return new int[] { 1, 1 };
            }
            else if (ivl == 2)
            {
                return new int[] { 2, 3 };
            }
            else if (ivl < 7)
            {
                fuzz = (int)(ivl * 0.25);
            }
            else if (ivl < 30)
            {
                fuzz = Math.Max(2, (int)(ivl * 0.15));
            }
            else
            {
                fuzz = Math.Max(4, (int)(ivl * 0.05));
            }
            // fuzz at least a day
            fuzz = Math.Max(fuzz, 1);
            return new int[] { ivl - fuzz, ivl + fuzz };
        }

        /// <summary>
        /// Integer interval after interval factor and prev+1 constraints applied
        /// </summary>
        /// <param name="ivl"></param>
        /// <param name="conf"></param>
        /// <param name="prev"></param>
        /// <returns></returns>
        private int ConstrainedInterval(int ivl, JsonObject conf, double prev)
        {
            double newIvl = ivl;
            newIvl = ivl * conf.GetNamedNumber("ivlFct", 1.0);
            return (int)Math.Max(newIvl, prev + 1);
        }

        private long DaysLate(Card card)
        {
            long due = card.OriginalDeckId != 0 ? card.OriginalDue : card.Due;
            return Math.Max(0, today - due);
        }       

        public JsonObject CardConf(Card card)
        {
            return collection.Deck.ConfForDeckId(card.DeckId);
        }

        private JsonObject NewConf(Card card)
        {
            JsonObject conf = CardConf(card);
            // normal deck
            if (card.OriginalDeckId == 0)
            {
                return conf.GetNamedObject("new");
            }
            // dynamic deck; override some attributes, use original deck for others
            JsonObject oconf = collection.Deck.ConfForDeckId(card.OriginalDeckId);
            //We need this block because UWP API does not recognize
            // delays = null as array
            JsonArray delays;
            try
            {
                var value = conf.GetNamedValue("delays");
                if (value.ValueType == JsonValueType.Null)
                    delays = null;
                else
                    delays = value.GetArray();
            }
            catch
            {
                delays = null;
            }

            if (delays == null)
            {
                delays = oconf.GetNamedObject("new").GetNamedArray("delays");
            }
            JsonObject dict = new JsonObject();
            // original deck
            dict.Add("ints", oconf.GetNamedObject("new").GetNamedArray("ints"));
            dict.Add("initialFactor", JsonValue.CreateNumberValue(JsonHelper.GetNameNumber(oconf.GetNamedObject("new"),"initialFactor")));
            dict.Add("bury", JsonValue.CreateBooleanValue(oconf.GetNamedObject("new").GetNamedBoolean("bury", true)));
            // overrides
            dict.Add("delays", delays);
            dict.Add("separate", JsonValue.CreateBooleanValue(conf.GetNamedBoolean("separate")));
            dict.Add("order", JsonValue.CreateNumberValue((int)NewCardInsertOrder.DUE));
            dict.Add("perDay", JsonValue.CreateNumberValue(reportLimit));
            return dict;
        }

        public JsonObject LapseConf(Card card)
        {
            JsonObject conf = CardConf(card);
            // normal deck
            if (card.OriginalDeckId == 0)
            {
                return conf.GetNamedObject("lapse");
            }
            // dynamic deck; override some attributes, use original deck for others
            JsonObject oconf = collection.Deck.ConfForDeckId(card.OriginalDeckId);
            
            //We need this block because python and java ver allow delays = null
            //while C# only recognizes delays = [null] as array
            JsonArray delays;
            try
            {
                var value = conf.GetNamedValue("delays");
                if (value.ValueType == JsonValueType.Null)
                    delays = null;
                else
                    delays = value.GetArray();                
            }
            catch
            {
                delays = null;
            }

            if (delays == null)
            {
                delays = oconf.GetNamedObject("lapse").GetNamedArray("delays");
            }
            JsonObject dict = new JsonObject();
            // original deck
            dict.Add("minInt", JsonValue.CreateNumberValue(JsonHelper.GetNameNumber(oconf.GetNamedObject("lapse"),"minInt")));
            dict.Add("leechFails", JsonValue.CreateNumberValue(JsonHelper.GetNameNumber(oconf.GetNamedObject("lapse"),"leechFails")));
            dict.Add("leechAction", JsonValue.CreateNumberValue(JsonHelper.GetNameNumber(oconf.GetNamedObject("lapse"),"leechAction")));
            dict.Add("mult", JsonValue.CreateNumberValue(JsonHelper.GetNameNumber(oconf.GetNamedObject("lapse"),"mult")));
            // overrides
            dict.Add("delays", delays);
            dict.Add("resched", JsonValue.CreateBooleanValue(conf.GetNamedBoolean("resched")));
            return dict;
        }

        private JsonObject RevConf(Card card)
        {
            JsonObject conf = CardConf(card);
            // normal deck
            if (card.OriginalDeckId == 0)
            {
                return conf.GetNamedObject("rev");
            }
            // dynamic deck
            return collection.Deck.ConfForDeckId(card.OriginalDeckId).GetNamedObject("rev");
        }

        public string DeckLimit()
        {
            return Utils.Ids2str(collection.Deck.Active().ToArray());
        }

        private bool Resched(Card card)
        {
            JsonObject conf = CardConf(card);
            if (JsonHelper.GetNameNumber(conf,"dyn") == 0)
            {
                return true;
            }
            return conf.GetNamedBoolean("resched");
        }

        //Daily cutoff 
        //This function uses GregorianCalendar so as to be sensitive to leap years, daylight savings, etc.
        private void UpdateCutoff()
        {
            int oldToday = today;
            // days since col created
            today = (int)((DateTimeOffset.Now.ToUnixTimeSeconds() - collection.Crt) / 86400);
            // end of day cutoff
            dayCutoff = collection.Crt + ((today + 1) * 86400);

            // update all daily counts, but don't save decks to prevent needless conflicts. we'll save on card answer
            // instead
            foreach (JsonObject deck in collection.Deck.All())
            {
                Update(deck);
            }
        }

        private void Update(JsonObject g)
        {
            foreach (string t in new string[] { "new", "rev", "lrn", "time" })
            {
                string key = t + "Today";
                var day = g.GetNamedArray(key).GetNumberAt(0);
                if (day != today)
                {
                    JsonArray ja = new JsonArray();
                    ja.Add(JsonValue.CreateNumberValue(today));
                    ja.Add(JsonValue.CreateNumberValue(0));
                    g[key] = ja;
                }
            }
        }

        public void CheckDay()
        {
            // check if the day has rolled over
            if (DateTimeOffset.Now.ToUnixTimeSeconds() > dayCutoff)
            {
                Reset();
            }
        }

        /// <summary>
        /// True if there are any rev cards due
        /// </summary>
        /// <returns></returns>
        public bool HaveRevDue()
        {
            return collection.Database.QueryScalar<int>(
                            "SELECT 1 FROM cards WHERE did IN " + DeckLimit() + " AND queue = 2 AND due <= " + today
                                    + " LIMIT 1") != 0;
        }

        /// <summary>
        /// True if there are any new cards due
        /// </summary>
        /// <returns></returns>
        public bool HaveNewDue()
        {
            return collection.Database.QueryScalar<int>("SELECT 1 FROM cards WHERE did IN " + DeckLimit() + " AND queue = 0 LIMIT 1") != 0;
        }

        public int CardCount()
        {
            string dids = DeckLimit();
            return collection.Database.QueryScalar<int>("SELECT count() FROM cards WHERE did IN " + dids);
        }

        public int MatureCount()
        {
            string dids = DeckLimit();
            return collection.Database.QueryScalar<int>("SELECT count() FROM cards WHERE type = 2 AND ivl >= 21 AND did IN " + dids);
        }

        public void DecrementCounts(Card card)
        {
            int type = card.Queue;
            switch (type)
            {
                case 0:
                    newCount--;
                    break;
                case 1:
                    learnCount -= card.Left / 1000;
                    break;
                case 2:
                    reviewCount--;
                    break;
                case 3:
                    learnCount--;
                    break;
            }
        }

        /// <summary>
        /// Sorts a card into the lrn queue
        /// </summary>
        /// <param name="due"></param>
        /// <param name="id"></param>
        private void SortIntoLearn(long due, long id)
        {
            //WARNING: Unlike the java ver, we check if
            //the learn queue is empty first or not 
            //before accessing it.
            if (learnQueue.Count == 0)
            {
                learnQueue.AddFirst(new long[] { due, id });
                return;
            }
            LinkedListNode<long[]> node = learnQueue.First;
            bool isInsert = false;
            foreach (var learn in learnQueue)
            {
                if (learn[0] > due)
                {
                    node = learnQueue.Find(learn);
                    isInsert = true;
                    break;
                }
            }
            if(isInsert)
                learnQueue.AddBefore(node, new long[] { due, id });
            else
                learnQueue.AddLast(new long[] { due, id });
        }

        public bool LeechActionSuspend(Card card)
        {
            JsonObject conf;
            conf = CardConf(card).GetNamedObject("lapse");
            return JsonHelper.GetNameNumber(conf,"leechAction") == 0;
        }
    }

    /**
     * Holds the data for a single node (row) in the deck due tree (the user-visible list
     * of decks and their counts). A node also contains a list of nodes that refer to the
     * next level of sub-decks for that particular deck (which can be an empty list).
     *
     * The names field is an array of names that build a deck name from a hierarchy (i.e., a nested
     * deck will have an entry for every level of nesting). While the python version interchanges
     * between a string and a list of strings throughout processing, we always use an array for
     * this field and use names[0] for those cases.
     */
    public class DeckDueNode : IComparable
    {
        public string[] Names { get; set; }
        public long DeckId { get; set; }
        public int Depth { get; set; }
        public int ReviewCount { get; set; }
        public int LearnCount { get; set; }
        public int NewCount { get; set; }
        public List<DeckDueNode> Children { get; set; }

        public DeckDueNode(string[] names, long did, int reviewCount, int learnCount, int newCount)
        {
            this.Names = names;
            this.DeckId = did;
            this.ReviewCount = reviewCount;
            this.LearnCount = learnCount;
            this.NewCount = newCount;
        }

        public DeckDueNode(string name, long did, int revCount, int lrnCount, int newCount)
            : this(new string[] { name }, did, revCount, lrnCount, newCount)
        {
            Children = new List<DeckDueNode>();
        }

        public DeckDueNode(string name, long did, int revCount, int lrnCount, int newCount, List<DeckDueNode> children)
            : this(new string[] { name }, did, revCount, lrnCount, newCount)
        {
            this.Children = children;
        }

        /// <summary>
        /// Sort on the head of the node
        /// </summary>
        /// <param name="other"></param>
        /// <returns></returns>
        public int CompareTo(object other)
        {
            DeckDueNode rhs = (DeckDueNode)other;
            // Consider each subdeck name in the ordering
            for (int i = 0; i < Names.Length && i < rhs.Names.Length; i++)
            {
                int cmp = Names[i].CompareTo(rhs.Names[i]);
                if (cmp == 0)
                {
                    continue;
                }
                return cmp;
            }
            // If we made it this far then the arrays are of different length. The longer one should
            // always come after since it contains all of the sections of the shorter one inside it
            // (i.e., the short one is an ancestor of the longer one).
            if (rhs.Names.Length > Names.Length)
            {
                return -1;
            }
            else
            {
                return 1;
            }
        }
    }


}

