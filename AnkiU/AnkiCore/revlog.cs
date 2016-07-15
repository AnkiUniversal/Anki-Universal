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

namespace AnkiU.AnkiCore
{
    /// <summary>
    /// Class used to retrieve revlog table from database
    /// </summary>
    [SQLite.Net.Attributes.Table("revlog")]
    public class revlog
    {
        [SQLite.Net.Attributes.Column("id")]
        public long Id { get; set; }

        [SQLite.Net.Attributes.Column("cid")]
        public long Cid { get; set; }

        [SQLite.Net.Attributes.Column("ease")]
        public int Ease { get; set; }

        [SQLite.Net.Attributes.Column("ivl")]
        public long Interval { get; set; }

        [SQLite.Net.Attributes.Column("lastIvl")]
        public long LastInterval { get; set; }

        [SQLite.Net.Attributes.Column("factor")]
        public int Factor { get; set; }

        [SQLite.Net.Attributes.Column("usn")]
        public int Usn { get; set; }

        [SQLite.Net.Attributes.Column("time")]
        public long Time { get; set; }

        [SQLite.Net.Attributes.Column("type")]
        public int Type { get; set; }
    }
}
