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
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;

namespace AnkiU.ViewModels
{
    public class TemplateInformationViewModel
    {        
        public ObservableCollection<TemplateInformation> Templates { get; set; }
        public AnkiU.AnkiCore.Models Models { get; set; }
        public JsonObject CurrentModel { get; set; }
        public JsonArray TemplatesJson { get; set; }
        private bool isForDeck;
        public TemplateInformationViewModel(AnkiU.AnkiCore.Models models, bool forDeck = true)
        {
            isForDeck = forDeck;
            Models = models;
            CurrentModel = models.GetCurrent(isForDeck);
            TemplatesJson = CurrentModel.GetNamedArray("tmpls");
            List<TemplateInformation> temp = new List<TemplateInformation>();
            foreach (var template in TemplatesJson)
            {
                string name = template.GetObject().GetNamedString("name");
                uint ord = (uint)JsonHelper.GetNameNumber(template.GetObject(),"ord");
                TemplateInformation m = new TemplateInformation(name, ord);
                temp.Add(m);
            }
            temp.Sort((x, y) => { return x.Ord.CompareTo(y.Ord); });
            this.Templates = new ObservableCollection<TemplateInformation>(temp);
        }

        public void AddNewTemplate(string name, uint ordToClone = 0)
        {
            var newTemplate = Models.NewTemplate(name);
            var cloneTemplate = TemplatesJson.GetObjectAt(ordToClone);
            newTemplate["qfmt"] = JsonValue.CreateStringValue(cloneTemplate.GetNamedString("qfmt"));
            newTemplate["afmt"] = JsonValue.CreateStringValue(cloneTemplate.GetNamedString("afmt"));
            Models.AddTemplate(CurrentModel, newTemplate);
            Models.Save(CurrentModel, true);            
            TemplatesJson = CurrentModel.GetNamedArray("tmpls");
            Templates.Add(new TemplateInformation(name, (uint)JsonHelper.GetNameNumber(newTemplate,"ord")));
        }

        public void RenameTemplate(string name, uint ord)
        {            
            var template = TemplatesJson.GetObjectAt(ord);
            template["name"] = JsonValue.CreateStringValue(name);                        
            Models.Save(CurrentModel);
            UpdateModelAndTemplates();
        }

        public void RemoveTemplate(uint ord)
        {
            Models.RemoveTemplate(CurrentModel, TemplatesJson.GetObjectAt(ord));
            Models.Save(CurrentModel, true);
            UpdateModelAndTemplates();
        }

        private void UpdateModelAndTemplates()
        {
            Templates.Clear();
            CurrentModel = Models.GetCurrent(isForDeck);
            TemplatesJson = CurrentModel.GetNamedArray("tmpls");
            foreach (var t in TemplatesJson)
            {
                uint newOrd = (uint)JsonHelper.GetNameNumber(t.GetObject(),"ord");
                Templates.Add(new TemplateInformation(t.GetObject().GetNamedString("name"), newOrd));
            }
        }

        public void RepositionTemplate(uint ord, int newOrd)
        {
            Models.MoveTemplate(CurrentModel, TemplatesJson.GetObjectAt(ord), newOrd);
            Models.Save(CurrentModel);
            UpdateModelAndTemplates();
        }

    }
}
