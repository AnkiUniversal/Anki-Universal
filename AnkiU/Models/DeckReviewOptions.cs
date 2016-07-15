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
    public class DeckReviewOptions : INotifyPropertyChanged
    {
        public readonly int MAX_PERDAY = 9999;
        public readonly int MAX_EASYBONUS = 1000;
        public readonly int MAX_IVKFCT = 999;
        public readonly int MAX_MAXIVL = 99999;

        public readonly int MIN_EASYBONUS = 100;

        private int perDay;
        public int PerDay
        {
            get
            {
                return perDay;
            }
            set
            {
                if (value < 0)
                    perDay = 0;
                else if (value > MAX_PERDAY)
                    perDay = MAX_PERDAY;
                else
                    perDay = value;
                RaisePropertyChanged("PerDay");
            }
        }

        private int easyBonus;
        public int EasyBonus
        {
            get
            {
                return easyBonus;
            }
            set
            {
                if (value < MIN_EASYBONUS)
                    easyBonus = MIN_EASYBONUS;
                else if (value > MAX_EASYBONUS)
                    easyBonus = MAX_EASYBONUS;
                else
                    easyBonus = value;
                RaisePropertyChanged("EasyBonus");
            }
        }

        private int ivlFct;
        public int IvlFct
        {
            get
            {
                return ivlFct;
            }
            set
            {
                if (value < 0)
                    ivlFct = 0;
                else if (value > MAX_IVKFCT)
                    ivlFct = MAX_IVKFCT;
                else
                    ivlFct = value;
                RaisePropertyChanged("IvlFct");
            }
        }

        private int maxIvl;
        public int MaxIvl
        {
            get
            {
                return maxIvl;
            }
            set
            {
                if (value < 0)
                    maxIvl = 0;
                else if (value > MAX_MAXIVL)
                    maxIvl = MAX_MAXIVL;
                else
                    maxIvl = value;
                RaisePropertyChanged("MaxIvl");
            }
        }

        private bool bury;
        public bool Bury
        {
            get
            {
                return bury;
            }
            set
            {
                bury = value;
                RaisePropertyChanged("Bury");
            }
        }

        public DeckReviewOptions()
        {
            this.PerDay = 100;
            this.EasyBonus = 130;
            this.IvlFct = 100;
            this.MaxIvl = 36500;
            this.Bury = false;
        }

        public DeckReviewOptions(int perDay,  int easyBonus, int ivlFct, int maxIvl, bool bury)
        {
            this.PerDay = perDay;
            this.EasyBonus = easyBonus;
            this.IvlFct = ivlFct;
            this.MaxIvl = maxIvl;
            this.Bury = bury;
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
