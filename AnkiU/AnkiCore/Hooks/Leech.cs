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

namespace AnkiU.AnkiCore.Hooks
{
    public class Leech
    {
        public static LeechHook leechHook = new LeechHook();

        public static void InstallHook(Hooks h)
        {
            h.AddHook("leech", leechHook);
        }

        public static void UnInstallHook(Hooks h)
        {
            h.RemoveHook("leech", leechHook);
        }


        //TODO:
        public class LeechHook : Hook
        {
            public override object RunFilter(object arg, params object[] args)
            {
                return null;
            }

            public override void RunHook(params object[] args)
            {
                return;
            }
        }
    }
}
