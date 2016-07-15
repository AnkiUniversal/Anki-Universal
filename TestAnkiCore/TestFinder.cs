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
using AnkiU;

namespace TestAnkiCore
{
    [TestClass]
    public class TestFinder
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
        public void TestTokenize()
        {
            var f = new Finder(null);
            string s = "hello world";
            string[] expected = new string[] { "hello", "world" };
            string[] actual = f.Tokenize(s);
            Assert.IsTrue(Utils.CompareArray(expected, actual), actual.PrintArray());

            s = "hello  world";
            expected = new string[] { "hello", "world" };
            actual = f.Tokenize(s);
            Assert.IsTrue(Utils.CompareArray(expected, actual), actual.PrintArray());

            s = "one -two";
            expected = new string[] { "one", "-", "two" };
            actual = f.Tokenize(s);
            Assert.IsTrue(Utils.CompareArray(expected, actual), actual.PrintArray());

            s = "one --two";
            expected = new string[] { "one", "-", "two" };
            actual = f.Tokenize(s);
            Assert.IsTrue(Utils.CompareArray(expected, actual), actual.PrintArray());

            s = "one - two";
            expected = new string[] { "one", "-", "two" };
            actual = f.Tokenize(s);
            Assert.IsTrue(Utils.CompareArray(expected, actual), actual.PrintArray());

            s = "one or -two";
            expected = new string[] { "one", "or", "-", "two" };
            actual = f.Tokenize(s);
            Assert.IsTrue(Utils.CompareArray(expected, actual), actual.PrintArray());

            s = "'hello \"world\"'";
            expected = new string[] { "hello \"world\"" };
            actual = f.Tokenize(s);
            Assert.IsTrue(Utils.CompareArray(expected, actual), actual.PrintArray());

            s = "\"hello world\"";
            expected = new string[] { "hello world" };
            actual = f.Tokenize(s);
            Assert.IsTrue(Utils.CompareArray(expected, actual), actual.PrintArray());

            s = "one (two or ( three or four))";
            expected = new string[] { "one", "(", "two", "or", "(", "three", "or", "four",
                                    ")", ")" };
            actual = f.Tokenize(s);
            Assert.IsTrue(Utils.CompareArray(expected, actual), actual.PrintArray());

            s = "embedded'string";
            expected = new string[] { "embedded'string" };
            actual = f.Tokenize(s);
            Assert.IsTrue(Utils.CompareArray(expected, actual), actual.PrintArray());

            s = "deck:'two words'";
            expected = new string[] { "deck:two words" };
            actual = f.Tokenize(s);
            Assert.IsTrue(Utils.CompareArray(expected, actual), actual.PrintArray());
        }

        [TestMethod]
        public async Task TestFindCard()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                var f = col.NewNote();
                f.SetItem("Front", "dog");
                f.SetItem("Back", "cat");
                f.Tags.Add("monkey");
                var f1id = f.Id;
                col.AddNote(f);
                var firstCardId = f.Cards()[0].Id;

                f = col.NewNote();
                f.SetItem("Front", "goats are fun");
                f.SetItem("Back", "sheep");
                f.Tags.Add("sheep goat horse");
                col.AddNote(f);
                var f2id = f.Id;

                f = col.NewNote();
                f.SetItem("Front", "cat");
                f.SetItem("Back", "sheep");
                col.AddNote(f);
                var catCard = f.Cards()[0];
                var m = col.Models.GetCurrent();
                var mm = col.Models;
                var t = mm.NewTemplate("Reverse");
                t["qfmt"] = JsonValue.CreateStringValue("{{Back}}");
                t["afmt"] = JsonValue.CreateStringValue("{{Front}}");
                mm.AddTemplate(m, t);
                mm.Save(m);
                f = col.NewNote();
                f.SetItem("Front", "test");
                f.SetItem("Back", "foo bar");
                col.AddNote(f);
                var latestCardIds = (from s in f.Cards() select s.Id).ToList();

                //Tag searches
                Assert.AreEqual(0, col.FindCards("tag:donkey").Count);
                Assert.AreEqual(1, col.FindCards("tag:sheep").Count);
                Assert.AreEqual(1, col.FindCards("tag:sheep tag:goat").Count);
                Assert.AreEqual(0, col.FindCards("tag:sheep tag:monkey").Count);
                Assert.AreEqual(1, col.FindCards("tag:monkey").Count);
                Assert.AreEqual(1, col.FindCards("tag:sheep -tag:monkey").Count);
                Assert.AreEqual(4, col.FindCards("-tag:sheep").Count);

