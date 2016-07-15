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

using AnkiU.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace AnkiU.ViewModels
{
    public class CollectionOptionViewModel
    {
        public CollectionOptions Options { get; set; }

        public JsonObject Config { get; set; }

        public CollectionOptionViewModel(JsonObject config)
        {
            Config = config;
            Options = new CollectionOptions();
            GetOptionsToView();
        }

        public void GetOptionsToView()
        {
            try
            {
                Options.IsShowDueCount = Config.GetNamedBoolean("dueCounts");
                Options.IsShowEstTime = Config.GetNamedBoolean("estTimes");
                Options.ReviewType = (int)Config.GetNamedNumber("newSpread");
            }
            catch //If any error happen we back to default
            {
                Options = new CollectionOptions();
            }
        }

        public void SaveOptionsToJsonConfig()
        {
            Config["dueCounts"] = JsonValue.CreateBooleanValue(Options.IsShowDueCount);
            Config["estTimes"] = JsonValue.CreateBooleanValue(Options.IsShowEstTime);
            Config["newSpread"] = JsonValue.CreateNumberValue(Options.ReviewType);
        }

        public static bool IsDueCountEnable(JsonObject config)
        {
            return config.GetNamedBoolean("dueCounts");
        }

        public static bool IsShowNextReviewTimeEnable(JsonObject config)
        {
            return config.GetNamedBoolean("estTimes");
        }
    }
}
