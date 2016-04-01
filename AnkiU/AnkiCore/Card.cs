using System;
using System.Collections.Generic;
using System.Text;

namespace AnkiU.AnkiCore
{
    class Card 
    {
        private long id;
        [SQLite.Net.Attributes.Column("id")]
        public long Id { get { return id; } }
    }
}
