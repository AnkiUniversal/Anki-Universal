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
    public class DeckGeneralOptions : INotifyPropertyChanged
    {
        public readonly int MAX_MAXTAKEN = 999;
        private int maxTaken;
        public int MaxTaken
        {
            get
            {
                return maxTaken;
            }
            set
            {
                if (value < 0)
                    maxTaken = 0;
                else if (value > MAX_MAXTAKEN)
                    maxTaken = MAX_MAXTAKEN;
                else
                    maxTaken = value;
                RaisePropertyChanged("MaxTaken");
            }
        }

        private bool showTimer;
        public bool ShowTimer
        {
            get
            {
                return showTimer;
            }
            set
            {
                showTimer = value;
                RaisePropertyChanged("ShowTimer");
            }
        }

        private bool autoPlay;
        public bool AutoPlay
        {
            get
            {
                return autoPlay;
            }
            set
            {
                autoPlay = value;
                RaisePropertyChanged("AutoPlay");
            }
        }

        public DeckGeneralOptions()
        {
            this.MaxTaken = 60;
            this.ShowTimer = false;
            this.AutoPlay = true;
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
