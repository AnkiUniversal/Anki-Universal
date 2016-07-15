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

using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;

namespace AnkiU.Models
{    
    public class DeckConfigName : INotifyPropertyChanged
    {
        //A little work around so we can show the help text 
        public static FrameworkElement PointToShowFlyoutStatic { get; set; }
        public static Flyout ParentFlyoutStatic { get; set; }

        public FrameworkElement PointToShowFlyout { get { return PointToShowFlyoutStatic; } }
        public Flyout ParentFlyout { get { return ParentFlyoutStatic; } }

        private long id;
        public long Id
        {
            get
            {
                return id;
            }
            set
            {
                id = value;
                RaisePropertyChanged("Id");
            }
        }

        private string name;
        public string Name
        {
            get
            {
                return name;
            }
            set
            {
                name = value;
                RaisePropertyChanged("Name");
            }
        }

        private bool? isUsedBySelectedDeck;
        public bool? IsUsedBySelectedDeck
        {
            get
            {
                return isUsedBySelectedDeck;
            }
            set
            {
                isUsedBySelectedDeck = value;
                RaisePropertyChanged("IsUsedBySelectedDeck");
            }
        }

        private Visibility helpTextVisibility;
        public Visibility HelpTextVisibility
        {
            get
            {
                return helpTextVisibility;
            }
            set
            {
                helpTextVisibility = value;
                RaisePropertyChanged("HelpTextVisibility");
            }
        }

        private string helpText;
        public string HelpText
        {
            get
            {
                return helpText;
            }
            set
            {
                helpText = value;
                RaisePropertyChanged("HelpText");
            }
        }

        private Visibility editVisibility;
        public Visibility EditVisibility
        {
            get
            {
                return editVisibility;
            }
            set
            {
                editVisibility = value;
                RaisePropertyChanged("IsAllowedEdit");
            }
        }

        public DeckConfigName()            
        {

        }

        public DeckConfigName(string name, long id, bool isUsed, Visibility editVisibility)
            : this(name, id, isUsed, Visibility.Collapsed, null, editVisibility)
        {

        }

        public DeckConfigName(string name, long id, bool isUsed, Visibility helpTextVisibility, string helpText, Visibility editVisibility)
        {
            this.name = name;
            this.id = id;
            this.isUsedBySelectedDeck = isUsed;
            this.helpTextVisibility = helpTextVisibility;
            this.helpText = helpText;
            this.editVisibility = editVisibility;
        }

        public event PropertyChangedEventHandler PropertyChanged;

        private void RaisePropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public override string ToString()
        {
            return this.Name;
        }
    }
}
