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
using Microsoft.VisualStudio.TestPlatform.UnitTestFramework;
using AnkiU.AnkiCore;
using Windows.Storage;
using System.Threading.Tasks;
using SQLite.Net;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Text;
using Windows.Data.Json;

namespace TestAnkiCore
{
    //TODO: implement undo on collection
    [TestClass, Ignore]
    public class TestUndo
    {
        public StorageFolder tempFolder;

        [TestInitialize()]
        public async Task Setup()
        {
            if (tempFolder != null)
            {
                await tempFolder.DeleteAsync();
                tempFolder = null;
            }

            tempFolder = await Utils.localFolder.CreateFolderAsync("tempFolder");
        }

        [TestCleanup()]
        public async Task Clean()
        {
            if (tempFolder != null)
                await tempFolder.DeleteAsync();

            tempFolder = null;
        }

        [TestMethod]
        public async Task TestOpen()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                ////Should have no undo by default
                //Assert.IsNull(col.UndoName());

                ////let's adjust a study option
                //col.Save(name: "studyopts");
                //col.Conf["abc"] = JsonValue.CreateNumberValue(5);

                ////It should be listed as undoable
                //Assert.AreEqual("studyopts", col.UndoName());

                ////With about 5 minutes until it's clobbered
                //Assert.IsTrue(DateTimeOffset.Now.ToUnixTimeSeconds() - col.LastSave < 1);

                ////Undoing should restore the old value
                //col.Undo();
                //Assert.IsNull(col.UndoName());
                //Assert.IsFalse(col.Conf.ContainsKey("abc"));

                ////An (auto)save will clear the undo
                //col.Save(name: "foo");
                //Assert.AreEqual("foo", col.UndoName());
                //col.Save();
                //Assert.IsNull(col.UndoName());

                ////And a review will, too
                //col.Save(name: "add");
                //var f = col.NewNote();
                //f.SetItem("Front", "one");
                //col.AddNote(f);
                //col.Reset();
                //Assert.AreEqual("add", col.UndoName());
                //var c = col.Sched.PopCard();
                //col.Sched.AnswerCard(c, Sched.AnswerEase.Hard);
                //Assert.AreEqual("Review", col.UndoName());
            }
        }
    }
}
