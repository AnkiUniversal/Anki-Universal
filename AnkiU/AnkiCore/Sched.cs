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

namespace AnkiU.AnkiCore
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

        private bool haveCustomStudy = true;
        private bool spreadRev = true;
        private bool burySiblingsOnAnswer = true;

        private Collection collection;
        private int queueLimit;
        private int reportLimit;
        private int reps;
        private bool haveQueues;
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

        public event Notify NotifyLeechEvent;

        //queue types: 0=new/cram, 1=lrn, 2=rev, 3=day lrn, -1=suspended, -2=buried
        //revlog types: 0=lrn, 1=rev, 2=relrn, 3=cram
        //positive revlog intervals are in days (rev), negative in seconds (lrn)
        public Sched(Collection col)
        {
            collection = col;
            queueLimit = 50;
            reportLimit = 1000;
            reps = 0;
            haveQueues = false;
            UpdateCutoff();
        }

        /// <summary>
        /// Pop the next card from queues.
        /// </summary>
        /// <returns>Card in queue or null if finished</returns>
        public Card PopCard()
        {
            CheckDay();
            if (!haveQueues)
            {
                Reset();
            }
            Card card = GetCardFromQueues();
            if (card != null)
            {
                collection.Log(args: card);
                if (burySiblingsOnAnswer)
                {
                    BurySiblings(card);
                }
                reps += 1;
                card.StartTimer();
                return card;
            }
            return null;
        }

        public void Reset()
        {
            UpdateCutoff();
            ResetLearn();
            ResetRev();
            ResetNew();
            haveQueues = true;
        }


        public enum AnswerEase
        {
            Again = 1,
            Hard = 2,
            Good = 3,
            Easy = 4
        }
        public void AnswerCard(Card card, AnswerEase ease)
        {
            collection.Log();
            collection.MarkReview(card);
            if (burySiblingsOnAnswer)
            {
                BurySiblings(card);
            }
            card.Reps = card.Reps + 1;
            // former is for logging new cards, latter also covers filt. decks
            card.WasNew = (card.Type == CardType.New);
            bool wasNewQ = (card.Queue == 0);
            if (wasNewQ)
            {
                // came from the new queue, move to learning
                card.Queue = 1;
                // if it was a new card, it's now a learning card
                if (card.Type == CardType.New)
                {
                    card.Type = CardType.Learn;
                }
                // init reps to graduation
                card.Left = StartingLeft(card);
                // dynamic?
                if (card.OriginalDeckId != 0 && card.Type == CardType.Review)
                {
                    if (Resched(card))
                    {
                        // reviews get their ivl boosted on first sight
                        card.Interval = (DynIntervalBoost(card));
                        card.OriginalDue = today + card.Interval;
                    }
                }
                UpdateTodayStats(card, "new");
            }
            if (card.Queue == 1 || card.Queue == 3)
            {
                AnswerLearnCard(card, ease);
                if (!wasNewQ)
                {
                    UpdateTodayStats(card, "lrn");
                }
            }
            else if (card.Queue == 2)
            {
                AnswerReviewCard(card, ease);
                UpdateTodayStats(card, "rev");
            }
            else
            {
                throw new Exception("Invalid queue");
            }
            UpdateTodayStats(card, "time", card.TimeTaken());
            card.TimeModified = DateTimeOffset.Now.ToUnixTimeSeconds();
            card.Usn = collection.Usn;
            card.SaveSchedToDatabase();
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

        public void UnburyCards()
        {
            collection.Conf["lastUnburied"] = JsonValue.CreateNumberValue(today);
            var list = collection.Database.QueryColumn<CardIdOnlyTable>("select id from cards where queue = -2");
            collection.Log(args: (from s in list select s.Id).ToArray());
            collection.Database.Execute("update cards set queue=type where queue = -2");
        }

        public void UnburyCardsForDeck(long did)
        {
            long odid = collection.Deck.Selected();
            collection.Deck.Select(did);
            UnburyCardsForDeck();
            collection.Deck.Select(odid);
        }

        public void UnburyCardsForDeck()
        {
            string sids = Utils.Ids2str(collection.Deck.Active().ToArray());
            var list = collection.Database.QueryColumn<CardIdOnlyTable>("select id from cards where queue = -2 and did in " + sids);
            collection.Log(args: (from s in list select s.Id).ToArray());
            var obj = new object[] { DateTimeOffset.Now.ToUnixTimeSeconds(), collection.Usn };
            collection.Database.Execute("update cards set mod=?,usn=?,queue=type where queue = -2 and did in " + sids, obj);
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
                collection.Deck.Save(g);
            }
        }

        public void ExtendLimits(int newc, int rev)
        {
            JsonObject cur = collection.Deck.Current();
            List<JsonObject> decks = new List<JsonObject>();
            decks.Add(cur);
            decks.AddRange(collection.Deck.Parents((long)cur.GetNamedNumber("id")));
            foreach (long did in collection.Deck.Children((long)cur.GetNamedNumber("id")).Values)
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
                collection.Deck.Save(g);
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
                    long id = (long)p.GetNamedNumber("id");
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
                    long id = (long)p.GetNamedNumber("id");
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
            collection.Deck.RecoverOrphans();
            List<JsonObject> decks = collection.Deck.AllSorted();
            var lims = new Dictionary<string, int[]>();
            List<DeckDueNode> data = new List<DeckDueNode>();
            foreach (JsonObject deck in decks)
            {
                // if we've already seen the exact same deck name, remove the
                // invalid duplicate and reload
                if (lims.ContainsKey(deck.GetNamedString("name")))
                {
                    collection.Deck.Remove((long)deck.GetNamedNumber("id"), false, true);
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
                        collection.Deck.Remove((long)deck.GetNamedNumber("id"), false, true);
                        return DeckDueList();
                    }
                    nlim = Math.Min(nlim, lims[p][0]);
                }
                int _new = NewCountForDeck((long)deck.GetNamedNumber("id"), nlim);
                // learning
                int lrn = LearnCountForDeck((long)deck.GetNamedNumber("id"));
                // reviews
                int rlim = DeckReviewLimitSingle(deck);
                if (!String.IsNullOrEmpty(p))
                {
                    rlim = Math.Min(rlim, lims[p][1]);
                }
                int rev = ReviewCountForDeck((long)deck.GetNamedNumber("id"), rlim);
                // save to list
                data.Add(new DeckDueNode(deck.GetNamedString("name"), (long)deck.GetNamedNumber("id"), rev, lrn, _new));
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
                if (conf.GetNamedNumber("dyn") == 0)
                {
                    rev = Math.Max(0, Math.Min(rev, (int)(conf.GetNamedObject("rev").GetNamedNumber("perDay")
                                                    - deck.GetNamedArray("revToday").GetNumberAt(1))));
                    _new = Math.Max(0, Math.Min(_new, (int)(conf.GetNamedObject("new").GetNamedNumber("perDay")
                                                    - deck.GetNamedArray("newToday").GetNumberAt(1))));
                }
                tree.Add(new DeckDueNode(head, (long)did, rev, lrn, _new, children));
            }
            return tree;
        }

        /// <summary>
        /// Return next card or null
        /// </summary>
        /// <returns></returns>
        private Card GetCardFromQueues()
        {
            // learning card due?
            Card c = GetLearnCard();
            if (c != null)
            {
                return c;
            }
            // new first, or time for one?
            if (TimeForNewCard())
            {
                c = GetNewCard();
                if (c != null)
                {
                    return c;
                }
            }
            // Card due for review?
            c = GetRevCard();
            if (c != null)
            {
                return c;
            }
            // day learning card due?
            c = GetLearnDayCard();
            if (c != null)
            {
                return c;
            }
            // New cards left?
            c = GetNewCard();
            if (c != null)
            {
                return c;
            }
            // collapse or finish
            return GetLearnCard(true);
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

        private Card GetNewCard()
        {
            if (FillNew())
            {
                newCount -= 1;
                long result = newQueue.First();
                newQueue.RemoveFirst();
                return collection.GetCard(result);
            }
            return null;
        }

        private void UpdateNewCardRatio()
        {
            if (collection.Conf.GetNamedNumber("newSpread") == (double)ReviewType.DISTRIBUTE)
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
            ReviewType spread = (ReviewType)collection.Conf.GetNamedNumber("newSpread");

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
            if (g.GetNamedNumber("dyn") != 0)
            {
                return reportLimit;
            }
            JsonObject c = collection.Deck.ConfForDeckId((long)g.GetNamedNumber("id"));
            return (int)Math.Max(0, c.GetNamedObject("new").GetNamedNumber("perDay")
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
                            + collection.Conf.GetNamedNumber("collapseTime"))
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

        private Card GetLearnCard(bool collapse = false)
        {
            if (FillLearn())
            {
                double cutoff = DateTimeOffset.Now.ToUnixTimeSeconds();
                if (collapse)
                {
                    cutoff += collection.Conf.GetNamedNumber("collapseTime");
                }
                if (learnQueue.First()[0] < cutoff)
                {
                    long id = learnQueue.First()[1];
                    learnQueue.RemoveFirst();
                    Card card = collection.GetCard(id);
                    learnCount -= card.Left / 1000;
                    return card;
                }
            }
            return null;
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

        private Card GetLearnDayCard()
        {
            if (FillLearnDay())
            {
                learnCount -= 1;
                long result = learnDayQueue.First();
                learnDayQueue.RemoveFirst();
                return collection.GetCard(result);
            }
            return null;
        }

        private void AnswerLearnCard(Card card, AnswerEase ease)
        {
            JsonObject conf = LearnConf(card);
            int type;
            if ((card.OriginalDeckId != 0) && (!card.WasNew))
            {
                type = 3;
            }
            else if (card.Type == CardType.Review)
            {
                type = 2;
            }
            else
            {
                type = 0;
            }
            bool leaving = false;
            // lrnCount was decremented once when card was fetched
            int lastLeft = card.Left;
            // immediate graduate?
            if (ease == AnswerEase.Good)
            {
                RescheduleAsRev(card, conf, true);
                leaving = true;
                // graduation time?
            }
            else if (ease == AnswerEase.Hard && (card.Left % 1000) - 1 <= 0)
            {
                RescheduleAsRev(card, conf, false);
                leaving = true;
            }
            else
            {
                // one step towards graduation
                if (ease == AnswerEase.Hard)
                {
                    // decrement real left count and recalculate left today
                    int left = (card.Left % 1000) - 1;
                    card.Left = (LeftToday(conf.GetNamedArray("delays"), left) * 1000 + left);
                }
                else
                {
                    card.Left = StartingLeft(card);
                    bool resched = Resched(card);
                    if (conf.ContainsKey("mult") && resched)
                    {
                        // review that's lapsed
                        card.Interval = (Math.Max(Math.Max(1, (int)(card.Interval * conf.GetNamedNumber("mult"))), (int)conf.GetNamedNumber("minInt")));
                    }
                    if (resched && card.OriginalDeckId != 0)
                    {
                        card.OriginalDue = today + 1;
                    }
                }

                int delay = DelayForGrade(conf, card.Left);
                if (card.Due < DateTimeOffset.Now.ToUnixTimeSeconds())
                {
                    // not collapsed; add some randomness
                    delay *= (1 + (new Random().Next(25) / 100));
                }
                card.Due = (int)(DateTimeOffset.Now.ToUnixTimeSeconds() + delay);

                // due today?
                if (card.Due < dayCutoff)
                {
                    learnCount += card.Left / 1000;
                    // if the queue is not empty and there's nothing else to do, make
                    // sure we don't put it at the head of the queue and end up showing
                    // it twice in a row
                    card.Queue = 1;
                    if ((learnQueue.Count != 0) && (reviewCount == 0) && (newCount == 0))
                    {
                        long smallestDue = learnQueue.First()[0];
                        card.Due = Math.Max(card.Due, smallestDue + 1);
                    }
                    SortIntoLearn(card.Due, card.Id);
                }
                else
                {
                    // the card is due in one or more days, so we need to use the day learn queue
                    long ahead = ((card.Due - dayCutoff) / 86400) + 1;
                    card.Due = today + ahead;
                    card.Queue = 3;
                }
            }
            LogLearn(card, (int)ease, conf, leaving, type, lastLeft);
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

        private void RescheduleAsRev(Card card, JsonObject conf, bool early)
        {
            bool lapse = (card.Type == CardType.Review);
            if (lapse)
            {
                if (Resched(card))
                {
                    card.Due = Math.Max(today + 1, card.OriginalDue);
                }
                else
                {
                    card.Due = card.OriginalDue;
                }
                card.OriginalDue = 0;
            }
            else
            {
                RescheduleNew(card, conf, early);
            }
            card.Queue = 2;
            card.Type = CardType.Review;
            // if we were dynamic, graduating means moving back to the old deck
            bool resched = Resched(card);
            if (card.OriginalDeckId != 0)
            {
                card.DeckId = card.OriginalDeckId;
                card.OriginalDue = 0;
                card.OriginalDeckId = 0;
                // if rescheduling is off, it needs to be set back to a new card
                if (!resched && !lapse)
                {
                    card.Type = 0;
                    card.Queue = (int)card.Type;
                    card.Due = collection.NextID("pos");
                }
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

        private int GraduatingIvl(Card card, JsonObject conf, bool early, bool adj = true)
        {
            if (card.Type == CardType.Review)
            {
                // lapsed card being relearnt
                if (card.OriginalDeckId != 0)
                {
                    if (conf.GetNamedBoolean("resched"))
                    {
                        return DynIntervalBoost(card);
                    }
                }
                return card.Interval;
            }
            int ideal;
            JsonArray ja;
            ja = conf.GetNamedArray("ints");
            if (!early)
            {
                // graduate
                ideal = (int)ja.GetNumberAt(0);
            }
            else
            {
                ideal = (int)ja.GetNumberAt(1);
            }
            if (adj)
            {
                return AdjustReviewInterval(card, ideal);
            }
            else
            {
                return ideal;
            }
        }

        private void RescheduleNew(Card card, JsonObject conf, bool early)
        {
            card.Interval = GraduatingIvl(card, conf, early);
            card.Due = today + card.Interval;
            card.Factor = (int)conf.GetNamedNumber("initialFactor");
        }

        private void LogLearn(Card card, int ease, JsonObject conf, bool leaving, int type, int lastLeft)
        {
            int lastIvl = -(DelayForGrade(conf, lastLeft));
            int ivl = leaving ? card.Interval : -(DelayForGrade(conf, card.Left));
            Log(card.Id, collection.Usn, ease, ivl, lastIvl, card.Factor, card.TimeTaken(), type);
        }

        private void Log(long id, int usn, int ease, int ivl, int lastIvl, int factor, int timeTaken, int type)
        {
            try
            {
                collection.Database.Execute("INSERT INTO revlog VALUES (?,?,?,?,?,?,?,?,?)",
                        DateTimeOffset.Now.ToUnixTimeMilliseconds(), id, usn, ease, ivl, lastIvl, factor, timeTaken, type);
            }
            // Duplicate pk; retry in 10ms
            catch (SQLite.Net.SQLiteException)
            {
                using (EventWaitHandle tmpEvent = new ManualResetEvent(false))
                {
                    tmpEvent.WaitOne(TimeSpan.FromMilliseconds(10));
                }
                Log(id, usn, ease, ivl, lastIvl, factor, timeTaken, type);
            }
        }

        /// <summary>
        /// Remove cards from the learning queues
        /// </summary>
        public void RemoveLearn()
        {
            RemoveLearn(null);
        }

        /// <summary>
        /// Remove cards from the learning queues
        /// </summary>
        /// <param name="ids"></param>
        private void RemoveLearn(long[] ids)
        {
            string extra;
            if (ids != null && ids.Length > 0)
            {
                extra = " AND id IN " + Utils.Ids2str(ids);
            }
            else
            {
                // benchmarks indicate it's about 10x faster to search all decks with the index than scan the table
                extra = " AND did IN " + Utils.Ids2str(collection.Deck.AllIds());
            }
            // review cards in relearning
            collection.Database.Execute(
                    "update cards set due = odue, queue = 2, mod = " + DateTimeOffset.Now.ToUnixTimeSeconds() +
                    ", usn = " + collection.Usn + ", odue = 0 where queue IN (1,3) and type = 2 " + extra);
            // new cards in learning
            var list = collection.Database.QueryColumn<CardIdOnlyTable>("SELECT id FROM cards WHERE queue IN (1,3) " + extra);
            long[] id = (from s in list select s.Id).ToArray();
            ResetCards(id);
        }

        private int DeckReviewLimit(long did)
        {
            return DeckNewLimit(did, this.DeckReviewLimitSingle);
        }

        private int DeckReviewLimitSingle(JsonObject d)
        {
            if (d.GetNamedNumber("dyn") != 0)
            {
                return reportLimit;
            }
            JsonObject c = collection.Deck.ConfForDeckId((long)d.GetNamedNumber("id"));
            return (int)Math.Max(0, c.GetNamedObject("rev").GetNamedNumber("perDay")
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
                        if (collection.Deck.Get(did).GetNamedNumber("dyn") != 0)
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

        private Card GetRevCard()
        {
            if (FillRev())
            {
                reviewCount -= 1;
                long result = reviewQueue.First();
                reviewQueue.RemoveFirst();
                return collection.GetCard(result);
            }
            else
            {
                return null;
            }
        }

        private void AnswerReviewCard(Card card, AnswerEase ease)
        {
            int delay = 0;
            if (ease == AnswerEase.Again)
            {
                delay = RescheduleLapse(card);
            }
            else
            {
                RescheduleReview(card, ease);
            }
            LogReview(card, (int)ease, delay);
        }

        private int RescheduleLapse(Card card)
        {
            JsonObject conf = LapseConf(card);
            card.LastInterval = card.Interval;
            if (Resched(card))
            {
                card.Lapses = card.Lapses + 1;
                card.Interval = NextLapseInterval(card, conf);
                card.Factor = Math.Max(1300, card.Factor - 200);
                card.Due = today + card.Interval;
                // if it's a filtered deck, update odue as well
                if (card.OriginalDeckId != 0)
                {
                    card.OriginalDue = card.Due;
                }
            }
            // if suspended as a leech, nothing to do
            int delay = 0;
            if (CheckLeech(card, conf) && card.Queue == -1)
            {
                return delay;
            }
            // if no relearning steps, nothing to do
            if (conf.GetNamedArray("delays").Count == 0)
            {
                return delay;
            }
            // record rev due date for later
            if (card.OriginalDue == 0)
            {
                card.OriginalDue = card.Due;
            }
            delay = DelayForGrade(conf, 0);
            card.Due = delay + DateTimeOffset.Now.ToUnixTimeSeconds();
            card.Left = StartingLeft(card);
            // queue 1
            if (card.Due < dayCutoff)
            {
                learnCount += card.Left / 1000;
                card.Queue = 1;
                SortIntoLearn(card.Due, card.Id);
            }
            else
            {
                // day learn queue
                long ahead = ((card.Due - dayCutoff) / 86400) + 1;
                card.Due = today + ahead;
                card.Queue = 3;
            }
            return delay;
        }

        private int NextLapseInterval(Card card, JsonObject conf)
        {
            return Math.Max((int)conf.GetNamedNumber("minInt"), (int)(card.Interval * conf.GetNamedNumber("mult")));
        }

        private void RescheduleReview(Card card, AnswerEase ease)
        {
            // update interval
            card.LastInterval = card.Interval;
            if (Resched(card))
            {
                UpdateReviewInterval(card, ease);
                // then the rest
                card.Factor = Math.Max(1300, card.Factor + FACTOR_ADD_VALUES[(int)ease - 2]);
                card.Due = today + card.Interval;
            }
            else
            {
                card.Due = card.OriginalDue;
            }
            if (card.OriginalDeckId != 0)
            {
                card.DeckId = card.OriginalDeckId;
                card.OriginalDeckId = 0;
                card.OriginalDue = 0;
            }
        }

        private void LogReview(Card card, int ease, int delay)
        {
            Log(card.Id, collection.Usn, ease, ((delay != 0) ? (-delay) : card.Interval), card.LastInterval,
                    card.Factor, card.TimeTaken(), 1);
        }

        private int NextReviewInterval(Card card, AnswerEase ease)
        {
            long delay = DaysLate(card);
            int interval = 0;
            JsonObject conf = RevConf(card);
            double fct = card.Factor / 1000.0;
            int ivl2 = ConstrainedInterval((int)((card.Interval + delay / 4) * 1.2), conf, card.Interval);
            int ivl3 = ConstrainedInterval((int)((card.Interval + delay / 2) * fct), conf, ivl2);
            int ivl4 = ConstrainedInterval((int)((card.Interval + delay) * fct * conf.GetNamedNumber("ease4")), conf, ivl3);
            if (ease == AnswerEase.Hard)
            {
                interval = ivl2;
            }
            else if (ease == AnswerEase.Good)
            {
                interval = ivl3;
            }
            else if (ease == AnswerEase.Easy)
            {
                interval = ivl4;
            }
            // interval capped?
            return Math.Min(interval, (int)conf.GetNamedNumber("maxIvl"));
        }

        private int FuzzedIvl(int ivl)
        {
            int[] minMax = FuzzedIvlRange(ivl);
            return new Random().Next(minMax[0], minMax[1]);
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

        private void UpdateReviewInterval(Card card, AnswerEase ease)
        {
            int idealIvl = NextReviewInterval(card, ease);
            card.Interval = AdjustReviewInterval(card, idealIvl);
        }

        private int AdjustReviewInterval(Card card, int idealIvl)
        {
            if (spreadRev)
            {
                idealIvl = FuzzedIvl(idealIvl);
            }
            return idealIvl;
        }

        public List<long> RebuildDyn(long did = 0)
        {
            if (did == 0)
            {
                did = collection.Deck.Selected();
            }
            JsonObject deck = collection.Deck.Get(did);
            if (deck.GetNamedNumber("dyn") == 0)
            {
                return null;
            }
            // move any existing cards back first, then fill
            EmptyDyn(did);
            List<long> ids = FillDyn(deck);
            if (ids.Count == 0)
            {
                return null;
            }
            // and change to our new deck
            collection.Deck.Select(did);
            return ids;
        }

        private List<long> FillDyn(JsonObject deck)
        {
            JsonArray terms;
            List<long> ids;

            terms = deck.GetNamedArray("terms").GetArrayAt(0);
            string search = terms.GetStringAt(0);
            int limit = (int)terms.GetNumberAt(1);
            int order = (int)terms.GetNumberAt(2);
            string orderlimit = DynOrder(order, limit);
            if (!String.IsNullOrEmpty(search.Trim()))
            {
                search = String.Format("({0})", search);
            }
            search = String.Format("{0} -is:suspended -is:buried -deck:filtered", search);
            ids = collection.FindCards(search, orderlimit);
            if (ids.Count == 0)
            {
                return ids;
            }
            // move the cards over
            collection.Log(args: new object[] { deck.GetNamedNumber("id"), ids });
            MoveToDyn((long)deck.GetNamedNumber("id"), ids);

            return ids;
        }

        public void EmptyDyn(long did, string lim = null)
        {
            if (lim == null)
            {
                lim = "did = " + did;
            }
            var list = collection.Database.QueryColumn<CardIdOnlyTable>("select id from cards where " + lim);
            collection.Log(args: (from s in list select s.Id).ToArray());
            // move out of cram queue
            collection.Database.Execute(
                "update cards set did = odid, queue = (case when type = 1 then 0 " +
                "else type end), type = (case when type = 1 then 0 else type end), " +
                "due = odue, odue = 0, odid = 0, usn = ? where " + lim,
                collection.Usn);
        }

        public void RemFromDyn(long[] cids)
        {
            EmptyDyn(0, "id IN " + Utils.Ids2str(cids) + " AND odid");
        }

        /// <summary>
        /// Generates the required SQL for order by and limit clauses, for dynamic decks.
        /// </summary>
        /// <param name="order"></param>
        /// <param name="limit"></param>
        /// <returns>The generated SQL to be suffixed to "select ... from ... order by "</returns>
        private string DynOrder(int order, int limit)
        {
            // if we don't understand the term, default to due order
            string t = "c.due";

            if ((order > 0) && (order <= (int)DynamicDeckOrder.DUEPRIORITY))
            {
                DynamicDeckOrder orderEnum = (DynamicDeckOrder)order;
                switch (orderEnum)
                {
                    case DynamicDeckOrder.OLDEST:
                        t = "c.mod";
                        break;
                    case DynamicDeckOrder.RANDOM:
                        t = "random()";
                        break;
                    case DynamicDeckOrder.SMALLINT:
                        t = "ivl";
                        break;
                    case DynamicDeckOrder.BIGINT:
                        t = "ivl desc";
                        break;
                    case DynamicDeckOrder.LAPSES:
                        t = "lapses desc";
                        break;
                    case DynamicDeckOrder.ADDED:
                        t = "n.id";
                        break;
                    case DynamicDeckOrder.REVADDED:
                        t = "n.id desc";
                        break;
                    case DynamicDeckOrder.DUE:
                        t = "c.due";
                        break;
                    case DynamicDeckOrder.DUEPRIORITY:
                        t = String.Format(Media.locale,
                                "(case when queue=2 and due <= {0} then (ivl / cast({1}-due+0.001 as real)) else 100000+due end)",
                                today, today);
                        break;
                }
            }
            return t + " limit " + limit;
        }

        private void MoveToDyn(long did, List<long> ids)
        {
            List<object[]> data = new List<object[]>();
            long t = DateTimeOffset.Now.ToUnixTimeSeconds();
            int u = collection.Usn;
            for (long c = 0; c < ids.Count; c++)
            {
                // start at -100000 so that reviews are all due
                data.Add(new Object[] { did, -100000 + c, u, ids[(int)c] });
            }
            // due reviews stay in the review queue. careful: can't use "odid or did", as sqlite converts to boolean
            String queue = "(CASE WHEN type = 2 AND (CASE WHEN odue THEN odue <= " + today +
                    " ELSE due <= " + today + " END) THEN 2 ELSE 0 END)";
            collection.Database.ExecuteMany(
                    "UPDATE cards SET odid = (CASE WHEN odid THEN odid ELSE did END), " +
                            "odue = (CASE WHEN odue THEN odue ELSE due END), did = ?, queue = " +
                            queue + ", due = ?, usn = ? WHERE id = ?", data);
        }

        private int DynIntervalBoost(Card card)
        {
            if (card.OriginalDeckId == 0 || card.Type != CardType.Review || card.Factor == 0)
                return 0;

            long elapsed = card.Interval - (card.OriginalDue - today);
            double factor = ((card.Factor / 1000.0) + 1.2) / 2.0;
            int ivl = Math.Max(1, Math.Max(card.Interval, (int)(elapsed * factor)));
            JsonObject conf = RevConf(card);
            return Math.Min((int)conf.GetNamedNumber("maxIvl"), ivl);
        }

        private bool CheckLeech(Card card, JsonObject conf)
        {
            int lf = (int)conf.GetNamedNumber("leechFails");
            if (lf == 0)
            {
                return false;
            }
            // if over threshold or every half threshold reps after that
            if (card.Lapses >= lf && ((card.Lapses - lf) % Math.Max(lf / 2, 1) == 0))
            {
                // add a leech tag
                Note n = card.LoadNote();
                n.Tags.Add("leech");
                n.SaveChangesToDatabase();

                // Java and python ver use hook to notify the GUI
                // Here we  use event     
                bool isCanSuspend = (bool)NotifyLeechEvent?.Invoke("leech", card);

                if (conf.GetNamedNumber("leechAction") == 0 && isCanSuspend)
                {
                    // if it has an old due, remove it from cram/relearning
                    if (card.OriginalDue != 0)
                    {
                        card.Due = card.OriginalDue;
                    }
                    if (card.OriginalDeckId != 0)
                    {
                        card.DeckId = card.OriginalDeckId;
                    }
                    card.OriginalDue = 0;
                    card.OriginalDeckId = 0;
                    card.Queue = -1;
                }
                //Hooks.Hooks.GetInstance().RunHook("leech", card);                
                return true;
            }
            return false;
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
            dict.Add("initialFactor", JsonValue.CreateNumberValue(oconf.GetNamedObject("new").GetNamedNumber("initialFactor")));
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
            dict.Add("minInt", JsonValue.CreateNumberValue(oconf.GetNamedObject("lapse").GetNamedNumber("minInt")));
            dict.Add("leechFails", JsonValue.CreateNumberValue(oconf.GetNamedObject("lapse").GetNamedNumber("leechFails")));
            dict.Add("leechAction", JsonValue.CreateNumberValue(oconf.GetNamedObject("lapse").GetNamedNumber("leechAction")));
            dict.Add("mult", JsonValue.CreateNumberValue(oconf.GetNamedObject("lapse").GetNamedNumber("mult")));
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
            if (conf.GetNamedNumber("dyn") == 0)
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
            if (oldToday != today)
            {
                collection.Log(args: new object[] { today, dayCutoff });
            }
            // update all daily counts, but don't save decks to prevent needless conflicts. we'll save on card answer
            // instead
            foreach (JsonObject deck in collection.Deck.All())
            {
                Update(deck);
            }
            // unbury if the day has rolled over
            int unburied = (int)collection.Conf.GetNamedNumber("lastUnburied", 0);
            if (unburied < today)
            {
                UnburyCards();
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

        public string FinishedMsg()
        {
            return ("Congratulations! You have finished this deck for now." + NextDueMsg());
        }

        public string NextDueMsg()
        {
            StringBuilder sb = new StringBuilder();
            if (HaveRevDue())
            {
                sb.Append("\n\n");
                sb.Append( "Today's review limit has been reached, " +
                            "but there are still cards waiting to be reviewed. For optimum memory, "  +
                            "consider increasing the daily limit in the options.");
            }
            if (HaveNewDue())
            {
                sb.Append("\n\n");
                sb.Append( "There are more new cards available, but the daily limit has been " + 
                            "reached. You can increase the limit in the options, but please " + 
                            "bear in mind that the more new cards you introduce, the higher " +
                            "your short-term review workload will become.");
            }
            if (HaveBuried())
            {
                string now;
                if (haveCustomStudy)
                {
                    now = " " + @"To see them now, click the Unbury button below.";
                }
                else
                {
                    now = "";
                }
                sb.Append("\n\n");
                sb.Append("" + "Some related or buried cards were delayed until a later session." + now);
            }
            if (haveCustomStudy && collection.Deck.Current().GetNamedNumber("dyn") == 0)
            {
                sb.Append("\n\n");
                sb.Append("To study outside of the normal schedule, click the Custom Study button below.");
            }
            return sb.ToString();
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

        public bool HaveBuried()
        {
            string sdids = Utils.Ids2str(collection.Deck.Active().ToArray());
            int cnt = collection.Database.QueryScalar<int>(String.Format(Media.locale,
                    "select 1 from cards where queue = -2 and did in {0} limit 1", sdids));
            return cnt != 0;
        }

        public string NextIntervalString(Card card, AnswerEase ease)
        {
            int ivl = NextIntervalInSeconds(card, ease);
            if (ivl == 0)
            {
                return "(end)";
            }

            string s = Utils.TimeQuantity(ivl);
            
            if (ivl < collection.Conf.GetNamedNumber("collapseTime"))
            {
                s = "<" + s;
            }

            return s;
        }

        /// <summary>
        /// Return the next interval for CARD in seconds
        /// </summary>
        /// <param name="card"></param>
        /// <param name="ease"></param>
        /// <returns></returns>
        public int NextIntervalInSeconds(Card card, AnswerEase ease)
        {
            if (card.Queue == 0 || card.Queue == 1 || card.Queue == 3)
            {
                return NextLearnInterval(card, ease);
            }
            else if (ease == AnswerEase.Again)
            {
                // lapsed
                JsonObject conf = LapseConf(card);
                if (conf.GetNamedArray("delays").Count > 0)
                {
                    return (int)(conf.GetNamedArray("delays").GetNumberAt(0) * 60.0);
                }
                return NextLapseInterval(card, conf) * 86400;
            }
            else
            {
                // review
                return NextReviewInterval(card, ease) * 86400;
            }
        }

        private int NextLearnInterval(Card card, AnswerEase ease)
        {
            // this isn't easily extracted from the learn code
            if (card.Queue == 0)
            {
                card.Left = StartingLeft(card);
            }
            JsonObject conf = LearnConf(card);
            if (ease == AnswerEase.Again)
            {
                // fail
                return DelayForGrade(conf, conf.GetNamedArray("delays").Count);
            }
            else if (ease == AnswerEase.Good)
            {
                // early removal
                if (!Resched(card))
                {
                    return 0;
                }
                return GraduatingIvl(card, conf, true, false) * 86400;
            }
            else
            {
                int left = card.Left % 1000 - 1;
                if (left <= 0)
                {
                    // graduate
                    if (!Resched(card))
                    {
                        return 0;
                    }
                    return GraduatingIvl(card, conf, false, false) * 86400;
                }
                else
                {
                    return DelayForGrade(conf, left);
                }
            }
        }

        public void SuspendCards(params long[] ids)
        {
            collection.Log(args: ids);
            RemFromDyn(ids);
            RemoveLearn(ids);
            collection.Database.Execute(
                    "UPDATE cards SET queue = -1, mod = " + DateTimeOffset.Now.ToUnixTimeSeconds()
                    + ", usn = " + collection.Usn + " WHERE id IN "
                    + Utils.Ids2str(ids));
        }

        public void UnsuspendCards(params long[] ids)
        {
            collection.Log(args: ids);
            collection.Database.Execute(
                    "UPDATE cards SET queue = type, mod = "
                    + DateTimeOffset.Now.ToUnixTimeSeconds()
                    + ", usn = " + collection.Usn
                    + " WHERE queue = -1 AND id IN " + Utils.Ids2str(ids));
        }

        public void BuryCards(long[] cids)
        {
            collection.Log(args: cids);
            RemFromDyn(cids);
            RemoveLearn(cids);
            collection.Database.Execute("update cards set queue=-2,mod=?,usn=? where id in " + Utils.Ids2str(cids),
                    new object[] { DateTimeOffset.Now.ToUnixTimeSeconds(), collection.Usn });
        }

        public void BuryNote(long nid)
        {
            var list = collection.Database.QueryColumn<CardIdOnlyTable>("SELECT id FROM cards WHERE nid = " 
                                                                + nid + " AND queue >= 0");
            var cids = (from s in list select s.Id).ToArray();
            BuryCards(cids);
        }

        private void BurySiblings(Card card)
        {
            LinkedList<long> toBury = new LinkedList<long>();
            JsonObject nconf = NewConf(card);
            //In java and python default is true
            bool buryNew = nconf.GetNamedBoolean("bury", true);
            JsonObject rconf = RevConf(card);
            bool buryRev = rconf.GetNamedBoolean("bury", true);
            var list = collection.Database.QueryColumn<CardTable>(String.Format(Media.locale,
                    "select id, queue from cards where nid={0} and id!={1} " +
                    "and (queue=0 or (queue=2 and due<={2}))", card.NoteId, card.Id, today));
            foreach (CardTable c in list)
            {
                long cid = c.Id;
                int queue = c.Queue;
                if (queue == 2)
                {
                    if (buryRev)
                        toBury.AddLast(cid);
                    // if bury disabled, we still discard to give same-day spacing
                    reviewQueue.Remove(cid);
                }
                else
                {
                    // if bury is disabled, we still discard to give same-day spacing
                    if (buryNew)
                        toBury.AddLast(cid);
                    newQueue.Remove(cid);
                }
            }
            // then bury
            if (toBury.Count > 0)
            {
                collection.Database.Execute("update cards set queue=-2,mod=?,usn=? where id in " + Utils.Ids2str(toBury.ToArray()),
                        new object[] { DateTimeOffset.Now.ToUnixTimeSeconds(), collection.Usn });
                collection.Log(args: toBury);
            }
        }

        /// <summary>
        /// Put cards at the end of the new queue
        /// </summary>
        /// <param name="ids"></param>
        public void ResetCards(long[] ids)
        {
            RemFromDyn(ids);
            collection.Database.Execute("update cards set type=0,queue=0,ivl=0,due=0,odue=0,factor=2500" +
                                        " where id in " + Utils.Ids2str(ids));
            int pmax = collection.Database.QueryScalar<int>("SELECT max(due) FROM cards WHERE type=0");
            // takes care of mod + usn
            SortCards(ids, pmax + 1);
            collection.Log(args: ids);
        }

        /// <summary>
        /// Put cards in review queue with a new interval in days (min, max)
        /// </summary>
        /// <param name="ids">The list of card ids to be affected</param>
        /// <param name="imin">imin the minimum interval (inclusive)</param>
        /// <param name="imax">imax The maximum interval (inclusive)</param>
        public void RescheduleIntoReviewCards(long[] ids, int imin, int imax)
        {
            List<object[]> d = new List<object[]>();
            int t = today;
            long mod = DateTimeOffset.Now.ToUnixTimeSeconds();
            Random rnd = new Random();
            foreach (long id in ids)
            {
                int r = rnd.Next(imin, imax);
                d.Add(new object[] { Math.Max(1, r), r + t, collection.Usn, mod, 2500, id });
            }
            RemFromDyn(ids);
            collection.Database.ExecuteMany(
                    "update cards set type=2,queue=2,ivl=?,due=?,odue=0, " +
                            "usn=?,mod=?,factor=? where id=?", d);
            collection.Log(args: ids);
        }

        /// <summary>
        /// Completely reset cards for export
        /// </summary>
        /// <param name="ids"></param>
        public void ResetCardsForExport(long[] ids)
        {
            var list = collection.Database.QueryColumn<CardIdOnlyTable>(
                String.Format("select id from cards where id in {0} and (queue != 0 or type != 0)", Utils.Ids2str(ids)));
            var nonNew = (from s in list select s.Id).ToArray();

            collection.Database.Execute("update cards set reps=0, lapses=0 where id in " + Utils.Ids2str(nonNew));
            ResetCards(nonNew);
            object[] obj = new object[] { ids };
            collection.Log(args: obj);
        }

        public void SortCards(long[] cids, int start, int step = 1, bool shuffle = false, bool shift = false)
        {
            string scids = Utils.Ids2str(cids);
            long now = DateTimeOffset.Now.ToUnixTimeSeconds();
            List<long> nids = new List<long>();
            foreach (long id in cids)
            {
                long nid = collection.Database.QueryScalar<long>("SELECT nid FROM cards WHERE id = " + id);
                if (!nids.Contains(nid))
                {
                    nids.Add(nid);
                }
            }
            if (nids.Count == 0)
            {
                // no new cards
                return;
            }
            // determine nid ordering
            Dictionary<long, long> due = new Dictionary<long, long>();
            if (shuffle)
            {
                Random r = new Random(today);
                nids.Shuffle<long>(r);
            }
            for (int c = 0; c < nids.Count; c++)
            {
                due.Add(nids[c], (long)(start + c * step));
            }
            int high = start + step * (nids.Count - 1);
            // shift?
            if (shift)
            {
                int low = collection.Database.QueryScalar<int>(
                        "SELECT min(due) FROM cards WHERE due >= " + start + " AND type = 0 AND id NOT IN " + scids);
                if (low != 0)
                {
                    int shiftby = high - low + 1;
                    collection.Database.Execute(
                            "UPDATE cards SET mod = " + now + ", usn = " + collection.Usn + ", due = due + " + shiftby
                                    + " WHERE id NOT IN " + scids + " AND due >= " + low + " AND queue = 0");
                }
            }
            List<object[]> d = new List<object[]>();
            var list = collection.Database.QueryColumn<CardTable>("SELECT id, nid FROM cards WHERE type = 0 AND id IN " + scids);
            foreach (CardTable c in list)
                d.Add(new object[] { due[c.Nid], now, collection.Usn, c.Id });

            collection.Database.ExecuteMany("UPDATE cards SET due = ?, mod = ?, usn = ? WHERE id = ?", d);
        }

        public void RandomizeCards(long did)
        {
            var list = collection.Database.QueryColumn<CardIdOnlyTable>("select id from cards where did = " + did);
            long[] cids = (from s in list select s.Id).ToArray();
            SortCards(cids, 1, 1, true, false);
        }

        public void OrderCards(long did)
        {
            var list = collection.Database.QueryColumn<CardIdOnlyTable>("SELECT id FROM cards WHERE did = " + did + " ORDER BY id");
            long[] cids = (from s in list select s.Id).ToArray();
            SortCards(cids, 1, 1, false, false);
        }

        public void ResortConf(JsonObject conf)
        {
            List<long> dids = collection.Deck.DeckIdsForConf(conf);
            foreach (long did in dids)
            {
                if (conf.GetNamedObject("new").GetNamedNumber("order") == 0)
                {
                    RandomizeCards(did);
                }
                else
                {
                    OrderCards(did);
                }
            }
        }

        public void MaybeRandomizeDeck(long? did = null)
        {
            if (did == null)
            {
                did = collection.Deck.Selected();
            }
            JsonObject conf = collection.Deck.ConfForDeckId((long)did);
            // in order due?
            if (conf.GetNamedObject("new").GetNamedNumber("order") == (double)NewCardInsertOrder.RANDOM)
            {
                RandomizeCards((long)did);
            }
        }

        //Not in python ver
        public bool HaveBuried(long did)
        {
            long odid = collection.Deck.Selected();
            collection.Deck.Select(did);
            bool buried = HaveBuried();
            collection.Deck.Select(odid);
            return buried;
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
            return conf.GetNamedNumber("leechAction") == 0;
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

        public override string ToString()
        {
            return String.Format(Media.locale, "{0}, {1}, {2}, {3}, {4}, {5}, {6}", 
                String.Join(" ", Names), DeckId, Depth, ReviewCount, LearnCount, NewCount, Children);
        }
    }


}