                var list = col.Database.QueryColumn<NoteTable>("select id from notes");
                var nid = (from s in list select s.Id).ToList();
                col.Tags.BulkAdd(nid, "foo bar");
                Assert.AreEqual(5, col.FindCards("tag:foo").Count);
                Assert.AreEqual(5, col.FindCards("tag:bar").Count);

                col.Tags.BulkRem(nid, "foo");
                Assert.AreEqual(0, col.FindCards("tag:foo").Count);
                Assert.AreEqual(5, col.FindCards("tag:bar").Count);

                //Text searches
                Assert.AreEqual(2, col.FindCards("cat").Count);
                Assert.AreEqual(1, col.FindCards("cat -dog").Count);
                Assert.AreEqual(0, col.FindCards("\"are goats\"").Count);
                Assert.AreEqual(1, col.FindCards("\"goats are\"").Count);

                //Card states
                var c = f.Cards()[0];
                c.Queue = 2;
                c.Type = CardType.Review;
                Assert.AreEqual(0, col.FindCards("is:review").Count);
                c.SaveChangesToDatabase();
                Assert.AreEqual(1, col.FindCards("is:review").Count);
                Assert.AreEqual(c.Id, col.FindCards("is:review")[0]);
                Assert.AreEqual(0, col.FindCards("is:due").Count);
                c.Due = 0;
                c.Queue = 2;
                c.SaveChangesToDatabase();
                Assert.AreEqual(1, col.FindCards("is:due").Count);
                Assert.AreEqual(c.Id, col.FindCards("is:due")[0]);
                Assert.AreEqual(4, col.FindCards("-is:due").Count);

                //Ensure this card gets a later modified time
                c.Queue = -1;
                c.SaveChangesToDatabase();
                col.Database.Execute("update cards set mod = mod + 1 where id = ?", c.Id);
                Assert.AreEqual(1, col.FindCards("is:suspended").Count);
                Assert.AreEqual(c.Id, col.FindCards("is:suspended")[0]);

                //Nids
                Assert.AreEqual(0, col.FindCards("nid:54321").Count);
                Assert.AreEqual(2, col.FindCards("nid:" + f.Id).Count);
                Assert.AreEqual(2, col.FindCards("nid:" + f1id +"," +f2id).Count);

                //Templates
                Assert.AreEqual(0, col.FindCards("card:foo").Count);
                Assert.AreEqual(4, col.FindCards("'card:card 1'").Count);
                Assert.AreEqual(1, col.FindCards("card:reverse").Count);
                Assert.AreEqual(4, col.FindCards("card:1").Count);
                Assert.AreEqual(1, col.FindCards("card:2").Count);

                //Field
                Assert.AreEqual(1, col.FindCards("front:dog").Count);
                Assert.AreEqual(4, col.FindCards("-front:dog").Count);
                Assert.AreEqual(0, col.FindCards("front:sheep").Count);
                Assert.AreEqual(2, col.FindCards("back:sheep").Count);
                Assert.AreEqual(3, col.FindCards("-back:sheep").Count);
                Assert.AreEqual(0, col.FindCards("front:do").Count);
                Assert.AreEqual(5, col.FindCards("front:*").Count);

                //Ordering
                col.Conf["sortType"] = JsonValue.CreateStringValue("noteCrt");
                var found = col.FindCards("front:*", true);
                Assert.IsTrue(latestCardIds.Contains(found[found.Count - 1])
                        , "LatestCardIds does not have: " + found[found.Count - 1]);
                found = col.FindCards("", true);
                Assert.IsTrue(latestCardIds.Contains(found[found.Count - 1])
                        , "LatestCardIds does not have: " + found[found.Count - 1]);

                col.Conf["sortType"] = JsonValue.CreateStringValue("noteFld");
                found = col.FindCards("", true);
                Assert.AreEqual(catCard.Id, found[0]);
                found = col.FindCards("", true);
                Assert.IsTrue(latestCardIds.Contains(found[found.Count - 1])
                        , "LatestCardIds does not have: " + found[found.Count - 1]);

                col.Conf["sortType"] = JsonValue.CreateStringValue("cardMod");
                found = col.FindCards("", true);
                Assert.AreEqual(firstCardId, found[0]);
                found = col.FindCards("", true);
                Assert.IsTrue(latestCardIds.Contains(found[found.Count - 1])
                        , "LatestCardIds does not have: " + found[found.Count - 1]);

