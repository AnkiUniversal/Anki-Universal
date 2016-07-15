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
    /**
     * Basic Hook class. All other hooks should extend this and override either runHook or runFilter.
     * In short if you want result from the hook, override runFilter, otherwise runHook.
     * <p>
     * If the hook you are creating is supposed to have state, meaning that:<ul>
     * <li>It probably uses arguments in its constructor.
     * <li>Can potentially have instances that don't behave identically.
     * <li>Uses private members to store information between runs.
     * </ul>
     * Then you should also override methods equals and hashCode, so that they take into consideration any fields you have added.<p>
     * You can do so using the auto-generate feature from Eclipse: Source->Generate hashCode() and equals()
     */
    public abstract class Hook
    {
        private const int prime = 31;

        public override int GetHashCode()
        {
            int result = 1;
            result = prime * result + ((GetType().FullName == null) ? 
                                        0 : GetType().FullName.GetHashCode());
            return result;
        }

        public override bool Equals(object obj)
        {
            if (this == obj)
            {
                return true;
            }
            if (obj == null)
            {
                return false;
            }
            if (GetType() != obj.GetType())
            {
                return false;
            }
            Hook other = obj as Hook;
            if (other == null)
                return false;

            string fName = GetType().FullName;
            string oName = other.GetType().FullName;    

            if (fName == null)
            {
                if (oName != null)
                {
                    return false;
                }
            }
            else if (!fName.Equals(oName))
            {
                return false;
            }
            return true;
        }

        public abstract void RunHook(params object[] args);

        public abstract object RunFilter(object arg, params object[] args);
    }
}
