using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace AnkiU.AnkiCore.Templates
{
    class Template
    {
        public const  string clozeReg = "(?s)\\{\\{c{0}::(.*?)(::(.*?))?\\}\\}";
        private static readonly Regex fHookFieldMod = new Regex("^(.*?)(?:\\((.*)\\))?$", RegexOptions.Compiled); 
        private static readonly Regex fClozeSection = new Regex(@"c[qa]:(\d+):(.+)", RegexOptions.Compiled);
    }
}
