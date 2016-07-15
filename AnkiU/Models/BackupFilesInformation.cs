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
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnkiU.Models
{
    public class BackupFilesInformation
    {
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
            }
        }

        private string dateCreate;
        public string DateCreate
        {
            get
            {
                return dateCreate;
            }
            set
            {
                dateCreate = value;
            }
        }

        private long dateCreatInLong;
        public long DateCreatInLong
        {
            get
            {
                return dateCreatInLong;
            }
            set
            {
                dateCreatInLong = value;
            }
        }
    }
}
