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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnkiU.Anki
{
    public class DeckTextSynthPreferences : DeckPreferences<TextSynthPreference>
    {
        public DeckTextSynthPreferences(List<TextSynthPreference> deckInkPrefList) 
            : base(deckInkPrefList)
        {
        }

        public string GetVoiceId(long deckId)
        {
            return deckPrefDict[deckId].VoiceId;
        }

        public double GetVoiceSpeed(long deckId)
        {
            return deckPrefDict[deckId].VoiceSpeed;
        }

        public void SetVoiceId(long deckId, string voiceId)
        {
            deckPrefDict[deckId].VoiceId = voiceId;
            ToUpdateToDatabaseDeckDict[deckId] = true;
        }

        public void SetVoiceSpeed(long deckId, double voiceSpeed)
        {
            deckPrefDict[deckId].VoiceSpeed = voiceSpeed;
            ToUpdateToDatabaseDeckDict[deckId] = true;
        }
    }
}
