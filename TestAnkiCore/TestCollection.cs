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
    [TestClass]
    public class TestCollection
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
        public async Task TestCreate()
        {
            try
            {
                using (Collection col = await Utils.GetEmptyCollection(tempFolder))
                {
                }
            }
            catch
            {
                Assert.Fail();
            }
        }


        [TestMethod]
        public async Task TestOpen()
        {
            try
            {
                using (Collection col = await Utils.GetExistCollection(tempFolder))
                {
                }
            }
            catch
            {
                Assert.Fail();
            }
        }

        [TestMethod]
        public async Task TestNoteAddDelete()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                var f = col.NewNote();
                f.SetItem("Front", "one");
                f.SetItem("Back", "two");
                var n = col.AddNote(f);
                Assert.AreEqual(1, n);

                //Test multiple cards - add another template
                var m = col.Models.GetCurrent();
                var mm = col.Models;
                var t = mm.NewTemplate("Reverse");
                t["qfmt"] = JsonValue.CreateStringValue("{{Back}}");
                t["afmt"] = JsonValue.CreateStringValue("{{Front}}");
                mm.AddTemplate(m, t);
                mm.Save(m);

                //The default save doesn't generate cards
                Assert.AreEqual(1, col.CardCount());

                //But when templates are edited such as in the card layout screen,
                //it should generate cards on close
                mm.Save(m, true);
                Assert.AreEqual(2, col.CardCount());

                //Creating new notes should use both cards
                f = col.NewNote();
                f.SetItem("Front", "three");
                f.SetItem("Back", "four");
                n = col.AddNote(f);
                Assert.AreEqual(2, n);
                Assert.AreEqual(4, col.CardCount());

                //Check q/a generation
                var c0 = f.Cards()[0];
                StringAssert.Contains(c0.GetQuestionWithCss(), "three");
                    
                //It should not be a duplicate
                Assert.IsFalse((int)f.DupeOrEmpty() > 0);

                //Now let's make a duplicate
                var f2 = col.NewNote();
                f2.SetItem("Front", "one");
                f2.SetItem("Back", "");
                Assert.IsTrue((int)f2.DupeOrEmpty() > 0);

                //Empty first field shoud not be permitted either
                f2.SetItem("Front", "");
                Assert.IsTrue((int)f2.DupeOrEmpty() > 0);
            }
        }

        [TestMethod]
        public async Task TestFieldChecksum()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                var f = col.NewNote();
                f.SetItem("Front", "new");
                f.SetItem("Back", "new2");
                var n = col.AddNote(f);
                var csum = col.Database.QueryScalar<long>("select csum from notes");
                Assert.AreEqual(Convert.ToInt64("c2a6b03f", 16), csum);

                f.SetItem("Front", "newx");
                f.SaveChangesToDatabase();
                csum = col.Database.QueryScalar<long>("select csum from notes");
                Assert.AreEqual(Convert.ToInt64("302811ae", 16), csum);
            }
        }

        [TestMethod]
        public async Task TestAddDelTags()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                var f = col.NewNote();
                f.SetItem("Front", "1");
                col.AddNote(f);

                var f2 = col.NewNote();
                f2.SetItem("Front", "2");
                col.AddNote(f2);

                //Adding for a given id
                col.Tags.BulkAdd(new List<long> {f.Id}, "foo");
                f.LoadFromDatabase();
                f2.LoadFromDatabase();
                Assert.IsTrue(f.Tags.Contains("foo"));
                Assert.IsFalse(f2.Tags.Contains("foo"));

                //Should be canonified
                col.Tags.BulkAdd(new List<long> { f.Id }, "foo aaa");
                f.LoadFromDatabase();
                Assert.AreEqual("aaa", f.Tags[0]);
                Assert.AreEqual(2, f.Tags.Count);
            }
        }

        [TestMethod]
        public async Task TestTimeStamps()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                Assert.AreEqual(4, col.Models.ThisModels.Count);
                for(int i = 0; i < 100; i++)
                {
                    Models.AddBasicModel(col);
                }
                Assert.AreEqual(104, col.Models.ThisModels.Count);
            }
        }

        [TestMethod]
        public async Task TestFurigana()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                var mm = col.Models;
                var m = mm.GetCurrent();

                //Filter should work
                m.GetNamedArray("tmpls").GetObjectAt(0)["qfmt"] 
                        = JsonValue.CreateStringValue("{{kana:Front}}");
                mm.Save(m);
                var n = col.NewNote();
                n.SetItem("Front", "foo[abc]");
                col.AddNote(n);
                var c = n.Cards()[0];
                Assert.IsTrue(c.GetQuestionWithCss().EndsWith("abc"));

                //And should avoid sound
                n.SetItem("Front", "foo[sound:abc.mp3]");
                n.SaveChangesToDatabase();
                StringAssert.Contains(c.GetQuestionWithCss(reload: true), "sound:");

                //It shouldn't throw an error while people are editing
                m.GetNamedArray("tmpls").GetObjectAt(0)["qfmt"]
                        = JsonValue.CreateStringValue("{{kana:}}");
                mm.Save(m);
                c.GetQuestionWithCss(reload: true);
            }
        }

    }
}
