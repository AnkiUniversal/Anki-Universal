using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AnkiU.AnkiCore
{
    [SQLite.Net.Attributes.Table("notes")]
    class Note
    {
        private long id;
        [SQLite.Net.Attributes.Column("id")]
        public long Id { get { return id; } }

        private long mid;
        [SQLite.Net.Attributes.Column("mid")]
        public long Mid { get { return mid; } }

        private string[] fields;
        [SQLite.Net.Attributes.Column("flds")]
        public string[] Fields { get { return fields; } }

        private Tags tags;
        [SQLite.Net.Attributes.Column("tags")]
        public Tags Tags { get { return tags; } }

        [SQLite.Net.Attributes.Column("flds")]
        public string JointFields { get { return String.Join(" ", fields); } }

        public void SetField(int index, string value)
        {
            fields[index] = value;
        }

        public void Flush()
        {
            throw new NotImplementedException();
        }
    }

    /// <summary>
    /// Class used to get data from notes table in database.
    /// Avoid using Note class to reduce dependency.
    /// </summary>
    [SQLite.Net.Attributes.Table("notes")]
    public class notes
    {
        [SQLite.Net.Attributes.Column("tags")]
        public string Tags { get; set; }

        [SQLite.Net.Attributes.Column("id")]
        public long Id { get; set; }

        [SQLite.Net.Attributes.Column("flds")]
        public string Fields { get; set; }

        public notes() { }

        public notes(long id, string tags, string fields)
        {
            Id = id;
            Tags = tags;
            Fields = fields;
        }
    }
}
