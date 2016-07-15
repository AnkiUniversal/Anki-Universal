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
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace AnkiU.Anki
{
    public class DeckInkPreferences : DeckPreferences<InkPreference>
    {
        public DeckInkPreferences(List<InkPreference> deckInkPrefList) 
            : base(deckInkPrefList)
        {
        }

        public void SetIsEnableInkToText(long deckId, bool isEnableInkToText)
        {
            deckPrefDict[deckId].IsInkToTextEnable = isEnableInkToText;
            ToUpdateToDatabaseDeckDict[deckId] = true;
        }

        public void SetIsAutoInkToTextEnable(long deckId, bool isAutomaticEnable)
        {
            deckPrefDict[deckId].IsAutoInkToTextEnable = isAutomaticEnable;
            ToUpdateToDatabaseDeckDict[deckId] = true;
        }

        public bool IsEnableInkToText(long deckId)
        {
            if (!HasId(deckId))
                return false;

            return deckPrefDict[deckId].IsInkToTextEnable;
        }

        public bool IsAutoInkToTextEnable(long deckId)
        {
            return deckPrefDict[deckId].IsAutoInkToTextEnable;
        }

    }
}