                col.Conf["sortBackwards"] = JsonValue.CreateBooleanValue(true);
                found = col.FindCards("", true);
                Assert.IsTrue(latestCardIds.Contains(found[0])
                        , "LatestCardIds does not have: " + found[0]);

                //Model
                Assert.AreEqual(5, col.FindCards("note:basic").Count);
                Assert.AreEqual(0, col.FindCards("-note:basic").Count);
                Assert.AreEqual(5, col.FindCards("-note:foo").Count);

                //Deck
                Assert.AreEqual(5, col.FindCards("deck:default").Count);
                Assert.AreEqual(0, col.FindCards("-deck:default").Count);
                Assert.AreEqual(5, col.FindCards("-deck:foo").Count);
                Assert.AreEqual(5, col.FindCards("deck:def*").Count);
                Assert.AreEqual(5, col.FindCards("deck:*EFAULT").Count);
                Assert.AreEqual(0, col.FindCards("deck:*cefault").Count);

                //Full search
                f = col.NewNote();
                f.SetItem("Front", "hello<b>world</b>");
                f.SetItem("Back", "abc");
                col.AddNote(f);

                //As it's the sort field, it matches
                Assert.AreEqual(2, col.FindCards("helloworld").Count);

                //If we put it on the back, it won't
                f.SetItem("Front", "abc");
                f.SetItem("Back", "hello<b>world</b>");
                f.SaveChangesToDatabase();
                Assert.AreEqual(0, col.FindCards("helloworld").Count);

                //Searching for an invalid special tag should not error
                Assert.AreEqual(0, col.FindCards("is:invalid").Count);

                //should be able to limit to parent deck, no children
                var id = col.Database.QueryScalar<long>("select id from cards limit 1");
                var did = (long)col.Deck.AddOrResuedDeck("Default::Child");
                col.Database.Execute("update cards set did = ? where id = ?", did, id);
                Assert.AreEqual(7, col.FindCards("deck:default").Count);
                Assert.AreEqual(1, col.FindCards("deck:default::child").Count);
                Assert.AreEqual(6, col.FindCards("deck:default -deck:default::*").Count);

                //Properties
                id = col.Database.QueryScalar<long>("select id from cards limit 1");
                col.Database.Execute("update cards set queue = 2, ivl = 10, reps = 20, due = 30, factor = 2200 " +
                                    "where id = ?", id);

                Assert.AreEqual(1, col.FindCards("prop:ivl>5").Count);
                Assert.IsTrue(col.FindCards("prop:ivl<5").Count > 1);
                Assert.AreEqual(1, col.FindCards("prop:ivl>=5").Count);
                Assert.AreEqual(0, col.FindCards("prop:ivl=9").Count);
                Assert.AreEqual(1, col.FindCards("prop:ivl=10").Count);
                Assert.IsTrue(col.FindCards("prop:ivl!=10").Count > 1);
                Assert.AreEqual(1, col.FindCards("prop:due>0").Count);

                //Due dates should work
                col.Sched.Today = 15;
                Assert.AreEqual(0, col.FindCards("prop:due=14").Count);
                Assert.AreEqual(1, col.FindCards("prop:due=15").Count);
                Assert.AreEqual(0, col.FindCards("prop:due=16").Count);

                //Including negatives
                col.Sched.Today = 32;
                Assert.AreEqual(0, col.FindCards("prop:due=-1").Count);
                Assert.AreEqual(1, col.FindCards("prop:due=-2").Count);

                //Ease factors
                Assert.AreEqual(0, col.FindCards("prop:ease=2.3").Count);
                Assert.AreEqual(1, col.FindCards("prop:ease=2.2").Count);
                Assert.AreEqual(1, col.FindCards("prop:ease>2").Count);
                Assert.IsTrue(col.FindCards("-prop:ease>2").Count > 1);

                //Recently failed
                Assert.AreEqual(0, col.FindCards("rated:1:1").Count);
                Assert.AreEqual(0, col.FindCards("rated:1:2").Count);

                c = col.Sched.PopCard();
                col.Sched.AnswerCard(c, Sched.AnswerEase.Hard);
                Assert.AreEqual(0, col.FindCards("rated:1:1").Count);
                Assert.AreEqual(1, col.FindCards("rated:1:2").Count);

