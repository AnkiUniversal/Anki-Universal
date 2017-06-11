/*
Copyright (C) 2016-2017 Anki Universal Team <ankiuniversal@outlook.com>

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
using AnkiU.UIUtilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.Foundation;

namespace AnkiU.AnkiCore.Importer
{
    public class NlpJdictImporter
    {        
        private const string ENTRY = "Entry";
        private const string WORD= "Word";
        private const string READING = "Readings";
        private const string MEANING = "Meanings";
        private const string RESTRICT = "Restricted Readings";
        private const string FORMS = "Other Forms";

        private Collection collection;
        public long DeckId { get; private set; }
        private JsonObject model;

        public const string MODEL_NAME = "NLP Japanese Dictionary";

        public NlpJdictImporter(Collection collection)
        {
            this.collection = collection;
            DeckId = (long)collection.Deck.AddOrResuedDeck(MODEL_NAME, true);
            GetOrAddModel();
        }

        public NlpJdictImporter(Collection collection, long deckId)
        {
            this.collection = collection;
            DeckId = deckId;
            GetOrAddModel();
        }

        public async Task<bool> AddNote(WwwFormUrlDecoder decoder)
        {
            try
            {
                if (decoder.Count < 6)
                    return false;

                var note = collection.NewNote(model);
                var unescape = Uri.UnescapeDataString(decoder.GetFirstValueByName("Entry"));
                note.SetItem(ENTRY, unescape);

                var firstField = note.DupeOrEmpty();
                if (firstField == Note.FirstField.Duplicate)
                {
                    await UIHelper.ShowMessageDialog("A note with the same entry has already been added.", "");
                    return false;
                }

                unescape = Uri.UnescapeDataString(decoder.GetFirstValueByName("Word"));
                note.TrySetItem(WORD, unescape);

                unescape = Uri.UnescapeDataString(decoder.GetFirstValueByName("Reading"));
                note.TrySetItem(READING, unescape);

                unescape = Uri.UnescapeDataString(decoder.GetFirstValueByName("Meaning"));
                note.TrySetItem(MEANING, unescape);

                unescape = Uri.UnescapeDataString(decoder.GetFirstValueByName("RestrictReading"));
                note.TrySetItem(RESTRICT, unescape);

                unescape = Uri.UnescapeDataString(decoder.GetFirstValueByName("Forms"));
                note.TrySetItem(FORMS, unescape);

                note.Model["did"] = JsonValue.CreateNumberValue((long)DeckId);
                collection.AddNote(note);
                collection.SaveAndCommit();
                return true;
            }
            catch(Exception e)
            {
                await UIHelper.ShowMessageDialog("Unable to add new note from NLP Japanese Dictionary: " + e.Message);
                return false;
            }
        }

        private void GetOrAddModel()
        {
            model = collection.Models.GetModelByName(MODEL_NAME);
            if (model != null)
                return;

            var allModels = collection.Models;
            model = allModels.NewModel(MODEL_NAME);
            JsonObject field = allModels.NewField(ENTRY);
            allModels.AddField(model, field);
            field = allModels.NewField(WORD);
            allModels.AddField(model, field);
            field = allModels.NewField(READING);
            allModels.AddField(model, field);
            field = allModels.NewField(MEANING);
            allModels.AddField(model, field);
            field = allModels.NewField(RESTRICT);
            allModels.AddField(model, field);
            field = allModels.NewField(FORMS);
            allModels.AddField(model, field);

            JsonObject template = allModels.NewTemplate("Card 1");                       
            template["qfmt"] = JsonValue.CreateStringValue("<div><span style=\"font-size: 36pt;\">{{Word}}</span></div>");
            template["afmt"] = JsonValue.CreateStringValue("<div><span style=\"font-size: 36pt;\">{{FrontSide}}</span></div><hr id=\"answer\" /><div><div>&nbsp;</div>{{#Readings}}<div><strong>(OTHER) READINGS</strong></div><div>{{Readings}}</div><div>&nbsp;</div>{{/Readings}}<div><strong>MEANINGS</strong></div><div>{{Meanings}}</div><div>&nbsp;</div>{{#Restricted Readings}}<div><strong>RESTRICTED READINGS</strong></div><div>{{Restricted Readings}}</div><div>&nbsp;</div>{{/Restricted Readings}}{{#Other Forms}}<div><strong>OTHER FORMS</strong></div><div>{{Other Forms}}</div>{{/Other Forms}}</div>");
            allModels.AddTemplate(model, template);
            allModels.Add(model);
        }       
    }
}
