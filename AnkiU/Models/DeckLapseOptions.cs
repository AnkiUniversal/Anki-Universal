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
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;

namespace AnkiU.Models
{
    public class DeckLapseOptions : INotifyPropertyChanged
    {
        public readonly int MAX_NEWINTERVAL = 100;
        public readonly int MAX_MinInt = 99;
        public readonly int MAX_LEECHTHRESHOLD = 99;

        private string delays;
        public string Delays
        {
            get
            {
                return delays;
            }
            set
            {
                delays = value;
                RaisePropertyChanged("Delays");
            }
        }

        private int newInterval;
        public int NewInterval
        {
            get
            {
                return newInterval;
            }
            set
            {
                if (value < 0)
                    newInterval = 0;
                else if (value > MAX_NEWINTERVAL)
                    newInterval = MAX_NEWINTERVAL;
                else
                    newInterval = value;
                RaisePropertyChanged("NewInterval");
            }
        }

        private int minInt;
        public int MinInt
        {
            get
            {
                return minInt;
            }
            set
            {
                if (value < 0)
                    minInt = 0;
                else if (value > MAX_MinInt)
                    minInt = MAX_MinInt;
                else
                    minInt = value;
                RaisePropertyChanged("MinInt");
            }
        }        

        private int leechFailsThreshold;
        public int LeechFailsThreshold
        {
            get
            {
                return leechFailsThreshold;
            }
            set
            {
                if (value < 0)
                    leechFailsThreshold = 0;
                else if (value > MAX_LEECHTHRESHOLD)
                    leechFailsThreshold = MAX_LEECHTHRESHOLD;
                else
                    leechFailsThreshold = value;
                RaisePropertyChanged("LeechFailsThreshold");
            }
        }

        private int leechAction;
        public int LeechAction
        {
            get
            {
                return leechAction;
            }
            set
            {
                if (value < 0)
                    leechAction = 0;
                else if (value > 1)
                    leechAction = 1;
                else
                    leechAction = value;
                RaisePropertyChanged("LeechAction");
            }
        }

        public DeckLapseOptions()
        {
            Delays = "10";
            NewInterval = 0;
            MinInt = 1;
            LeechFailsThreshold = 8;
            LeechAction = 0;
        }

        public DeckLapseOptions(string delays, int newInterval, int minInt, int leechFailsThreshold, int leechAction)
        {
            Delays = delays;
            NewInterval = newInterval;
            MinInt = minInt;
            LeechFailsThreshold = leechFailsThreshold;
            LeechAction = leechAction;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override string ToString()
        {
            return this.GetType().Name;
        }
    }
}
