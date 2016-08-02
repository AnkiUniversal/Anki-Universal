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
