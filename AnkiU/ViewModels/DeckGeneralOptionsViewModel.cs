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
    public class DeckGeneralOptionsViewModel : IAnkiDeckOptionsViewModel, INotifyPropertyChanged
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

        private DeckGeneralOptions options;
        public DeckGeneralOptions Options
        {
            get { return options; }
            set
            {
                options = value;
                RaisePropertyChanged("Config");
            }
        }

        public DeckGeneralOptionsViewModel(JsonObject config)
        {
            this.Config = config;
            Options = new DeckGeneralOptions();
        }        

        public void GetOptionsToView()
        {
            try
            {                
                Options.MaxTaken = (int)Config.GetNamedNumber("maxTaken");

                //python ver use number instead of bool
                var isTimer = Config.GetNamedNumber("timer", 0);
                if (isTimer > 0)
                    Options.ShowTimer = true;
                else
                    Options.ShowTimer = false;

                Options.AutoPlay = Config.GetNamedBoolean("autoplay");                
            }
            catch //If any error happen we back to default
            {
                Options = new DeckGeneralOptions();
            }            
        }

        public void SaveOptionsToJsonConfig()
        {            
            Config["maxTaken"] = JsonValue.CreateNumberValue(Options.MaxTaken);

            //python ver use number instead of bool                
            if (Options.ShowTimer)
                Config["timer"] = JsonValue.CreateNumberValue(1);
            else
                Config["timer"] = JsonValue.CreateNumberValue(0);

            Config["autoplay"] = JsonValue.CreateBooleanValue(Options.AutoPlay);                
        }
    }
}
