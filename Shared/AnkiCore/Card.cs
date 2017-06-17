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
using System.Diagnostics;
using Windows.Data.Json;
using System.Reflection;

/** Comment from java ver:
A Card is the ultimate entity subject to review; it encapsulates the scheduling parameters (from which to derive
the next interval), the note it is derived from (from which field data is retrieved), its own ownership (which deck it
currently belongs to), and the retrieval of presentation elements (filled-in templates).

Card presentation has two components: the question (front) side and the answer (back) side. The presentation of the
card is derived from the template of the card's Card Type. The Card Type is a component of the Note Type (see Models)
that this card is derived from.

This class is responsible for:
- Storing and retrieving database entries that map to Cards in the Collection
- Providing the HTML representation of the Card's question and answer
- Recording the results of review (answer chosen, time taken, etc)

It does not:
- Generate new cards (see Collection)
- Store the templates or the style sheet (see Models)

WARNING: Unknown the function of Queue = 3 so we can't convert this to enum
Queue: same as above, and:
-1=suspended, -2=user buried, -3=sched buried
Due is used differently for different queues.
- new queue: note id or random int
- rev queue: integer day
- lrn queue: integer timestamp
*/
namespace Shared.AnkiCore
{
    public enum CardType
    {
        New = 0,
        Learn = 1,
        Review = 2
    }

    public class Card
    {
        private static System.Globalization.CultureInfo locale = new System.Globalization.CultureInfo("en-US");

        private Collection collection;
        public Collection Collection { get { return collection; } set { collection = value; } }

        private double timerStarted;

        // Not in LibAnki. Record time spent reviewing in order to restore when resuming.
        private double elapsedTime;

        #region BEGIN SQL table entries
        private long id;
        private long noteId;
        private long deckId;
        private int ord;
        private long timeModified;
        private int usn;
        private CardType type;
        private int queue;
        private long due;
        private int interval;
        private int factor;
        private int reps;
        private int lapses;
        private int left;
        private long oDue;
        private long oDid;
        private int flags;
        private string data;

        public long Id { get { return id; } set { id = value; } }
        public long NoteId { get { return noteId; } set { noteId = value; } }
        public long DeckId { get { return deckId; } set { deckId = value; } }
        public int Ord { get { return ord; } set { ord = value; } }
        public long TimeModified { get { return timeModified; } set { timeModified = value; } }
        public int Usn { get { return usn; } set { usn = value; } }
        public CardType Type { get { return type; } set { type = value; } }
        public int Queue { get { return queue; } set { queue = value; } }
        public long Due { get { return due; } set { due = value; } }
        public int Interval { get { return interval; } set { interval = value; } }
        public int Factor { get { return factor; } set { factor = value; } }
        public int Reps { get { return reps; } set { reps = value; } }
        public int Lapses { get { return lapses; } set { lapses = value; } }
        public int Left { get { return left; } set { left = value; } }
        public long OriginalDue { get { return oDue; } set { oDue = value; } }
        public long OriginalDeckId { get { return oDid; } set { oDid = value; } }
        public int Flags { get { return flags; } }
        public string Data { get { return data; } }
        #endregion

        // Used by Sched to determine which queue to move the card to after answering.
        private bool wasNew;
        public bool WasNew { get { return wasNew; } set { wasNew = value; } }
        // Used by Sched to record the original interval in the revlog after answering.
        private int lastInterval;
        public int LastInterval { get { return lastInterval; } set { lastInterval = value; } }

        public Card(Collection col, long? id = null)
        {
            collection = col;
            timerStarted = Double.NaN;
            if (id != null)
            {
                this.id = (long)id;
                LoadFromDatabase();
            }
            else
            {
                // to flush, set nid, ord, and due
                this.id = Utils.TimestampID(collection.Database, "cards");
                deckId = 1;
                type = CardType.New;
                queue = 0;
                interval = 0;
                factor = 0;
                reps = 0;
                lapses = 0;
                left = 0;
                oDue = 0;
                oDid = 0;
                flags = 0;
                data = "";
            }
        }

        public void LoadFromDatabase()
        {
            var list = collection.Database.QueryColumn<CardTable>("SELECT * FROM cards WHERE id = ?", id);
            if (list == null || list.Count == 0)
            {
                throw new NoCardException(" No card with id " + id);
            }
            id = list[0].Id;
            noteId = list[0].Nid;
            deckId = list[0].Did;
            ord = list[0].Ord;
            timeModified = list[0].Mod;
            usn = list[0].Usn;
            type = (CardType)list[0].Type;
            queue = list[0].Queue;
            due = list[0].Due;
            interval = list[0].Interval;
            factor = list[0].Factor;
            reps = list[0].Reps;
            lapses = list[0].Lapses;
            left = list[0].Left;
            oDue = list[0].ODue;
            oDid = list[0].ODid;
            flags = list[0].Flags;
            data = list[0].Data;
        }

        public void StartTimer()
        {
            timerStarted = DateTimeOffset.Now.ToUnixTimeSeconds();
        }

