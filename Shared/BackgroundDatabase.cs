using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Shared
{
    public class BackgroundDatabase
    {
        [SQLite.Net.Attributes.PrimaryKey]
        public int Id { get; set; }

        public long DateInUnixSeconds { get; set; }

        public bool IsShownToast { get; set; }
    }
}
