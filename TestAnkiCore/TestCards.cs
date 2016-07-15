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
    public class TestCards
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
        public async Task TestPreviewCards()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                var f = col.NewNote();
                f.SetItem("Front", "1");
                f.SetItem("Back", "2");

                //Non empty and active
                var cards = col.PreviewCards(f, Collection.PreviewType.NonEmpty);
                Assert.AreEqual(1, cards.Count);
                Assert.AreEqual(0, cards[0].Ord);

                //All templates
                cards = col.PreviewCards(f, Collection.PreviewType.AllTemplates);
                Assert.AreEqual(1, cards.Count);

                //Add the note, and then test exising preview
                col.AddNote(f);
                cards = col.PreviewCards(f, Collection.PreviewType.Existing);
                Assert.AreEqual(1, cards.Count);
                Assert.AreEqual(0, cards[0].Ord);
                
                //Make sure we haven't accidentally added cards to the db
                Assert.AreEqual(1, col.CardCount());
            }
        }

        [TestMethod]
        public async Task TestDelete()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                var f = col.NewNote();
                f.SetItem("Front", "1");
                f.SetItem("Back", "2");
                col.AddNote(f);
                var cid = f.Cards()[0].Id;
                col.Reset();
                col.Sched.AnswerCard(col.Sched.PopCard(), Sched.AnswerEase.Hard);
                col.RemoveCardsAndNoteIfNoCardsLeft(new long[] { cid });
                Assert.AreEqual(0, col.CardCount());
                Assert.AreEqual(0, col.NoteCount());
                Assert.AreEqual(0, col.Database.QueryScalar<int>("select count() from notes"));
                Assert.AreEqual(0, col.Database.QueryScalar<int>("select count() from cards"));
                Assert.AreEqual(2, col.Database.QueryScalar<int>("select count() from graves"));
            }
        }

        [TestMethod]
        public async Task TestMisc()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                var f = col.NewNote();
                f.SetItem("Front", "1");
                f.SetItem("Back", "2");
                col.AddNote(f);
                var c = f.Cards()[0];
                Assert.AreEqual(0, c.GetTemplate()["ord"].GetNumber());
            }
        }

        [TestMethod]
        public async Task TestGenRem()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                var f = col.NewNote();
                f.SetItem("Front", "1");
                col.AddNote(f);
                Assert.AreEqual(1, f.Cards().Count);
                var m = col.Models.GetCurrent();
                var mm = col.Models;

                //Adding a new template should automatically create cards
                var t = mm.NewTemplate("rev");
                t["qfmt"] = JsonValue.CreateStringValue("{{Front}}");
                t["afmt"] = JsonValue.CreateStringValue("");
                mm.AddTemplate(m, t);
                mm.Save(m, true);
                Assert.AreEqual(2, f.Cards().Count);

                //if the template is changed to remove cards, they'll be removed
                t["qfmt"] = JsonValue.CreateStringValue("{{Back}}");
                mm.Save(m, true);
                col.RemoveCardsAndNoteIfNoCardsLeft(col.EmptyCids().ToArray());
                Assert.AreEqual(1, f.Cards().Count);

                //If we add to the note, a card should be automatically generated
                f.LoadFromDatabase();
                f.SetItem("Back", "1");
                f.SaveChangesToDatabase();
                Assert.AreEqual(2, f.Cards().Count);
            }
        }

        [TestMethod]
        public async Task TestGenDeck()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                var cloze = col.Models.GetModelByName("Cloze");
                col.Models.SetCurrent(cloze);
                var f = col.NewNote();
                string str = "{{c1::one}}";
                f.SetItem("Text", str);
                col.AddNote(f);
                Assert.AreEqual(1, f.Cards().Count);
                Assert.AreEqual(1, f.Cards()[0].DeckId);

                //Set the model to a new default deck
                var newID = col.Deck.AddOrResuedDeck("new");
                cloze["did"] = JsonValue.CreateNumberValue((long)newID);
                col.Models.Save(cloze);

                //A newly generated card should share the first card's deck
                str += "{{c2::two}}";
                f.SetItem("Text", str);
                f.SaveChangesToDatabase();
                Assert.AreEqual(1, f.Cards()[1].DeckId);

                //And same with multiple cards
                str += "{{c3::three}}";
                f.SetItem("Text", str);
                f.SaveChangesToDatabase();
                Assert.AreEqual(1, f.Cards()[2].DeckId);

                //If one of the cards is in a different deck, 
                //it should revert to the default model
                var c = f.Cards()[1];
                c.DeckId = (long)newID;
                c.SaveChangesToDatabase();
                str += "{{c4::four}}";
                f.SetItem("Text", str);
                f.SaveChangesToDatabase();
                Assert.AreEqual(newID, f.Cards()[3].DeckId);
            }
        }



    }
}
