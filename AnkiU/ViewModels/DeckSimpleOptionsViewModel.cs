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
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace AnkiU.ViewModels
{
    public class DeckSimpleOptionsViewModel : IAnkiDeckOptionsViewModel, INotifyPropertyChanged
    {        
        private DeckSimpleOptions options;
        public DeckSimpleOptions Options
        {
            get
            {
                return options;
            }
            set
            {
                options = value;
                RaisePropertyChanged("Options");
            }
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private JsonObject config;
        public JsonObject Config
        {
            get
            {
                return config;
            }
            set
            {
                config = value;
                RaisePropertyChanged("Config");
            }
        }

        public DeckSimpleOptionsViewModel(JsonObject config)
        {
            this.Config = config;
            Options = new DeckSimpleOptions();
        }

        public void GetOptionsToView()
        {
            try
            {
                Options.AutoPlay = config.GetNamedBoolean("autoplay"); 
                var newObject = config.GetNamedObject("new");
                Options.NewcardOrder = (int)JsonHelper.GetNameNumber(newObject,"order");
                Options.NewCardPerDay = (int)JsonHelper.GetNameNumber(newObject, "perDay");
                Options.BuryRelatedNewCard = newObject.GetNamedBoolean("bury", false);

                var revObject = config.GetNamedObject("rev");
                Options.ReviewCardPerDay = (int)JsonHelper.GetNameNumber(revObject,"perDay");
                Options.IvlFct = (int)(JsonHelper.GetNameNumber(revObject, "ivlFct") * 100);
                Options.BuryRelatedReviewCard = revObject.GetNamedBoolean("bury");

                var lapseObject = config.GetNamedObject("lapse");
                Options.LeechFailsThreshold = (int)JsonHelper.GetNameNumber(lapseObject,"leechFails");
                Options.LeechAction = (int)JsonHelper.GetNameNumber(lapseObject,"leechAction");
            }
            catch //If any error happen we back to default
            {
                Options = new DeckSimpleOptions();
            }
        }

        public void SaveOptionsToJsonConfig()
        {
            Config["autoplay"] = JsonValue.CreateBooleanValue(Options.AutoPlay);

            Config.GetNamedObject("new")["order"] = JsonValue.CreateNumberValue(Options.NewcardOrder);
            Config.GetNamedObject("new")["perDay"] = JsonValue.CreateNumberValue(Options.NewCardPerDay);
            Config.GetNamedObject("new")["bury"] = JsonValue.CreateBooleanValue(Options.BuryRelatedNewCard);

            Config.GetNamedObject("rev")["perDay"] = JsonValue.CreateNumberValue(Options.ReviewCardPerDay);
            Config.GetNamedObject("rev")["ivlFct"] = JsonValue.CreateNumberValue(Options.IvlFct/100.0);
            Config.GetNamedObject("rev")["bury"] = JsonValue.CreateBooleanValue(Options.BuryRelatedReviewCard);

            Config.GetNamedObject("lapse")["leechFails"] = JsonValue.CreateNumberValue(Options.LeechFailsThreshold);
            Config.GetNamedObject("lapse")["leechAction"] = JsonValue.CreateNumberValue(Options.LeechAction);
        }
    }
}
