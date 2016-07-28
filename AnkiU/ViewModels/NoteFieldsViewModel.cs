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

using AnkiU.Anki;
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
    public class NoteFieldsViewModel 
    {        
        public ObservableCollection<NoteField> Fields { get; set; }

        public NoteFieldsViewModel(IEnumerable<NoteField> noteField)
        {
            Fields = new ObservableCollection<NoteField>(noteField);
        }

        public NoteFieldsViewModel(JsonObject model)
        {
            List<NoteField> temp = new List<NoteField>();
            foreach (var json in model.GetNamedArray("flds"))
            {
                var field = json.GetObject();
                NoteField f = new NoteField();
                f.Order = (int)field.GetNamedNumber("ord");
                f.Name = field.GetNamedString("name");
                temp.Add(f);
            }
            Fields = new ObservableCollection<NoteField>(temp);
        }     

        public void MoveField(int oldOrder, int newOrder)
        {
            var temp = Fields[oldOrder];
            Fields.RemoveAt(oldOrder);
            Fields.Insert(newOrder, temp);
            UpdateFieldOrder();
        }

        public void UpdateFieldOrder()
        {
            for (int i = 0; i < Fields.Count; i++)
                Fields[i].Order = i;
        }

        public List<IName> GetExistedFieldsName()
        {
            var existed = new List<IName>(Fields);

            return existed;
        }
    }
}
