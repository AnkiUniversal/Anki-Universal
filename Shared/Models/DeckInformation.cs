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

using Shared.AnkiCore;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System;
using System.Threading.Tasks;
using Windows.Storage;
using System.Collections.Generic;
using Windows.UI.Xaml;

namespace Shared.Models
{
    public class DeckInformation 
    {
        public int NewCards { get; set; }
        public int DueCards { get; set; }
        public long Id { get; set; }

        public DeckInformation(int NewCards, int DueCards, long Id)
        {
            this.NewCards = NewCards;
            this.DueCards = DueCards;
            this.Id = Id;
        }

    }
}
