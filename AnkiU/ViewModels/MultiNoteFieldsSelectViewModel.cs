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
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace AnkiU.ViewModels
{
    public class MultiNoteFieldsSelectViewModel
    {
        public ObservableCollection<NoteField> Fields { get; set; } = new ObservableCollection<NoteField>();

        public MultiNoteFieldsSelectViewModel(IEnumerable<JsonObject> models)
        {
            //Use dictionary to ensure unique field name
            Dictionary<NoteField, bool> temp = new Dictionary<NoteField, bool>();
            foreach (var model in models)
            {                
                foreach (var json in model.GetNamedArray("flds"))
                {
                    var field = json.GetObject();
                    NoteField f = new NoteField();
                    f.Order = (int)field.GetNamedNumber("ord");
                    f.Name = field.GetNamedString("name");
                    temp[f] = true;
                }                
            }
            Fields = new ObservableCollection<NoteField>(temp.Keys);
        }

    }
}
