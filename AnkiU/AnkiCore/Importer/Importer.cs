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
using Windows.Storage;

namespace AnkiU.AnkiCore.Importer
{
    public abstract class Importer : IDisposable
    {
        protected bool needMapper = false;
        protected bool needDelimiter = false;
        protected string relativePathToFile;
        protected StorageFolder sourceFolder;
        protected List<string> log;
        protected int total;

        public List<string> Log { get { return log; } }

        private long timeStamp;
        protected Collection destCol;
        protected Collection sourceCol;

        public Importer(Collection destCollection, StorageFolder sourceFolder, string file)
        {
            this.relativePathToFile = file;
            log = new List<string>();
            this.destCol = destCollection;
            this.sourceFolder = sourceFolder;
            total = 0;
        }

        public void Close()
        {
            //Dong't call close on destcol because it's 
            //passed to this class
            if (destCol != null)
                destCol = null;

            if (sourceCol != null)
            {
                sourceCol.Close(false);
                sourceCol = null;
            }
        }

        abstract public Task Run();

        /**
        * Timestamps
        * ***********************************************************
        * It's too inefficient to check for existing ids on every object,
        * and a previous import may have created timestamps in the future, so we
        * need to make sure our starting point is safe.
        */
        protected void PrepareTimeStamp()
        {
            timeStamp = Utils.MaxID(destCol.Database);
        }

        protected long IncreaseThenGetTimeStamp()
        {
            timeStamp++;
            return timeStamp;
        }

        public void Dispose()
        {
            Close();
        }
    }
}
