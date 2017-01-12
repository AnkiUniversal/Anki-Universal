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
    public class CollectionOptions 
    {     
        private bool isShowDueCount;
        public bool IsShowDueCount
        {
            get
            {
                return isShowDueCount;
            }
            set
            {
                isShowDueCount = value;
            }
        }

        private bool isShowEstTime;
        public bool IsShowEstTime
        {
            get
            {
                return isShowEstTime;
            }
            set
            {
                isShowEstTime = value;
            }
        }

        private int collapseTime;
        public int CollapseTime
        {
            get
            {
                return collapseTime;
            }
            set
            {
                collapseTime = value;
            }
        }

        private AnkiCore.ReviewType reviewType;
        public int ReviewType
        {
            get
            {
                return (int)reviewType;
            }
            set
            {
                switch(value)
                {
                    case 0:
                        reviewType = AnkiCore.ReviewType.DISTRIBUTE;
                        break;
                    case 1:
                        reviewType = AnkiCore.ReviewType.LAST;
                        break;
                    case 2:
                        reviewType = AnkiCore.ReviewType.FIRST;
                        break;                    
                    default:
                        reviewType = AnkiCore.ReviewType.DISTRIBUTE;
                        break;
                }                
            }
        }

        private bool isTTSAutoplay;
        public bool IsTTSAutoplay
        {
            get
            {
                return isTTSAutoplay;
            }
            set
            {
                isTTSAutoplay = value;
            }
        }

        private bool isEnableNotification;
        public bool IsEnableNotification
        {
            get
            {
                return isEnableNotification;
            }
            set
            {
                isEnableNotification = value;
            }
        }

        public CollectionOptions()
        {
            IsShowDueCount = true;
            IsShowEstTime = true;
            ReviewType = (int)AnkiCore.ReviewType.DISTRIBUTE;
            IsTTSAutoplay = false;
            CollapseTime = 20;
            IsEnableNotification = true;
        }

        public override string ToString()
        {
            return this.GetType().Name;
        }

        public bool IsTheSame(CollectionOptions compared)
        {
            if (compared.IsShowDueCount != IsShowDueCount)
                return false;
            if (compared.IsShowEstTime != IsShowEstTime)
                return false;
            if (compared.ReviewType != ReviewType)
                return false;
            if (compared.IsTTSAutoplay != IsTTSAutoplay)
                return false;
            if (compared.CollapseTime != CollapseTime)
                return false;
            if (compared.IsEnableNotification != IsEnableNotification)
                return false;

            return true;
        }

    }
}
