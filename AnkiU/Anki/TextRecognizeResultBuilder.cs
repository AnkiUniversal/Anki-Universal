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
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;

namespace AnkiU.Anki
{
    public class TextRecognizeResultBuilder
    {
        private int index = 0;
        private Dictionary<int, string> result = new Dictionary<int, string>();
        private Dictionary<Selector, int> comboBoxDict = new Dictionary<Selector, int>();
        private int defaultIndex;
        private int defaultSkipIndex;        

        public TextRecognizeResultBuilder(int defaultIndex, int defaultSkipIndex = -1)
        {
            this.defaultIndex = defaultIndex;
            this.defaultSkipIndex = defaultSkipIndex;
        }

        public void AddComboBox(Selector box)
        {
            if (!comboBoxDict.Keys.Contains(box))
            {
                comboBoxDict[box] = index;
                result[index] = "";
                index++;
                box.SelectedIndex = defaultIndex;
            }
        }

        public void OnSelectionChanged(Selector box)
        {
            var word = box.SelectedItem as InkToWord;
            if (word == null)
                throw new Exception("The item stored in Selector must bind with InkToWord data type!");

            if (!comboBoxDict.Keys.Contains(box))
                return;

            var index = comboBoxDict[box];
            if(box.SelectedIndex == defaultSkipIndex)
                result[index] = "";
            else
                result[index] = word.Name;
        }

        public string GetResult()
        {            
            var recog = result.Values.ToArray();

            StringBuilder builder = new StringBuilder();
            builder.Append(recog[0]);
            for (int i = 1; i < recog.Length; i++)
            {
                if (recog[i] == "")
                    continue;

                if (recog[i-1] != "")
                    builder.Append(" ");                

                builder.Append(recog[i]);                
            }

            return builder.ToString();
        }

        public void Clear()
        {
            index = 0;
            result.Clear();
            comboBoxDict.Clear();
        }

    }
}