        /// <summary>
        /// Time limit for answering in milliseconds
        /// </summary>
        /// <returns></returns>
        public int TimeLimit()
        {
            JsonObject conf = collection.Deck.ConfForDeckId(oDid == 0 ? deckId : oDid);
            return (int)JsonHelper.GetNameNumber(conf,"maxTaken") * 1000;
        }

        public bool ShouldShowTimer()
        {
            JsonObject conf = collection.Deck.ConfForDeckId(oDid == 0 ? deckId : oDid);
            return JsonHelper.GetNameNumber(conf,"timer") != 0;
        }

        /// <summary>
        /// Time taken to answer card, in integer MS
        /// </summary>
        /// <returns></returns>
        public int TimeTaken()
        {
            int total = (int)((DateTimeOffset.Now.ToUnixTimeSeconds() - timerStarted) * 1000);
            return Math.Min(total, TimeLimit());
        }

        /// <summary>
        /// Save the currently elapsed reviewing time so it can be restored on resume.
        /// Use this method whenever a review session (activity) has been paused. Use the resumeTimer()
        /// method when the session resumes to start counting review time again.
        /// </summary>
        public void StopTimer()
        {
            elapsedTime = DateTimeOffset.Now.ToUnixTimeSeconds() - timerStarted;
        }

        /// <summary>
        /// Resume the timer that counts the time spent reviewing this card.
        /// Unlike the desktop client, AnkiDroid must pause and resume the process in the middle of
        /// reviewing.This method is required to keep track of the actual amount of time spent in
        /// the reviewer and* must* be called on resume before any calls to timeTaken() take place
        /// or the result of timeTaken() will be wrong.
        /// </summary>
        public void resumeTimer()
        {
            timerStarted = DateTimeOffset.Now.ToUnixTimeSeconds() - elapsedTime;
        }

        public void setTimerStarted(double timeStarted)
        {
            this.timerStarted = timeStarted;
        }

        public bool ShowTimer()
        {
            var conf = collection.Deck.ConfForDeckId(oDid == 0 ? deckId : oDid);
            return JsonHelper.GetNameNumber(conf, "timer", 1) != 0;
        }

        public Card ShallowClone()
        {
            var clone = this.MemberwiseClone() as Card;
            if (clone == null)
                throw new Exception("Can not clone this Card!");
            return clone;
        }

        // A list of class members to skip in the ToString() representation
        public static HashSet<string> SKIP_PRINT = new HashSet<string>(new string[] {"SKIP_PRINT", "$assertionsDisabled", "TYPE_LRN",
                "TYPE_NEW", "TYPE_REV", "mNote", "mQA", "mCol", "mTimerStarted", "mTimerStopped"});

    }

    public class NoCardException : Exception
    {
        public NoCardException() : base() { }
        public NoCardException(string message) : base(message) { }
    }

    /// <summary>
    /// Class used to get only ID from cards table in database.
    /// </summary>
    [SQLite.Net.Attributes.Table("cards")]
    public class CardIdOnlyTable
    {
        [SQLite.Net.Attributes.Column("id")]
        public long Id { get; set; }
    }

    /// <summary>
    /// Class used to get data from cards table in database.
    /// Avoid using Card class to reduce unwanted fields.
    /// </summary>
    [SQLite.Net.Attributes.Table("cards")]
    public class CardTable
    {
        [SQLite.Net.Attributes.Column("id")]
        public long Id { get; set; }

        [SQLite.Net.Attributes.Column("nid")]
        public long Nid { get; set; }

        [SQLite.Net.Attributes.Column("did")]
        public long Did { get; set; }

        [SQLite.Net.Attributes.Column("ord")]
        public int Ord { get; set; }

        [SQLite.Net.Attributes.Column("mod")]
        public long Mod { get; set; }

        [SQLite.Net.Attributes.Column("usn")]
        public int Usn { get; set; }

        [SQLite.Net.Attributes.Column("type")]
        public int Type { get; set; }

        [SQLite.Net.Attributes.Column("queue")]
        public int Queue { get; set; }

        [SQLite.Net.Attributes.Column("due")]
        public long Due { get; set; }

        [SQLite.Net.Attributes.Column("ivl")]
        public int Interval { get; set; }

        [SQLite.Net.Attributes.Column("factor")]
        public int Factor { get; set; }

        [SQLite.Net.Attributes.Column("reps")]
        public int Reps { get; set; }

        [SQLite.Net.Attributes.Column("lapses")]
        public int Lapses { get; set; }

        [SQLite.Net.Attributes.Column("left")]
        public int Left { get; set; }

        [SQLite.Net.Attributes.Column("odue")]
        public long ODue { get; set; }

        [SQLite.Net.Attributes.Column("odid")]
        public long ODid { get; set; }

        [SQLite.Net.Attributes.Column("flags")]
        public int Flags { get; set; }

        [SQLite.Net.Attributes.Column("data")]
        public string Data { get; set; }
    }
}
