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
    public class NotesFirstFieldViewModel 
    {        
        public ObservableCollectionAutoResize<NoteField> FirstFields { get; set; }

        public NotesFirstFieldViewModel(int maxSize)
        {
            FirstFields = new ObservableCollectionAutoResize<NoteField>(maxSize);
        }

        public void AddFirstFieldToList(Note note)
        {
            string fieldName = note.Model["flds"].GetArray().GetObjectAt(0).GetNamedString("name"); ;
            NoteField firstField = new NoteField(note.Id, fieldName, 0, note.Fields[0]);
            FirstFields.Insert(0, firstField);
        }

        public NoteField GetNoteField(long noteId)
        {
            foreach(var field in FirstFields)            
                if (field.Id == noteId)
                    return field;
            return null;          
        }

        public void RemoveFirstFieldFromList(NoteField note)
        {
            for(int i = 0; i < FirstFields.Count; i++)
            {
                if(FirstFields[i].Content == note.Content)                
                    FirstFields.RemoveAt(i);                
            }
        }

    }
}