                c = col.Sched.PopCard();
                col.Sched.AnswerCard(c, Sched.AnswerEase.Again);
                Assert.AreEqual(1, col.FindCards("rated:1:1").Count);
                Assert.AreEqual(1, col.FindCards("rated:1:2").Count);
                Assert.AreEqual(2, col.FindCards("rated:1").Count);
                Assert.AreEqual(0, col.FindCards("rated:0:2").Count);
                Assert.AreEqual(1, col.FindCards("rated:2:2").Count);

                //Empty field
                Assert.AreEqual(0 , col.FindCards("front:").Count);
                f = col.NewNote();
                f.SetItem("Front", "");
                f.SetItem("Back", "abc2");
                Assert.AreEqual(1, col.AddNote(f));
                Assert.AreEqual(1, col.FindCards("front:").Count);

                //OR searches and nesting
                Assert.AreEqual(2, col.FindCards("tag:monkey or tag:sheep").Count);
                Assert.AreEqual(2, col.FindCards("(tag:monkey OR tag:sheep)").Count);
                Assert.AreEqual(6, col.FindCards("-(tag:monkey OR tag:sheep)").Count);
                Assert.AreEqual(2, col.FindCards("tag:monkey or (tag:sheep sheep)").Count);
                Assert.AreEqual(1, col.FindCards("tag:monkey or (tag:sheep octopus)").Count);

                //Invalid grouping shouldn't error
                Assert.AreEqual(0, col.FindCards(")").Count);
                Assert.AreEqual(0, col.FindCards("(()").Count);

                //Added
                Assert.AreEqual(0, col.FindCards("added:0").Count);
                col.Database.Execute("update cards set id = id - 86400*1000 where id = ?", id);
                Assert.AreEqual(col.CardCount() - 1, col.FindCards("added:1").Count);
                Assert.AreEqual(col.CardCount(), col.FindCards("added:2").Count);
            }
        }

        [TestMethod]
        public async Task TestFindReplace()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                var f = col.NewNote();
                f.SetItem("Front", "foo");
                f.SetItem("Back", "bar");
                col.AddNote(f);

                var f2 = col.NewNote();
                f2.SetItem("Front", "baz");
                f2.SetItem("Back", "foo");
                col.AddNote(f2);

                var nids = new List<long> { f.Id, f2.Id };

                //Should do nothing
                Assert.AreEqual(0, col.FindReplace(nids, "abc", "123"));

                //Global replace
                Assert.AreEqual(2, col.FindReplace(nids, "foo", "qux"));
                f.LoadFromDatabase();
                f2.LoadFromDatabase();
                Assert.AreEqual("qux", f.GetItem("Front"));
                Assert.AreEqual("qux", f2.GetItem("Back"));

                //Single field replace
                Assert.AreEqual(1, col.FindReplace(nids, "qux", "foo", field: "Front"));
                f.LoadFromDatabase();
                f2.LoadFromDatabase();
                Assert.AreEqual("foo", f.GetItem("Front"));
                Assert.AreEqual("qux", f2.GetItem("Back"));

                //Regex replace
                Assert.AreEqual(0, col.FindReplace(nids, "B.r", "reg"));
                f.LoadFromDatabase();
                Assert.AreNotEqual("reg", f.GetItem("Back"));
                Assert.AreEqual(1, col.FindReplace(nids, "B.r", "reg", regex: true));
                f.LoadFromDatabase();
                Assert.AreEqual("reg", f.GetItem("Back"));
            }
        }


        [TestMethod]
        public async Task TestFindDupes()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                var f = col.NewNote();
                f.SetItem("Front", "foo");
                f.SetItem("Back", "bar");
                col.AddNote(f);

                var f2 = col.NewNote();
                f2.SetItem("Front", "baz");
                f2.SetItem("Back", "bar");
                col.AddNote(f2);

                var f3 = col.NewNote();
                f3.SetItem("Front", "quux");
                f3.SetItem("Back", "bar");
                col.AddNote(f3);

                var f4 = col.NewNote();
                f4.SetItem("Front", "quuux");
                f4.SetItem("Back", "nope");
                col.AddNote(f4);

                var r = col.FindDupes("Back");
                Assert.AreEqual("bar", r[0].Key);
                Assert.AreEqual(3, r[0].Value.Count);

                //Valid search
                r = col.FindDupes("Back", "bar");
                Assert.AreEqual("bar", r[0].Key);
                Assert.AreEqual(3, r[0].Value.Count);

                //Excludes everything
                r = col.FindDupes("Back", "invalid");
                Assert.AreEqual(0, r.Count);
                r = col.FindDupes("Front");
                Assert.AreEqual(0, r.Count);
            }
        }

    }
}
