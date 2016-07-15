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
    public class InkPreference : IDeckPreference
    { 
        [SQLite.Net.Attributes.PrimaryKey, SQLite.Net.Attributes.Column("Id")]
        public long Id { get; set; }

        private bool isInkToTextEnable;
        [SQLite.Net.Attributes.Column("IsInkToTextEnable")]
        public bool IsInkToTextEnable
        { get
            {
                return isInkToTextEnable;
            }
            set
            {
                isInkToTextEnable = value;
            }
        }

        private bool isAutoInkToTextEnable;
        [SQLite.Net.Attributes.Column("IsAutoInkToTextEnable")]
        public bool IsAutoInkToTextEnable
        {
            get
            {
                return isAutoInkToTextEnable;
            }
            set
            {
                isAutoInkToTextEnable = value;
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

        public InkPreference()
        {
            Id = 0;
            IsInkToTextEnable = false;
            IsAutoInkToTextEnable = true;
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
