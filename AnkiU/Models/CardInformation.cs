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

using AnkiU.AnkiCore;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace AnkiU.Models
{
    public class CardInformation : INotifyPropertyChanged
    {
        private long id;
        public long Id
        {
            get
            {
                return id;
            }
            set
            {
                id = value;
                RaisePropertyChanged("Id");
            }
        }

        private long noteId;
        public long NoteId
        {
            get
            {
                return noteId;
            }
            set
            {
                noteId = value;
                RaisePropertyChanged("NoteId");
            }
        }

        private long deckId;
        public long DeckId
        {
            get
            {
                return deckId;
            }
            set
            {
                deckId = value;
                RaisePropertyChanged("DeckId");
            }
        }

        private long outDeckId;
        public long OutDeckId
        {
            get
            {
                return outDeckId;
            }
            set
            {
                outDeckId = value;
                RaisePropertyChanged("OutDeckId");
            }
        }

        public long Due { get; set; }

        private string dueStr;
        public string DueStr
        {
            get
            {
                return dueStr;
            }
            set
            {
                dueStr = value;
                RaisePropertyChanged("DueStr");
            }
        }

        private int ord;
        public int Ord
        {
            get
            {
                return ord;
            }
            set
            {
                ord = value;
                RaisePropertyChanged("Ord");
            }
        }

        private CardType type;
        public CardType Type
        {
            get
            {
                return type;
            }
            set
            {
                type = value;
                RaisePropertyChanged("Type");
            }
        }

        private int interval;
        public int Interval
        {
            get
            {
                return interval;
            }
            set
            {
                interval = value;
                RaisePropertyChanged("Interval");
            }
        }

        private int queue;
        public int Queue
        {
            get
            {
                return queue;
            }
            set
            {
                queue = value;
                RaisePropertyChanged("Queue");
            }
        }

        private int lapses;
        public int Lapses
        {
            get
            {
                return lapses;
            }
            set
            {
                lapses = value;
                RaisePropertyChanged("Lapses");
            }
        }

        private string question;
        public string Question
        {
            get
            {
                return question;
            }
            set
            {
                question = value;
                RaisePropertyChanged("Question");
            }
        }

        private string answer;
        public string Answer
        {
            get
            {
                return answer;
            }
            set
            {
                answer = value;
                RaisePropertyChanged("Answer");
            }
        }

        private string sortField;
        public string SortField
        {
            get
            {
                return sortField;
            }
            set
            {
                sortField = value;
                RaisePropertyChanged("SortField");
            }
        }

        private long timeModified;
        public long TimeModified
        {
            get
            {
                return timeModified;
            }
            set
            {
                timeModified = value;
                TimeModifiedStr = DateTimeOffset.FromUnixTimeSeconds(timeModified).LocalDateTime.ToString();
            }
        }

        private string timeModifiedStr;
        public string TimeModifiedStr
        {
            get
            {
                return timeModifiedStr;
            }
            set
            {
                timeModifiedStr = value;
                RaisePropertyChanged("TimeModifiedStr");
            }
        }

        private string timeCreatedStr;
        public string TimeCreatedStr
        {
            get
            {
                return timeCreatedStr;
            }
            set
            {
                timeCreatedStr = value;
                RaisePropertyChanged("TimeCreatedStr");
            }
        }

        public CardInformation(Card c,  string dueStr, string question, string answer, string sortField, long noteTimeModified)
        {            
            this.id = c.Id;
            this.noteId = c.NoteId;
            this.deckId = c.DeckId;
            this.outDeckId = c.OriginalDeckId;           
            this.ord = c.Ord;
            this.type = c.Type;
            this.interval = c.Interval;
            this.queue = c.Queue;
            this.lapses = c.Lapses;
            this.question = question;
            this.answer = answer;
            this.dueStr = dueStr;
            this.Due = c.Due;
            this.sortField = sortField;                      
            this.timeCreatedStr = DateTimeOffset.FromUnixTimeMilliseconds(id).LocalDateTime.ToString();

            //Must use property to notify TimeModifiedStr
            this.TimeModified = noteTimeModified;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
