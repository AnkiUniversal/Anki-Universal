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
    public class DeckSimpleOptions : INotifyPropertyChanged
    {
        public readonly int MAX_PERDAY = 9999;        
        public readonly int MAX_IVKFCT = 999;
        public readonly int MAX_LEECHTHRESHOLD = 99;

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

        private int newcardOrder;
        public int NewcardOrder
        {
            get
            {
                return newcardOrder;
            }
            set
            {
                if (value <= 0)
                    newcardOrder = 0;
                else
                    newcardOrder = 1;
                RaisePropertyChanged("NewcardOrder");
            }
        }

        private int newCardPerDay;
        public int NewCardPerDay
        {
            get
            {
                return newCardPerDay;
            }
            set
            {
                if (value < 0)
                    newCardPerDay = 0;
                else if (value > MAX_PERDAY)
                    newCardPerDay = MAX_PERDAY;
                else
                    newCardPerDay = value;
                RaisePropertyChanged("NewCardPerDay");
            }
        }

        private int reviewCardPerDay;
        public int ReviewCardPerDay
        {
            get
            {
                return reviewCardPerDay;
            }
            set
            {
                if (value < 0)
                    reviewCardPerDay = 0;
                else if (value > MAX_PERDAY)
                    reviewCardPerDay = MAX_PERDAY;
                else
                    reviewCardPerDay = value;
                RaisePropertyChanged("ReviewCardPerDay");
            }
        }

        private bool buryRelatedReviewCard;
        public bool BuryRelatedReviewCard
        {
            get
            {
                return buryRelatedReviewCard;
            }
            set
            {
                buryRelatedReviewCard = value;
                RaisePropertyChanged("BuryRelatedReviewCard");
            }
        }

        private bool buryRelatedNewCard;
        public bool BuryRelatedNewCard
        {
            get
            {
                return buryRelatedNewCard;
            }
            set
            {
                buryRelatedNewCard = value;
                RaisePropertyChanged("BuryRelatedNewCard");
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

        public DeckSimpleOptions()
        {
            AutoPlay = true;

            NewcardOrder = (int)NewCardInsertOrder.DUE;
            NewCardPerDay = 20;
            BuryRelatedNewCard = false;

            ReviewCardPerDay = 100;
            IvlFct = 100;
            BuryRelatedReviewCard = false;

            LeechFailsThreshold = 8;
            LeechAction = 0;
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
