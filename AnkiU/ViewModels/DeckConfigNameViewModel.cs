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
using AnkiU.Models;
using AnkiU.UIUtilities;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace AnkiU.ViewModels
{
    public class DeckConfigNameViewModel
    {
        public ObservableCollection<DeckConfigName> Configs { get; set; }        

        private JsonObject selectedDeck;
        public Collection Collection { get; set; }

        public DeckConfigNameViewModel(Collection collection, long selectedDeckId)
        {
            this.Collection = collection;
            selectedDeck = collection.Deck.Get(selectedDeckId);
            var usedConfigId = selectedDeck.GetNamedNumber("conf");
            var allConfig = collection.Deck.AllConf();
            List<DeckConfigName> list = new List<DeckConfigName>();
            foreach (var config in allConfig)
            {
                DeckConfigName configName = new DeckConfigName();

                configName.Id = (long)config.GetNamedNumber("id");
                configName.Name = config.GetNamedString("name");
                configName.IsUsedBySelectedDeck = (usedConfigId == configName.Id);

                if (configName.Id == (int)ConfigPresets.Default)
                    DefaultConfigViewSetup(configName);
                else if (configName.Id >= (int)ConfigPresets.TagOnLeech && configName.Id <= (int)ConfigPresets.DueOnly)
                    PresetConfigsViewSetup(configName);
                else
                    UserCreateConfigsViewSetup(configName);

                list.Add(configName);
            }
            SortConfigsByID(list);
            Configs = new ObservableCollection<DeckConfigName>(list);            
        }

        public DeckConfigName GetSelectedConfig()
        {
            return Configs.First((x) => { return x.IsUsedBySelectedDeck == true; });
        }

        public void SetDeckConfigToSelected()
        {
            var config = GetSelectedConfig();
            selectedDeck["conf"] = JsonValue.CreateNumberValue(config.Id);                    
            Collection.Deck.Save(selectedDeck);     
        }

        private static void SortConfigsByID(List<DeckConfigName> list)
        {
            list.Sort((a, b) =>
            {
                if (a.Id > b.Id)
                    return 1;
                else return -1;
            });
        }

        private static void UserCreateConfigsViewSetup(DeckConfigName configName)
        {
            configName.EditVisibility = Visibility.Visible;
            configName.HelpTextVisibility = Visibility.Collapsed;
        }

        private static void PresetConfigsViewSetup(DeckConfigName configName)
        {
            configName.EditVisibility = Visibility.Collapsed;
            configName.HelpTextVisibility = Visibility.Visible;

            ConfigPresets type = (ConfigPresets)configName.Id;
            if (type == ConfigPresets.TagOnLeech)
                configName.HelpText = UIConst.HELP_TAG_ONLY_LEECH;
            else if (type == ConfigPresets.ShortInterval)
                configName.HelpText = UIConst.HELP_SHORT_INTERVAL;
            else if (type == ConfigPresets.LongInterval)
                configName.HelpText = UIConst.HELP_LONG_INTERVAL;
            else
                configName.HelpText = UIConst.HELP_DUE_ONLY;
        }

        private static void DefaultConfigViewSetup(DeckConfigName configName)
        {
            configName.HelpTextVisibility = Visibility.Collapsed;
            configName.EditVisibility = Visibility.Collapsed;
        }
    }
}
