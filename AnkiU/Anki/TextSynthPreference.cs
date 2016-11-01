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
using AnkiU.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace AnkiU.Anki
{
    public class TextSynthPreference : IDeckPreference
    {
        [SQLite.Net.Attributes.PrimaryKey, SQLite.Net.Attributes.Column("Id")]
        public long Id { get; set; }        

        private double voiceSpeed;
        [SQLite.Net.Attributes.Column("VoiceSpeed")]
        public double VoiceSpeed
        {
            get
            {
                return voiceSpeed;
            }
            set
            {
                voiceSpeed = value;
            }
        }

        private string voiceId;
        [SQLite.Net.Attributes.Column("VoiceId")]
        public string VoiceId
        {
            get
            {
                return voiceId;
            }
            set
            {
                voiceId = value;
            }
        }

        private JsonObject otherPreferencesJson;
        [SQLite.Net.Attributes.Column("Preferences")]
        public string Preferences
        {
            get
            {
                if (otherPreferencesJson != null)
                    return Utils.JsonToString(otherPreferencesJson);
                else
                    return "{}";
            }
            set
            {

                otherPreferencesJson = JsonObject.Parse(value);
            }
        }

        public TextSynthPreference()
        {
            Id = 0;
            voiceId = "";
            voiceSpeed = 1;            
        }

        public JsonValue GetPreferenceJson(string name)
        {
            return otherPreferencesJson.GetNamedValue(name);
        }

        public void AddOrChangePreferenceJson(string name, JsonValue value)
        {
            otherPreferencesJson[name] = value;
        }
    }
}
