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
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.Data.Json;
using Windows.UI.Core;

namespace AnkiU.ViewModels
{
    public class TagInformationViewModel : INotifyPropertyChanged
    {
        private Collection collection;
        public Note CurrentNote { get; set; }

        private List<TagInformation> tags;
        public List<TagInformation> Tags
        {
            get { return tags; }
            set
            {
                tags = value;
                RaisePropertyChanged("Tags");
            }
        }

        private string usedTags = "";
        public string UsedTags
        {
            get { return usedTags; }
            set
            {
                usedTags = value;
                RaisePropertyChanged("UsedTags");
            }
        }

        public TagInformationViewModel(Collection collection, Note note)
        {
            this.collection = collection;
            this.CurrentNote = note;            
            InitNoteTags();                                 
        }

        private void InitNoteTags()
        {
            tags = new List<TagInformation>();
            var usedTags = CurrentNote.Tags;
            bool isUsed = false;
            foreach (var tag in collection.Tags.All())
            {
                if (usedTags.Contains(tag))
                    isUsed = true;
                else
                    isUsed = false;
                tags.Add(new TagInformation(tag, isUsed));
            }
            tags.Sort(SortWithUsedTags);
            //Reassign to throw a propertychanged event through property
            Tags = tags;

            usedTags.Sort((x, y) => { return x.CompareTo(y); });
            UsedTags = String.Join(", ", usedTags);
        }

        private int SortWithUsedTags(TagInformation first, TagInformation second)
        {
            if (first.IsUsed == second.IsUsed)
                return first.Name.CompareTo(second.Name);
            else
            {
                if (first.IsUsed == null || first.IsUsed == false)
                    return 1;
                else
                    return -1;
            }
        }

        public void CloneUsedTagsToNewNote()
        {
            CurrentNote.Tags = new List<string>(UsedTags.Split(new string[] { ", " }, StringSplitOptions.RemoveEmptyEntries));
        }

        public void UpdateNoteTagsFromField()
        {
            tags.Sort(SortWithUsedTags);
            List<string> usedTags = new List<string>();
            foreach (var tag in tags)
            {
                if (tag.IsUsed != null && tag.IsUsed == true)
                    usedTags.Add(tag.Name);
            }
            CurrentNote.Tags = usedTags;
            UsedTags = String.Join(", ", usedTags);
            UpdateViewRender();
        }

        private void UpdateViewRender()
        {
            //WARNING: this has to be done to avoid databinding
            //bind to old reference and cause a null reference exception
            var temp = new List<TagInformation>();
            foreach (var tag in tags)
                temp.Add(new TagInformation(tag.Name, (bool)tag.IsUsed));
            Tags = temp;
        }

        public void UpdateNoteTagsFromNote()
        {                        
            foreach (var tag in tags)
            {
                if (CurrentNote.HasTag(tag.Name))
                    tag.IsUsed = true;
                else
                    tag.IsUsed = false;
            }            
            UsedTags = String.Join(", ", CurrentNote.Tags);
            tags.Sort(SortWithUsedTags);
            UpdateViewRender();
        }

        public void AddNewTags(string newTags)
        {
            var listTags = collection.Tags.Split(newTags);
            var canonifyTags = collection.Tags.Canonify(listTags);           
            collection.Tags.Register(canonifyTags);
            collection.Tags.SaveChangesToDatabase();
            foreach(var tag in canonifyTags)
                if (!CurrentNote.Tags.Contains(tag))
                    CurrentNote.Tags.Add(tag);
                        
            InitNoteTags();
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

    }
}
