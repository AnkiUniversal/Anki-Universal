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
    public class DeckNewOptions : INotifyPropertyChanged
    {
        public readonly int MAX_GRADINTERVAL = 99;
        public readonly int MAX_PERDAY = 9999;
        public readonly int MAX_INITFACTOR = 999;
        public readonly int MAX_EASYINTERVAL = 99;

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

        private int graduatingInterval;
        public int GraduatingInterval
        {
            get
            {
                return graduatingInterval;
            }
            set
            {
                if (value < 0)
                    graduatingInterval = 0;
                else if (value > MAX_GRADINTERVAL)
                    graduatingInterval = MAX_GRADINTERVAL;
                else
                    graduatingInterval = value;
                RaisePropertyChanged("GraduatingInterval");
            }
        }        

        private int easyInterval;
        public int EasyInterval
        {
            get
            {
                return easyInterval;
            }
            set
            {
                if (value < 0)
                    easyInterval = 0;
                else if (value > MAX_EASYINTERVAL)
                    easyInterval = MAX_EASYINTERVAL;
                else
                    easyInterval = value;
                RaisePropertyChanged("EasyInterval");
            }
        }       

        private int initialFactor;
        public int InitialFactor
        {
            get
            {
                return initialFactor;
            }
            set
            {
                if (value < 0)
                    initialFactor = 0;
                else if (value > MAX_INITFACTOR)
                    initialFactor = MAX_INITFACTOR;
                else
                    initialFactor = value;
                RaisePropertyChanged("InitialFactor");
            }
        }

        private int order;
        public int Order
        {
            get
            {
                return order;
            }
            set
            {
                if (value <= 0)
                    order = 0;
                else 
                    order = 1;
                RaisePropertyChanged("Order");
            }
        }        

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

        public DeckNewOptions()
        {
            this.Delays = "1 10";
            this.GraduatingInterval = 1;
            this.EasyInterval = 4;
            this.InitialFactor = 250;
            this.Order = (int) NewCardInsertOrder.DUE;
            this.PerDay = 20;
            this.Bury = false;
        }

        public DeckNewOptions(string delays, int graduatingInterval, int easyInterval, int initialFactor, int order, int perDay, bool bury)
        {
            this.Delays = delays;
            this.GraduatingInterval = graduatingInterval;
            this.EasyInterval = easyInterval;
            this.InitialFactor = initialFactor;
            this.Order = order;
            this.PerDay = perDay;
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

