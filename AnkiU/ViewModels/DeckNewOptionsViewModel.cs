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
using AnkiU.Models;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace AnkiU.ViewModels
{
    class DeckNewOptionsViewModel : IAnkiDeckOptionsViewModel, INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private JsonObject config;
        public JsonObject Config
        {
            get { return config; }
            set
            {
                config = value;
                RaisePropertyChanged("Config");
            }
        }

        private DeckNewOptions options;
        public DeckNewOptions Options
        {
            get { return options; }
            set
            {
                options = value;
                RaisePropertyChanged("Config");
            }
        }

        public DeckNewOptionsViewModel(JsonObject config)
        {
            this.Config = config.GetNamedObject("new");
            Options = new DeckNewOptions();
        }

        public void GetOptionsToView()
        {
            try
            {
                Options.Delays = Utils.JsonNumberArrayToString(Config.GetNamedArray("delays"));
                Options.GraduatingInterval = (int)Config.GetNamedArray("ints").GetNumberAt(0);
                Options.EasyInterval = (int)Config.GetNamedArray("ints").GetNumberAt(1);
                Options.InitialFactor = (int)(Config.GetNamedNumber("initialFactor") / 10);
                Options.Order = (int)Config.GetNamedNumber("order");
                Options.PerDay = (int)Config.GetNamedNumber("perDay");
                Options.Bury = Config.GetNamedBoolean("bury", false);                
            }
            catch //If any error happen we back to default
            {
                Options = new DeckNewOptions();
            }            
        }

        public void SaveOptionsToJsonConfig()
        {            
            Config["delays"] = Utils.StringNumberToJsonArray(Options.Delays);

            JsonArray jsonArray = new JsonArray();
            jsonArray.Add(JsonValue.CreateNumberValue(Options.GraduatingInterval));
            jsonArray.Add(JsonValue.CreateNumberValue(Options.EasyInterval));
            Config["ints"] = jsonArray;

            Config["initialFactor"] = JsonValue.CreateNumberValue(Options.InitialFactor*10);
            Config["order"] = JsonValue.CreateNumberValue(Options.Order);
            Config["perDay"] = JsonValue.CreateNumberValue(Options.PerDay);
            Config["bury"] = JsonValue.CreateBooleanValue(Options.Bury);
        }
    }
}
