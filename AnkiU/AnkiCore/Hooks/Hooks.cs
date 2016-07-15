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
using System.Diagnostics;

namespace AnkiU.AnkiCore.Hooks
{
    public class Hooks
    {
        public static Hooks thisInstance;
        private static Dictionary<string, List<Hook>> hooksDict = new Dictionary<string, List<Hook>>();

        public delegate void ExceptionReport(NullReferenceException e);
        public static event ExceptionReport ExceptionReportEvent;

        private static readonly object syncLock = new object();

        //Singleton pattern
        public static Hooks GetInstance()
        {
            lock (syncLock)
            {
                if (thisInstance == null)
                {
                    thisInstance = new Hooks();
                }
                return thisInstance;
            }
        }

        //Singleton pattern
        private Hooks()
        {
            hooksDict = new Dictionary<string, List<Hook>>();
            // Always-ON hooks
            new FuriganaFilters().Install(this);
            //new HintFilter().Install(this);
            new LaTeX().InstallHook(this);
            Leech.InstallHook(this);
        }

        /// <summary>
        /// Add a function to hook. Ignore if already on hook.
        /// </summary>
        /// <param name="hook"></param>
        /// <param name="func"></param>
        public void AddHook(string hook, Hook func)
        {
            if (!hooksDict.ContainsKey(hook) || hooksDict[hook] == null)
            {
                hooksDict.Add(hook, new List<Hook>());
            }
            bool found = false;
            foreach (Hook h in hooksDict[hook])
            {
                if (func.Equals(h))
                {
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                hooksDict[hook].Add(func);
            }
        }

        /// <summary>
        /// Remove a function if is on hook.
        /// </summary>
        /// <param name="hook"></param>
        /// <param name="func"></param>
        public void RemoveHook(string hook, Hook func)
        {
            if (hooksDict.ContainsKey(hook) && hooksDict[hook] != null)
            {
                foreach (Hook h in hooksDict[hook])
                {
                    if (func.Equals(h))
                    {
                        hooksDict[hook].Remove(h);
                        break;
                    }
                }
            }
        }

        /// <summary>
        /// Run all functions on hook
        /// </summary>
        /// <param name="hook">The name of the hook</param>
        /// <param name="">Variable arguments to be passed to the method runHook of each function on this hook</param>
        public void RunHook(string hook, params object[] args)
        {
            List<Hook> hookList = hooksDict[hook];
            string funcName = "";
            if (hookList != null)
            {
                try
                {
                    foreach (Hook func in hookList)
                    {
                        funcName = func.GetType().FullName;
                        func.RunHook(args);
                    }
                }
                catch(NotImplementedException e)
                {
                    string message = String.Format("{0}\nHook is not implemented {1} : {2}", e.Message, hook, funcName);
                    throw new NotImplementedException(message, e);
                }
                catch (Exception e)
                {
                    string message = String.Format("{0}\nException while running hook {1} : {2}", e.Message, hook, funcName);
                    throw new Exception(message, e);
                }
            }
        }

        /// <summary>
        /// Apply all functions on hook to arg and return the result.
        /// </summary>
        /// <param name="hook">The name of the hook</param>
        /// <param name="arg">The input to the filter on hook</param>
        /// <param name="args">Variable arguments to be passed to the method runHook of each function on this hook</param>
        /// <returns></returns>
        public static object RunFilter(string hook, object arg, params object[] args)
        {
            if (hooksDict == null)
            {
                ExceptionReportEvent(new NullReferenceException("Hooks.runFilter: Hooks object uninitialized"));
                return arg;
            }

            if (!hooksDict.ContainsKey(hook))
                return arg;

            List<Hook> _hook = hooksDict[hook];
            string funcName = "";
            if (_hook != null)
            {
                try
                {
                    foreach (Hook func in _hook)
                    {
                        funcName = func.GetType().FullName;
                        arg = func.RunFilter(arg, args);
                    }
                }
                catch (Exception e)
                {
                    throw new Exception ("Error in filter " + hook + ":" + funcName, e);
                }
            }
            return arg;
        }
    }
}
