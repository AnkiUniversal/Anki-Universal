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
    public class TestDeck
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
        public async Task TestBasic()
        {
            using (Collection deck = await Utils.GetEmptyCollection(tempFolder))
            {
                //We start with a standard deck
                Assert.AreEqual(1, deck.Deck.DeckDict.Count);

                //It should have an id of 1
                Assert.IsNotNull(deck.Deck.GetDeckName(1));

                //Create a new deck
                var parentId = deck.Deck.AddOrResuedDeck("new deck");
                Assert.IsTrue(parentId > 0);
                Assert.AreEqual(2, deck.Deck.DeckDict.Count);

                //Should get the same id
                Assert.AreEqual(parentId, deck.Deck.AddOrResuedDeck("new deck"));

                //We start with the default deck selected
                Assert.AreEqual(1, deck.Deck.Selected());
                Assert.AreEqual(1, deck.Deck.Active().Count);

                //We can select a different deck
                deck.Deck.Select((long)parentId);
                Assert.AreEqual(parentId, deck.Deck.Selected());
                Assert.IsTrue(deck.Deck.Active().Contains((long)parentId));

                //Let's create a child
                var childId = deck.Deck.AddOrResuedDeck("new deck::child");

                //It should have been added to the active list
                Assert.AreEqual(parentId, deck.Deck.Selected());
                Assert.IsTrue(deck.Deck.Active().Contains((long)parentId));
                Assert.IsTrue(deck.Deck.Active().Contains((long)childId));

                //We can select the child individually too
                deck.Deck.Select((long)childId);
                Assert.AreEqual(childId, deck.Deck.Selected());
                Assert.AreEqual(1, deck.Deck.Active().Count);
                Assert.IsTrue(deck.Deck.Active().Contains((long)childId));

                //Parents with a different case should be handled correctly
                deck.Deck.AddOrResuedDeck("ONE");
                var m = deck.Models.GetCurrent();
                m["did"] = JsonValue.CreateNumberValue((long)deck.Deck.AddOrResuedDeck("one::two"));
                deck.Models.Save(m);
                var n = deck.NewNote();
                n.SetItem("Front", "abc");
                deck.AddNote(n);

                //This will error if child and parent case don't match
                deck.Sched.DeckDueList();
            }
        }

        [TestMethod]
        public async Task TestParentChildOrder()
        {
            using (Collection deck = await Utils.GetEmptyCollection(tempFolder))
            {
                //Create a new deck
                var newDeckId = deck.Deck.AddOrResuedDeck("Default::foo");
                var active = deck.Deck.Active();
                Assert.AreEqual(1, active.First());
                active.RemoveFirst();
                Assert.AreEqual(newDeckId, active.First());
            }
        }

        [TestMethod]
        public async Task TestRemove()
        {
            using (Collection deck = await Utils.GetEmptyCollection(tempFolder))
            {
                var g1 = deck.Deck.AddOrResuedDeck("g1");
                var f = deck.NewNote();
                f.SetItem("Front", "1");
                f.Model["did"] = JsonValue.CreateNumberValue((long)g1);
                deck.AddNote(f);
                var c = f.Cards()[0];
                Assert.AreEqual(g1, c.DeckId);

                //By default deleting the deck leaves the cards with an invalid did
                Assert.AreEqual(1, deck.CardCount());
                deck.Deck.Remove((long)g1);
                Assert.AreEqual(1, deck.CardCount());
                c.LoadFromDatabase();
                Assert.AreEqual(g1, c.DeckId);

                //But if we try to get it, we get the default
                Assert.AreEqual("[no deck]", deck.Deck.GetDeckName(c.DeckId));

                //Let's create another deck and explicitly set the card to it
                var g2 = deck.Deck.AddOrResuedDeck("g2");
                c.DeckId = (long)g2;
                c.SaveChangesToDatabase();

                //This time we'll delete the card/note too
                deck.Deck.Remove((long)g2, cardsToo: true);
                Assert.AreEqual(0, deck.CardCount());
                Assert.AreEqual(0, deck.NoteCount());
            }
        }

        [TestMethod]
        public async Task TestRename()
        {
            using (Collection deck = await Utils.GetEmptyCollection(tempFolder))
            {
                var id = deck.Deck.AddOrResuedDeck("hello::world");

                //Should be able to rename into a completely different branch, 
                //creating parents as necessary
                deck.Deck.Rename(deck.Deck.Get(id), "foo::bar");
                Assert.IsTrue(deck.Deck.AllNames().Contains("foo"));
                Assert.IsTrue(deck.Deck.AllNames().Contains("foo::bar"));
                Assert.IsFalse(deck.Deck.AllNames().Contains("hello::world"));

                //Create another deck
                id = deck.Deck.AddOrResuedDeck("tmp");

                //We can't rename it if it conflicts
                try
                {
                    deck.Deck.Rename(deck.Deck.Get(id), "foo");
                    Assert.Fail();
                }
                catch(DeckRenameException)
                {

                }

                //When renaming, the children should be renamed too
                deck.Deck.AddOrResuedDeck("one::two::three");
                id = deck.Deck.AddOrResuedDeck("one");
                deck.Deck.Rename(deck.Deck.Get(id), "yo");
                Assert.IsTrue(deck.Deck.AllNames().Contains("yo"));
                Assert.IsTrue(deck.Deck.AllNames().Contains("yo::two"));
                Assert.IsTrue(deck.Deck.AllNames().Contains("yo::two::three"));
            }
        }

        private List<string> GetSortedDeckNamesWithoutDefault(Collection deck)
        {
            var names = deck.Deck.AllNames();
            names.Sort();
            var list = from s in names where s.ToString() != "Default" select s;
            return list.ToList();
        }
       
        [TestMethod]
        public async Task TestRenameForDragAndDrop()
        {
            using (Collection deck = await Utils.GetEmptyCollection(tempFolder))
            {
                List<string> nameList = new List<string>() { "Languages", "Chinese", "Chinese::HSK" };
                var LangDid = deck.Deck.AddOrResuedDeck(nameList[0]);
                var ChineseDid = deck.Deck.AddOrResuedDeck(nameList[1]);
                var HskDid = deck.Deck.AddOrResuedDeck(nameList[2]);

                //Renaming also renames children
                deck.Deck.RenameForDragAndDrop((long)ChineseDid, LangDid);
                var names = GetSortedDeckNamesWithoutDefault(deck);
                List<string> expected = new List<string>() { "Languages", "Languages::Chinese", "Languages::Chinese::HSK" };
                Assert.IsTrue(Utils.CompareLists(names, expected));

                //Dragging a deck onto itself is a no-op
                deck.Deck.RenameForDragAndDrop((long)LangDid, LangDid);
                names = GetSortedDeckNamesWithoutDefault(deck);
                Assert.IsTrue(Utils.CompareLists(names, expected));

                //Dragging a deck onto its parent is a no-op
                deck.Deck.RenameForDragAndDrop((long)HskDid, ChineseDid);
                names = GetSortedDeckNamesWithoutDefault(deck);
                Assert.IsTrue(Utils.CompareLists(names, expected));

                //Dragging a deck onto a descendant is a no-op
                deck.Deck.RenameForDragAndDrop((long)LangDid, HskDid);
                names = GetSortedDeckNamesWithoutDefault(deck);
                Assert.IsTrue(Utils.CompareLists(names, expected));

                //Can drag a grandchild onto its grandparent.  It becomes a child
                deck.Deck.RenameForDragAndDrop((long)HskDid, LangDid);
                names = GetSortedDeckNamesWithoutDefault(deck);
                expected = new List<string>() { "Languages", "Languages::Chinese", "Languages::HSK" };
                Assert.IsTrue(Utils.CompareLists(names, expected));

                //Can drag a deck onto its sibling
                deck.Deck.RenameForDragAndDrop((long)HskDid, ChineseDid);
                names = GetSortedDeckNamesWithoutDefault(deck);
                expected = new List<string>() { "Languages", "Languages::Chinese", "Languages::Chinese::HSK" };
                Assert.IsTrue(Utils.CompareLists(names, expected));

                //Can drag a deck back to the top level
                deck.Deck.RenameForDragAndDrop((long)ChineseDid, null);
                names = GetSortedDeckNamesWithoutDefault(deck);
                expected = new List<string>() { "Chinese", "Chinese::HSK", "Languages" };
                Assert.IsTrue(Utils.CompareLists(names, expected));

                //Dragging a top level deck to the top level is a no-op
                deck.Deck.RenameForDragAndDrop((long)ChineseDid, null);
                names = GetSortedDeckNamesWithoutDefault(deck);
                Assert.IsTrue(Utils.CompareLists(names, expected));
            }
        }

    }
}
