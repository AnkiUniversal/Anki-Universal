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
    public class TestModels
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
        public async Task TestModelDelete()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                var f = col.NewNote();
                f.SetItem("Front", "1");
                f.SetItem("Back", "2");
                col.AddNote(f);
                Assert.AreEqual(1, col.CardCount());
                col.Models.Remove(col.Models.GetCurrent());
                Assert.AreEqual(0, col.CardCount());
            }
        }

        [TestMethod]
        public async Task TestModelCopy()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                var m = col.Models.GetCurrent();
                var m2 = col.Models.Copy(m);

                Assert.AreEqual("Basic copy", m2["name"].GetString());
                Assert.AreNotEqual(m["name"], m2["name"]);
                Assert.AreNotEqual(m["id"], m2["id"]);
                Assert.AreEqual(2, m["flds"].GetArray().Count);
                Assert.AreEqual(2, m2["flds"].GetArray().Count);
                Assert.AreEqual(1, m["tmpls"].GetArray().Count);
                Assert.AreEqual(1, m2["tmpls"].GetArray().Count);
                Assert.AreEqual(col.Models.SchemaHash(m), col.Models.SchemaHash(m2));
            }
        }

        [TestMethod]
        public async Task TestFields()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                var f = col.NewNote();
                f.SetItem("Front", "1");
                f.SetItem("Back", "2");
                col.AddNote(f);

                var m = col.Models.GetCurrent();

                //Make sure renaming a field updates the templates
                var fieldArray = m["flds"].GetArray();
                var field = fieldArray.GetObjectAt(0);
                col.Models.RenameField(m, field, "NewFront");
                StringAssert.Contains(m["tmpls"].GetArray().GetObjectAt(0)["qfmt"].GetString(), "{{NewFront}}");

                //Add a field
                var h = col.Models.SchemaHash(m);
                var json = col.Models.NewField("foo");
                col.Models.AddField(m, json);
                var listFields = col.GetNote(col.Models.NoteIds(m)[0]).Fields;
                var expectedFields = new string[] { "1", "2", "" };
                for(int i = 0; i < expectedFields.Length; i++)
                    Assert.AreEqual(expectedFields[i], listFields[i]);
                Assert.AreNotEqual(h, col.Models.SchemaHash(m));

                //Rename it
                col.Models.RenameField(m, json, "bar");
                long noteID = col.Models.NoteIds(m)[0];
                Assert.AreEqual("", col.GetNote(noteID).GetItem("bar"));

                //Delete back
                col.Models.RemoveField(m, fieldArray.GetObjectAt(1));
                listFields = col.GetNote(noteID).Fields;
                expectedFields = new string[] { "1", "" };
                for (int i = 0; i < expectedFields.Length; i++)
                    Assert.AreEqual(expectedFields[i], listFields[i]);

                //Move 0 -> 1
                fieldArray = m["flds"].GetArray();
                col.Models.MoveField(m, fieldArray.GetObjectAt(0), 1);
                listFields = col.GetNote(noteID).Fields;
                expectedFields = new string[] { "", "1" };
                for (int i = 0; i < expectedFields.Length; i++)
                    Assert.AreEqual(expectedFields[i], listFields[i]);

                //Move 1 -> 0
                fieldArray = m["flds"].GetArray();
                col.Models.MoveField(m, fieldArray.GetObjectAt(1), 0);
                listFields = col.GetNote(noteID).Fields;
                expectedFields = new string[] { "1", "" };
                for (int i = 0; i < expectedFields.Length; i++)
                    Assert.AreEqual(expectedFields[i], listFields[i]);

                //Add another and put in middle
                json = col.Models.NewField("baz");
                col.Models.AddField(m, json);
                f = col.GetNote(noteID);
                f.SetItem("baz", "2");
                f.SaveChangesToDatabase();
                listFields = col.GetNote(noteID).Fields;
                expectedFields = new string[] { "1", "", "2"};
                for (int i = 0; i < expectedFields.Length; i++)
                    Assert.AreEqual(expectedFields[i], listFields[i]);

                //Move 2 -> 1
                fieldArray = m["flds"].GetArray();
                col.Models.MoveField(m, fieldArray.GetObjectAt(2), 1);
                listFields = col.GetNote(noteID).Fields;
                expectedFields = new string[] { "1", "2", ""};
                for (int i = 0; i < expectedFields.Length; i++)
                    Assert.AreEqual(expectedFields[i], listFields[i]);

                //Move 0 -> 2
                fieldArray = m["flds"].GetArray();
                col.Models.MoveField(m, fieldArray.GetObjectAt(0), 2);
                listFields = col.GetNote(noteID).Fields;
                expectedFields = new string[] { "2", "", "1" };
                for (int i = 0; i < expectedFields.Length; i++)
                    Assert.AreEqual(expectedFields[i], listFields[i]);

                //Move 0 -> 2
                fieldArray = m["flds"].GetArray();
                col.Models.MoveField(m, fieldArray.GetObjectAt(0), 1);
                listFields = col.GetNote(noteID).Fields;
                expectedFields = new string[] { "", "2", "1" };
                for (int i = 0; i < expectedFields.Length; i++)
                    Assert.AreEqual(expectedFields[i], listFields[i]);

            }
        }

        [TestMethod]
        public async Task TestTemplates()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                var m = col.Models.GetCurrent();
                var mm = col.Models;

                var t = mm.NewTemplate("Reverse");                
                t["qfmt"] = JsonValue.CreateStringValue("{{Back}}");
                t["afmt"] = JsonValue.CreateStringValue("{{Front}}");
                mm.AddTemplate(m, t);
                mm.Save(m);
                var f = col.NewNote();
                f.SetItem("Front", "1");
                f.SetItem("Back", "2");
                col.AddNote(f);

                Assert.AreEqual(2, col.CardCount());

                var listCard = f.Cards();
                //First card should have first ord
                Assert.AreEqual(0, listCard[0].Ord);
                Assert.AreEqual(1, listCard[1].Ord);

                //Switch templates
                col.Models.MoveTemplate(m, listCard[0].GetTemplate(), 1);
                listCard[0].LoadFromDatabase();
                listCard[1].LoadFromDatabase();
                Assert.AreEqual(1, listCard[0].Ord);
                Assert.AreEqual(0, listCard[1].Ord);

                //Removing a template should delete its cards
                Assert.IsTrue(col.Models.RemoveTemplate(m,
                              m["tmpls"].GetArray().GetObjectAt(0)));
                Assert.AreEqual(1, col.CardCount());

                //And should have updated the other cards' ordinals
                var c = f.Cards()[0];
                Assert.AreEqual(0, c.Ord);
                Assert.AreEqual("1", AnkiU.AnkiCore.Utils.StripHTML(c.GetQuestionWithCss()));

                //It shouldn't be possible to orphan notes by removing templates
                t = mm.NewTemplate(m["name"].GetString());
                mm.AddTemplate(m, t);
                bool success = col.Models.RemoveTemplate(m, m["tmpls"].GetArray().GetObjectAt(0));
                Assert.IsFalse(success);
            }
        }

        [TestMethod]
        public async Task TestClozeOrdinals()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                col.Models.SetCurrent(col.Models.GetModelByName("Cloze"));
                var m = col.Models.GetCurrent();
                var mm = col.Models;

                //We replace the default Cloze template
                var t = mm.NewTemplate("ChainedCloze");
                t["qfmt"] = JsonValue.CreateStringValue("{{text:cloze:Text}}");
                t["afmt"] = JsonValue.CreateStringValue("{{text:cloze:Text}}");
                mm.AddTemplate(m, t);
                mm.Save(m);

                var temArray = m["tmpls"].GetArray();

                col.Models.RemoveTemplate(m, m["tmpls"].GetArray().GetObjectAt(0));

                temArray = m["tmpls"].GetArray();

                var f = col.NewNote();
                f.SetItem("Text", "{{c1::firstQ::firstA}}{{c2::secondQ::secondA}}");
                col.AddNote(f);
                Assert.AreEqual(2, col.CardCount());
                var cardList = f.Cards();
                //First card should have first ord
                Assert.AreEqual(0, cardList[0].Ord);
                Assert.AreEqual(1, cardList[1].Ord);
            }
        }

        [TestMethod]
        public async Task TestText()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                var m = col.Models.GetCurrent();
                m["tmpls"].GetArray().GetObjectAt(0)["qfmt"] = JsonValue.CreateStringValue("{{text:Front}}");
                col.Models.Save(m);
                var f = col.NewNote();
                f.SetItem("Front", "hello<b>world");
                col.AddNote(f);
                StringAssert.Contains(f.Cards()[0].GetQuestionWithCss(), "helloworld");
            }
        }

        [TestMethod]
        public async Task TestClozet()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                col.Models.SetCurrent(col.Models.GetModelByName("Cloze"));
                var f = col.NewNote();
                Assert.AreEqual("Cloze", f.Model["name"].GetString());

                //A cloze model with no clozes is not empty
                f.SetItem("Text", "nothing");
                Assert.IsTrue(col.AddNote(f) > 0);

                //Try with one cloze
                f = col.NewNote();
                f.SetItem("Text", "hello {{c1::world}}");
                Assert.AreEqual(1, col.AddNote(f));
                StringAssert.Contains(f.Cards()[0].GetQuestionWithCss(), "hello <span class=cloze>[...]</span>");
                StringAssert.Contains(f.Cards()[0].GetAnswerWithCss(), "hello <span class=cloze>world</span>");

                //And with a comment
                f = col.NewNote();
                f.SetItem("Text", "hello {{c1::world::typical}}");
                Assert.AreEqual(1, col.AddNote(f));
                StringAssert.Contains(f.Cards()[0].GetQuestionWithCss(), "hello <span class=cloze>[typical]</span>");
                StringAssert.Contains(f.Cards()[0].GetAnswerWithCss(), "hello <span class=cloze>world</span>");

                //And with 2 clozes
                f = col.NewNote();
                f.SetItem("Text", "hello {{c1::world}} {{c2::bar}}");
                Assert.AreEqual(2, col.AddNote(f));
                List<Card> cards = f.Cards();
                StringAssert.Contains(cards[0].GetQuestionWithCss(), "<span class=cloze>[...]</span> bar");
                StringAssert.Contains(cards[0].GetAnswerWithCss(), "<span class=cloze>world</span> bar");
                StringAssert.Contains(cards[1].GetQuestionWithCss(), "world <span class=cloze>[...]</span>");
                StringAssert.Contains(cards[1].GetAnswerWithCss(), "world <span class=cloze>bar</span>");

                //If there are multiple answers for a single cloze, they are given in a list
                f = col.NewNote();
                f.SetItem("Text", "a {{c1::b}} {{c1::c}}");
                Assert.AreEqual(1, col.AddNote(f));
                StringAssert.Contains(f.Cards()[0].GetAnswerWithCss(), "<span class=cloze>b</span> <span class=cloze>c</span>");

                //If we add another cloze, a card should be generated
                int cnt = col.CardCount();
                f.SetItem("Text", "{{c2::hello}} {{c1::foo}}");
                f.SaveChangesToDatabase();
                Assert.AreEqual(cnt + 1, col.CardCount());

                //0 or negative indices are not supported
                f.SetItem("Text", "{{c0::zero}} {{c-1:foo}}");
                f.SaveChangesToDatabase();
                Assert.AreEqual(2, f.Cards().Count);
            }
        }

        [TestMethod]
        public async Task TestChainedMods()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                col.Models.SetCurrent(col.Models.GetModelByName("Cloze"));
                var m = col.Models.GetCurrent();
                var mm = col.Models;

                //We replace the default Cloze template
                var t = mm.NewTemplate("ChainedCloze");
                t["qfmt"] = JsonValue.CreateStringValue("{{text:cloze:Text}}");
                t["afmt"] = JsonValue.CreateStringValue("{{text:cloze:Text}}");
                mm.AddTemplate(m, t);
                mm.Save(m);
                col.Models.RemoveTemplate(m, m["tmpls"].GetArray().GetObjectAt(0));

                var f = col.NewNote();
                string q1 = "<span style=\"color:red\">phrase</span>";
                string a1 = "<b>sentence</b>";
                string q2 = "<span style=\"color:red\">en chaine</span>";
                string a2 = "<i>chained</i>";
                string str = String.Format("This {{{{c1::{0}::{1}}}}} demonstrates {{{{c1::{2}::{3}}}}} clozes.",
                                          q1,a1,q2,a2);
                f.SetItem("Text", str);
                Assert.AreEqual(1, col.AddNote(f));
                string expected = "This [sentence] demonstrates [chained] clozes.";
                StringAssert.Contains(f.Cards()[0].GetQuestionWithCss(), expected);
                expected = "This phrase demonstrates en chaine clozes.";
                StringAssert.Contains(f.Cards()[0].GetAnswerWithCss(), expected);
            }
        }

        [TestMethod]
        public async Task TestAvailOrds()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                var m = col.Models.GetCurrent();
                var mm = col.Models;
                var t = m["tmpls"].GetArray().GetObjectAt(0);
                var f = col.NewNote();
                f.SetItem("Front", "1");

                //Simple templates
                Assert.AreEqual(1, mm.AvailableOrds(m, AnkiU.AnkiCore.Utils.JoinFields(f.Fields)).Count);
                Assert.AreEqual(0, mm.AvailableOrds(m, AnkiU.AnkiCore.Utils.JoinFields(f.Fields))[0]);
                t["qfmt"] = JsonValue.CreateStringValue("{{Back}}");
                mm.Save(m, template: true);
                Assert.AreNotEqual(0, mm.AvailableOrds(m, AnkiU.AnkiCore.Utils.JoinFields(f.Fields)));

                //And
                t["qfmt"] = JsonValue.CreateStringValue("{{#Front}}{{#Back}}{{Front}}{{/Back}}{{/Front}}");
                mm.Save(m, template: true);
                Assert.AreNotEqual(0, mm.AvailableOrds(m, AnkiU.AnkiCore.Utils.JoinFields(f.Fields)));
                t["qfmt"] = JsonValue.CreateStringValue("{{#Front}}\n{{#Back}}\n{{Front}}\n{{/Back}}\n{{/Front}}");
                mm.Save(m, template: true);
                Assert.AreNotEqual(0, mm.AvailableOrds(m, AnkiU.AnkiCore.Utils.JoinFields(f.Fields)));

                //Or
                t["qfmt"] = JsonValue.CreateStringValue("{{Front}}\n{{Back}}");
                mm.Save(m, template: true);
                Assert.AreEqual(1, mm.AvailableOrds(m, AnkiU.AnkiCore.Utils.JoinFields(f.Fields)).Count);
                Assert.AreEqual(0, mm.AvailableOrds(m, AnkiU.AnkiCore.Utils.JoinFields(f.Fields))[0]);
                t["Front"] = JsonValue.CreateStringValue("");
                t["Back"] = JsonValue.CreateStringValue("1");
                Assert.AreEqual(1, mm.AvailableOrds(m, AnkiU.AnkiCore.Utils.JoinFields(f.Fields)).Count);
                Assert.AreEqual(0, mm.AvailableOrds(m, AnkiU.AnkiCore.Utils.JoinFields(f.Fields))[0]);
            }
        }

        [TestMethod]
        public async Task TestModelChange()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                var basic = col.Models.GetModelByName("Basic");
                var cloze = col.Models.GetModelByName("Cloze");

                //Enable second template and add a note
                var m = col.Models.GetCurrent();
                var mm = col.Models;

                var t = mm.NewTemplate("Reverse");
                t["qfmt"] = JsonValue.CreateStringValue("{{Back}}");
                t["afmt"] = JsonValue.CreateStringValue("{{Front}}");
                mm.AddTemplate(m, t);
                mm.Save(m);
                var f = col.NewNote();
                f.SetItem("Front", "f");
                f.SetItem("Back", "b123");
                col.AddNote(f);
                Dictionary<int, int?> map = new Dictionary<int, int?>();
                map.Add(0, 1);
                map.Add(1, 0);

                //Switch fields
                col.Models.Change(basic, new long[] { f.Id }, basic, map, null);
                f.LoadFromDatabase();
                Assert.AreEqual("b123", f.GetItem("Front"));
                Assert.AreEqual("f", f.GetItem("Back"));

                //Switch cards
                var c0 = f.Cards()[0];
                var c1 = f.Cards()[1];
                StringAssert.Contains(c0.GetQuestionWithCss(), "b123");
                StringAssert.Contains(c1.GetQuestionWithCss(), "f");
                Assert.AreEqual(0, c0.Ord);
                Assert.AreEqual(1, c1.Ord);
                col.Models.Change(basic, new long[] { f.Id }, basic, null, map);
                f.LoadFromDatabase();
                c0.LoadFromDatabase();
                c1.LoadFromDatabase();
                StringAssert.Contains(c0.GetQuestionWithCss(), "f");
                StringAssert.Contains(c1.GetQuestionWithCss(), "b123");
                Assert.AreEqual(1, c0.Ord);
                Assert.AreEqual(0, c1.Ord);

                //.Cards() returns cards in order?
                Assert.AreEqual(c1.Id, f.Cards()[0].Id);

                //Delete first card;
                map[0] = null;
                map[1] = 1;
                col.Models.Change(basic, new long[] { f.Id }, basic, null, map);
                f.LoadFromDatabase();
                c0.LoadFromDatabase();
                try
                {
                    c1.LoadFromDatabase();
                    Assert.Fail();
                }
                catch(NoCardException)
                {
                }

                //But we have two cards, as a new one was generated
                Assert.AreEqual(2, f.Cards().Count);

                //An unmapped field becomes blank
                Assert.AreEqual("b123", f.GetItem("Front"));
                Assert.AreEqual("f", f.GetItem("Back"));
                col.Models.Change(basic, new long[] { f.Id }, basic, map, null);
                f.LoadFromDatabase();
                Assert.AreEqual("", f.GetItem("Front"));
                Assert.AreEqual("f", f.GetItem("Back"));

                //Another note to try model conversion
                f = col.NewNote();
                f.SetItem("Front", "f2");
                f.SetItem("Back", "b2");
                col.AddNote(f);
                Assert.AreEqual(2, col.Models.NoteUseCount(basic));
                Assert.AreEqual(0, col.Models.NoteUseCount(cloze));
                map[0] = 0;
                map[1] = 1;
                col.Models.Change(basic, new long[] { f.Id }, cloze, map, map);
                f.LoadFromDatabase();
                Assert.AreEqual("f2", f.GetItem("Text"));
                Assert.AreEqual(2, f.Cards().Count);

                //Back the other way, with deletion of second ord
                col.Models.RemoveTemplate(basic, basic["tmpls"].GetArray().GetObjectAt(1));
                string query = "select count() from cards where nid = ?";
                Assert.AreEqual(2, col.Database.QueryScalar<int>(query, f.Id));
                col.Models.Change(cloze, new long[] { f.Id }, basic, map, map);
                Assert.AreEqual(1, col.Database.QueryScalar<int>(query, f.Id));
            }
        }

    }
}

