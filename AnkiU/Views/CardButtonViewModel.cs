using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AnkiU.Models;
using System.Collections.ObjectModel;

namespace AnkiU.ViewModels
{
    public class CardButtonViewModel
    {
        public ObservableCollection<CardButton> CardButtons { get; set; }

        public CardButtonViewModel(IEnumerable<CardButton> list)
        {
            CardButtons = new ObservableCollection<CardButton>(list);
        }
    }
}
