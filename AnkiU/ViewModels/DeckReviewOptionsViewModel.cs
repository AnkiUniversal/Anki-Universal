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
    public class DeckReviewOptionsViewModel : IAnkiDeckOptionsViewModel, INotifyPropertyChanged
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


        public DeckReviewOptionsViewModel(JsonObject config)
        {
            this.Config = config.GetNamedObject("rev");
            Options = new DeckReviewOptions();
        }
        
        private DeckReviewOptions options;
        public DeckReviewOptions Options
        {
            get { return options; }
            set
            {
                options = value;
                RaisePropertyChanged("Config");
            }
        }


        public void GetOptionsToView()
        {
            try
            {                
                Options.PerDay = (int)Config.GetNamedNumber("perDay");
                Options.EasyBonus = (int)(Config.GetNamedNumber("ease4") * 100);
                Options.IvlFct = (int)(Config.GetNamedNumber("ivlFct")*100);
                Options.MaxIvl = (int)Config.GetNamedNumber("maxIvl");
                Options.Bury = Config.GetNamedBoolean("bury");                
            }
            catch //If any error happen we back to default
            {
                Options = new DeckReviewOptions();
            }            
        }

        public void SaveOptionsToJsonConfig()
        {            
            Config["perDay"] = JsonValue.CreateNumberValue(Options.PerDay);
            Config["ease4"] = JsonValue.CreateNumberValue(Options.EasyBonus/100.0);
            Config["ivlFct"] = JsonValue.CreateNumberValue(Options.IvlFct/100.0);
            Config["maxIvl"] = JsonValue.CreateNumberValue(Options.MaxIvl);
            Config["bury"] = JsonValue.CreateBooleanValue(Options.Bury);
        }
    }
}
