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
    public class TestSched
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

        private long Now()
        {
            return DateTimeOffset.Now.ToUnixTimeSeconds();
        }

        [TestMethod]
        public async Task TestClock()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                if (col.Sched.DayCutoff - Now() < 10 * 60)
                    Assert.Fail("Unit tests will fail around the day rollover");
            }
        }

        [TestMethod]
        public async Task TestBasic()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                col.Reset();
                Assert.IsNull(col.Sched.PopCard());
            }
        }

        [TestMethod]
        public async Task TestNew()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                col.Reset();
                Assert.AreEqual(0, col.Sched.TotalNewCountForCurrentDecks());

                //Add a note
                var f = col.NewNote();
                f.SetItem("Front", "one");
                f.SetItem("Back", "two");
                col.AddNote(f);
                col.Reset();
                Assert.AreEqual(1, col.Sched.TotalNewCountForCurrentDecks());

                //Fetch it
                var c = col.Sched.PopCard();
                Assert.IsNotNull(c);
                Assert.AreEqual(0, c.Queue);
                Assert.AreEqual(CardType.New, c.Type);

                //If we answer it, it should become a learn card
                var t = Now();
                col.Sched.AnswerCard(c, Sched.AnswerEase.Again);
                Assert.AreEqual(1, c.Queue);
                Assert.AreEqual(CardType.Learn, c.Type);
                Assert.IsTrue(c.Due > t, "Card due is wrong!");
            }
        }

        [TestMethod]
        public async Task TestNewLimits()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                var g2 = col.Deck.AddOrResuedDeck("Default::foo");
                for(int i = 0; i <30; i++)
                {
                    var f = col.NewNote();
                    f.SetItem("Front", i.ToString());
                    if (i > 4)
                        f.Model["did"] = JsonValue.CreateNumberValue((long)g2);
                    col.AddNote(f);
                }

                //Give the child deck a different configuration
                var c2 = col.Deck.CreateNewConfiguration("new conf");
                col.Deck.SetConf(col.Deck.Get(g2), c2);
                col.Reset();

                //Both confs have defaulted to a limit of 20
                Assert.AreEqual(20, col.Sched.NewCount);

                //First card we get comes from parent
                var c = col.Sched.PopCard();
                Assert.AreEqual(1, c.DeckId);

                //Limit the parent to 10 cards, meaning we get 10 in total
                var conf1 = col.Deck.ConfForDeckId(1);
                conf1.GetNamedObject("new")["perDay"] = JsonValue.CreateNumberValue(10);
                col.Reset();
                Assert.AreEqual(10, col.Sched.NewCount);

                //If we limit child to 4, we should get 9
                var conf2 = col.Deck.ConfForDeckId((long)g2);
                conf2.GetNamedObject("new")["perDay"] = JsonValue.CreateNumberValue(4);
                col.Reset();
                Assert.AreEqual(9, col.Sched.NewCount);
            }
        }

        [TestMethod]
        public async Task TestNewBoxes()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                var f = col.NewNote();
                f.SetItem("Front", "one");
                col.AddNote(f);
                col.Reset();
                var c = col.Sched.PopCard();
                JsonArray ja = new JsonArray();
                ja.Add(JsonValue.CreateNumberValue(1));
                ja.Add(JsonValue.CreateNumberValue(2));
                ja.Add(JsonValue.CreateNumberValue(3));
                ja.Add(JsonValue.CreateNumberValue(4));
                ja.Add(JsonValue.CreateNumberValue(5));
                col.Sched.CardConf(c).GetNamedObject("new")["delays"] = ja;
                col.Sched.AnswerCard(c, Sched.AnswerEase.Hard);

                //Should handle gracefully
                ja.Clear();
                ja.Add(JsonValue.CreateNumberValue(1));
                col.Sched.CardConf(c).GetNamedObject("new")["delays"] = ja;
                col.Sched.AnswerCard(c, Sched.AnswerEase.Hard);
            }
        }

        [TestMethod]
        public async Task TestLearn()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                var f = col.NewNote();
                f.SetItem("Front", "one");
                f.SetItem("Back", "two");
                col.AddNote(f);
                col.Reset();

                //Set as a learn card and rebuild queues
                col.Database.Execute("update cards set queue=0, type=0");
                col.Reset();

                //Sched.PopCard should return it, since it's due in the past
                var c = col.Sched.PopCard();
                Assert.IsNotNull(c, "Card not found!");
                JsonArray ja = new JsonArray();
                ja.Add(JsonValue.CreateNumberValue(0.5));
                ja.Add(JsonValue.CreateNumberValue(3));
                ja.Add(JsonValue.CreateNumberValue(10));
                col.Sched.CardConf(c).GetNamedObject("new")["delays"] = ja;
                //Fail it
                col.Sched.AnswerCard(c, Sched.AnswerEase.Again);

                //It should have three reps left to graduation
                Assert.AreEqual(3, c.Left % 1000);
                Assert.AreEqual(3, c.Left / 1000);

                //It should by due in 30 seconds
                var t = c.Due - Now();
                Assert.IsTrue( t >= 25 && t <= 40, "Card due is not in 30 seconds: " + t.ToString());

                //Pass it once
                col.Sched.AnswerCard(c, Sched.AnswerEase.Hard);

                //It should by due in 3 minutes
                t = c.Due - Now();
                Assert.IsTrue(t >= 179 && t <= 180, "Card due is not in 3 minutes: " + t.ToString());
                Assert.AreEqual(2, c.Left % 1000);
                Assert.AreEqual(2, c.Left / 1000);

                //Check log is accurate
                var Log = col.Database.QueryColumn<revlog>("select * from revlog order by id desc");
                Assert.AreEqual(2, Log[0].Ease);
                Assert.AreEqual(-180, Log[0].Interval);
                Assert.AreEqual(-30, Log[0].LastInterval);

                //Pass again
                col.Sched.AnswerCard(c, Sched.AnswerEase.Hard);

                //It should by due in 10 minutes
                t = c.Due - Now();
                Assert.IsTrue(t >= 599 && t <= 600, "Card due is not in 10 minutes: " + t.ToString());
                Assert.AreEqual(1, c.Left % 1000);
                Assert.AreEqual(1, c.Left / 1000);

                //The next pass should graduate the card
                Assert.AreEqual(1, c.Queue);
                Assert.AreEqual(CardType.Learn, c.Type);
                col.Sched.AnswerCard(c, Sched.AnswerEase.Hard);
                Assert.AreEqual(2, c.Queue);
                Assert.AreEqual(CardType.Review, c.Type);

                //Should be due tomorrow, with an interval of 1
                Assert.AreEqual(col.Sched.Today + 1, c.Due);
                Assert.AreEqual(1, c.Interval);

                //Or normal removal
                c.Type = 0;
                c.Queue = 1;
                col.Sched.AnswerCard(c, Sched.AnswerEase.Good);
                Assert.AreEqual(CardType.Review, c.Type);
                Assert.AreEqual(2, c.Queue);
                Assert.IsTrue(CheckReviewLearn(col, c, 4), "Wrong review learn interval");

                //Revlog should have been updated each time
                Assert.AreEqual(5, col.Database.QueryScalar<int>("select count() from revlog where type = 0"));

                //Now failed card handling
                c.Type = CardType.Review;
                c.Queue = 1;
                c.OriginalDue = 123;
                col.Sched.AnswerCard(c, Sched.AnswerEase.Good);
                Assert.AreEqual(123, c.Due);
                Assert.AreEqual(CardType.Review, c.Type);
                Assert.AreEqual(2, c.Queue);

                //we should be able to remove manually, too
                c.Type = CardType.Review;
                c.Queue = 1;
                c.OriginalDue = 321;
                c.SaveChangesToDatabase();
                col.Sched.RemoveLearn();
                c.LoadFromDatabase();
                Assert.AreEqual(2, c.Queue);
                Assert.AreEqual(321, c.Due);
            }
        }

        private bool CheckReviewLearn(Collection col, Card card, int targetInterval)
        {
            var array = col.Sched.FuzzedIvlRange(targetInterval);
            return card.Interval >= array[0] && card.Interval <= array[1];
        }

        [TestMethod]
        public async Task TestLearnCollapsed()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                var f = col.NewNote();
                f.SetItem("Front", "1");
                col.AddNote(f);
                f = col.NewNote();
                f.SetItem("Front", "2");
                col.AddNote(f);

                //Set as a learn card and rebuild queues
                col.Database.Execute("update cards set queue=0, type=0");
                col.Reset();

                //Should get '1' first
                var c = col.Sched.PopCard();
                Assert.IsTrue(c.GetQuestionWithCss().EndsWith("1"), "Card does not end with 1");

                //Pass it so it's due in 10 minutes
                col.Sched.AnswerCard(c, Sched.AnswerEase.Hard);

                //Get the other card
                c = col.Sched.PopCard();
                Assert.IsTrue(c.GetQuestionWithCss().EndsWith("2"), "Card does not end with 2");

                //Fail it so it's due in 1 minute
                col.Sched.AnswerCard(c, Sched.AnswerEase.Again);
                //We shouldn't get the same card again
                c = col.Sched.PopCard();
                Assert.IsFalse(c.GetQuestionWithCss().EndsWith("2"), "Card end with 2");
            }
        }

        [TestMethod]
        public async Task TestLearnDay()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                var f = col.NewNote();
                f.SetItem("Front", "1");
                col.AddNote(f);
                col.Sched.Reset();
                var c = col.Sched.PopCard();
                JsonArray ja = new JsonArray();
                ja.Add(JsonValue.CreateNumberValue(1));
                ja.Add(JsonValue.CreateNumberValue(10));
                ja.Add(JsonValue.CreateNumberValue(1440));
                ja.Add(JsonValue.CreateNumberValue(2880));
                col.Sched.CardConf(c).GetNamedObject("new")["delays"] = ja;

                //Pass it
                col.Sched.AnswerCard(c, Sched.AnswerEase.Hard);

                //Two reps to graduate, 1 more today
                Assert.AreEqual(3, c.Left % 1000);
                Assert.AreEqual(1, c.Left / 1000);
                var expected = new List<int>() { 0, 1, 0 };
                var actual = col.Sched.Counts();
                Assert.IsTrue(Utils.CompareLists(expected, actual.ToList()), actual.ToString());

                c = col.Sched.PopCard();
                Assert.AreEqual(86400, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Hard));

                //Answering it will place it in queue 3
                col.Sched.AnswerCard(c, Sched.AnswerEase.Hard);
                Assert.AreEqual(col.Sched.Today + 1, c.Due);
                Assert.AreEqual(3, c.Queue);
                Assert.IsNull(col.Sched.PopCard());

                //For testing, move it back a day
                c.Due -= 1;
                c.SaveChangesToDatabase();
                col.Reset();
                actual = col.Sched.Counts();
                Assert.IsTrue(Utils.CompareLists<int>(expected, actual.ToList()), actual.ToString());
                c = col.Sched.PopCard();

                //Nextinvertval should work
                Assert.AreEqual(86400 * 2, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Hard));

                //If we fail it, it should be back in the correct queue
                col.Sched.AnswerCard(c, Sched.AnswerEase.Again);
                Assert.AreEqual(1, c.Queue);
                col.Undo();
                col.Reset();
                c = col.Sched.PopCard();
                col.Sched.AnswerCard(c, Sched.AnswerEase.Hard);

                //Simulate the passing of another two days
                c.Due -= 2;
                c.SaveChangesToDatabase();
                col.Reset();

                //The last pass should graduate it into a review card
                Assert.AreEqual(86400, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Hard));
                col.Sched.AnswerCard(c, Sched.AnswerEase.Hard);
                Assert.AreEqual(2, c.Queue);
                Assert.AreEqual(CardType.Review, c.Type);

                //If the lapse step is tomorrow, 
                //failing it should handle the counts correctly
                c.Due = 0;
                c.SaveChangesToDatabase();
                col.Reset();
                actual = col.Sched.Counts();
                expected = new List<int>() { 0, 0, 1 };
                Assert.IsTrue(Utils.CompareLists(expected, actual.ToList()), actual.ToString());

                ja.Clear();
                ja.Add(JsonValue.CreateNumberValue(1440));
                col.Sched.CardConf(c).GetNamedObject("lapse")["delays"] = ja;
                c = col.Sched.PopCard();
                col.Sched.AnswerCard(c, Sched.AnswerEase.Again);
                Assert.AreEqual(3, c.Queue);
                expected = new List<int>() { 0, 0, 0 };
                actual = col.Sched.Counts();
                Assert.IsTrue(Utils.CompareLists(expected, actual.ToList()), actual.ToString());
            }
        }

        [TestMethod]
        public async Task TestReview()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                var f = col.NewNote();
                f.SetItem("Front", "one");
                f.SetItem("Back", "two");
                col.AddNote(f);

                //Set the card up as a review card, due 8 days ago
                var c = f.Cards()[0];
                c.Type = CardType.Review;
                c.Queue = 2;
                c.Due = col.Sched.Today - 8;
                c.Factor = 2500;
                c.Reps = 3;
                c.Lapses = 1;
                c.Interval = 100;
                c.StartTimer();
                c.SaveChangesToDatabase();

                //Save it for later use as well
                var cCopy = c.ShallowClone();

                //Failing it should put it in the learn queue with the default options
                //--------------------------------------------------------------------
                //Different delay to new
                col.Reset();
                JsonArray ja = new JsonArray();
                ja.Add(JsonValue.CreateNumberValue(2));
                ja.Add(JsonValue.CreateNumberValue(20));
                col.Sched.CardConf(c).GetNamedObject("lapse")["delays"] = ja;
                col.Sched.AnswerCard(c, Sched.AnswerEase.Again);
                Assert.AreEqual(1, c.Queue);

                //It should be due tomorrow, with an interval of 1
                Assert.AreEqual(col.Sched.Today + 1, c.OriginalDue);
                Assert.AreEqual(1, c.Interval);

                //But because it's in the learn queue, 
                //its current due time should be in the future
                Assert.IsTrue(c.Due > Now(), "Due time is not in the future!");
                var diff = c.Due - Now();
                Assert.IsTrue(c.Due - Now() > 118, "Due time is not larger than 118! Due: " + diff);

                //Factor should have been decremented
                Assert.AreEqual(2300, c.Factor);

                //Chech counters
                Assert.AreEqual(2, c.Lapses);
                Assert.AreEqual(4, c.Reps);

                //Check ests
                Assert.AreEqual(120, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Again));
                Assert.AreEqual(20*60, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Hard));
                //--------------------------------------------------------------------

                //Try again with an ease of 2 instead
                //--------------------------------------------------------------------
                c = cCopy.ShallowClone();
                c.SaveChangesToDatabase();
                col.Sched.AnswerCard(c, Sched.AnswerEase.Hard);
                Assert.AreEqual(2, c.Queue);

                //The new interval should be (100 + 8/4) * 1.2 = 122
                Assert.IsTrue(CheckReviewLearn(col, c, 122), "Interval is not 122");
                Assert.AreEqual(col.Sched.Today + c.Interval, c.Due);

                //Factor should have been decreased
                Assert.AreEqual(2350, c.Factor);

                //Check counters
                Assert.AreEqual(1, c.Lapses);
                Assert.AreEqual(4, c.Reps);
                //--------------------------------------------------------------------

                //Ease 3
                //--------------------------------------------------------------------
                c = cCopy.ShallowClone();
                c.SaveChangesToDatabase();
                col.Sched.AnswerCard(c, Sched.AnswerEase.Good);
                //The new interval should be (100 + 8/2) * 2.5 = 260
                Assert.IsTrue(CheckReviewLearn(col, c, 260), "Interval is not 260");
                Assert.AreEqual(col.Sched.Today + c.Interval, c.Due);
                //Factor should have been left alone
                Assert.AreEqual(2500, c.Factor);
                //--------------------------------------------------------------------

                //Ease 4
                //--------------------------------------------------------------------
                c = cCopy.ShallowClone();
                c.SaveChangesToDatabase();
                col.Sched.AnswerCard(c, Sched.AnswerEase.Easy);
                //The new interval should be (100 + 8) * 2.5 * 1.3 = 351
                Assert.IsTrue(CheckReviewLearn(col, c, 351), "Interval is not 351");
                Assert.AreEqual(col.Sched.Today + c.Interval, c.Due);
                //Factor should have been increased
                Assert.AreEqual(2650, c.Factor);
                //--------------------------------------------------------------------

                //Leech handling
                //--------------------------------------------------------------------
                c = cCopy.ShallowClone();
                c.Lapses = 7;
                c.SaveChangesToDatabase();
                var hook = AnkiU.AnkiCore.Hooks.Hooks.GetInstance();

                //Remove the real hook to avoid unwanted side effects
                AnkiU.AnkiCore.Hooks.Leech.UnInstallHook(hook);
                hook.AddHook("leech", new LeechHook());
                col.Sched.AnswerCard(c, Sched.AnswerEase.Again);

                //Did the hook run?
                Assert.AreEqual(1, LeechHook.count);

                //Is the queue correct?
                Assert.AreEqual(-1, c.Queue);
                c.LoadFromDatabase();
                Assert.AreEqual(-1, c.Queue);
            }
        }

        private class LeechHook : AnkiU.AnkiCore.Hooks.Hook
        {
            public static int count = 0;
            public override object RunFilter(object arg, params object[] args)
            {
                return count++;
            }

            public override void RunHook(params object[] args)
            {
                count++;
            }
        }

        [TestMethod]
        public async Task TestButtonSpacing()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                col.Reset();
                Assert.AreEqual(0, col.Sched.TotalNewCountForCurrentDecks());

                //Add a note
                var f = col.NewNote();
                f.SetItem("Front", "one");
                col.AddNote(f);

                //1 Day interval review card due now
                var c = f.Cards()[0];
                c.Type = CardType.Review;
                c.Queue = 2;
                c.Due = col.Sched.Today;
                c.Reps = 1;
                c.Interval = 1;
                c.StartTimer();
                c.SaveChangesToDatabase();
                col.Reset();
                Assert.AreEqual("2.0 days", col.Sched.NextIntervalString(c, Sched.AnswerEase.Hard));
                Assert.AreEqual("3.0 days", col.Sched.NextIntervalString(c, Sched.AnswerEase.Good));
                Assert.AreEqual("4.0 days", col.Sched.NextIntervalString(c, Sched.AnswerEase.Easy));
            }
        }

        [TestMethod]
        public async Task TestFinished()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                StringAssert.Contains(col.Sched.FinishedMsg(), "Congratulations");
                Assert.IsFalse(col.Sched.FinishedMsg().Contains("limit"));

                //Add a note
                var f = col.NewNote();
                f.SetItem("Front", "one");
                f.SetItem("Back", "two");
                col.AddNote(f);

                //Have a new card
                StringAssert.Contains(col.Sched.FinishedMsg(), "new cards available");

                //Turn it into a review
                col.Reset();
                var c = f.Cards()[0];
                c.StartTimer();
                col.Sched.AnswerCard(c, Sched.AnswerEase.Good);

                //Nothing should be due tomorrow, as it's due in a week
                StringAssert.Contains(col.Sched.FinishedMsg(), "Congratulations");
                Assert.IsFalse(col.Sched.FinishedMsg().Contains("limit"));
            }
        }


        [TestMethod]
        public async Task TestNextInverval()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                var f = col.NewNote();
                f.SetItem("Front", "one");
                f.SetItem("Back", "two");
                col.AddNote(f);
                col.Reset();

                var conf = col.Deck.ConfForDeckId(1);
                JsonArray ja = new JsonArray();
                ja.Add(JsonValue.CreateNumberValue(0.5));
                ja.Add(JsonValue.CreateNumberValue(3));
                ja.Add(JsonValue.CreateNumberValue(10));
                conf.GetNamedObject("new")["delays"] = ja;
                JsonArray ja2 = new JsonArray();
                ja2.Add(JsonValue.CreateNumberValue(1));
                ja2.Add(JsonValue.CreateNumberValue(5));
                ja2.Add(JsonValue.CreateNumberValue(9));
                conf.GetNamedObject("lapse")["delays"] = ja2;
                var c = col.Sched.PopCard();

                //New cards
                Assert.AreEqual(30, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Again));
                Assert.AreEqual(180, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Hard));
                Assert.AreEqual(4 * 86400, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Good));

                //Cards in learning
                col.Sched.AnswerCard(c, Sched.AnswerEase.Again);
                Assert.AreEqual(30, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Again));
                Assert.AreEqual(180, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Hard));
                Assert.AreEqual(4 * 86400, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Good));

                col.Sched.AnswerCard(c, Sched.AnswerEase.Hard);
                Assert.AreEqual(30, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Again));
                Assert.AreEqual(600, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Hard));
                Assert.AreEqual(4 * 86400, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Good));

                col.Sched.AnswerCard(c, Sched.AnswerEase.Hard);
                //Normal graducation is tomorrow
                Assert.AreEqual(86400, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Hard));
                Assert.AreEqual(4 * 86400, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Good));

                //Lapsed cards
                c.Type = CardType.Review;
                c.Interval = 100;
                c.Factor = 2500;
                Assert.AreEqual(60, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Again));
                Assert.AreEqual(100 * 86400, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Hard));
                Assert.AreEqual(100 * 86400, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Good));

                //Review cards
                c.Queue = 2;
                c.Interval = 100;
                c.Factor = 2500;
                //Failing it should put it at 60s
                Assert.AreEqual(60, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Again));
                //or 1 day if relearn is false
                ja2.Clear();
                Assert.AreEqual(86400, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Again));
                //(* 100 1.2 86400)10368000.0
                Assert.AreEqual(10368000, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Hard));
                //(* 100 2.5 86400)21600000.0
                Assert.AreEqual(21600000, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Good));
                //(*100 2.5 1.3 86400)28080000.0
                Assert.AreEqual(28080000, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Easy));
                Assert.AreEqual("10.8 months", col.Sched.NextIntervalString(c, Sched.AnswerEase.Easy));
            }
        }

        [TestMethod]
        public async Task TestMisc()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                //Add a note
                var f = col.NewNote();
                f.SetItem("Front", "one");
                f.SetItem("Back", "two");
                col.AddNote(f);
                var c = f.Cards()[0];

                //Burying 
                col.Sched.BuryNote(c.NoteId);
                col.Reset();
                Assert.IsNull(col.Sched.PopCard());

                col.Sched.UnburyCards();
                col.Reset();
                Assert.IsNotNull(col.Sched.PopCard());
            }
        }

        [TestMethod]
        public async Task TestSuspend()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                //Add a note
                var f = col.NewNote();
                f.SetItem("Front", "one");
                f.SetItem("Back", "two");
                col.AddNote(f);
                var c = f.Cards()[0];

                //Suspending 
                col.Reset();
                Assert.IsNotNull(col.Sched.PopCard());
                var cidArray = new long[] { c.Id };
                col.Sched.SuspendCards(cidArray);
                col.Reset();
                Assert.IsNull(col.Sched.PopCard());

                //Unsuspending
                col.Sched.UnsuspendCards(cidArray);
                col.Reset();
                Assert.IsNotNull(col.Sched.PopCard());

                //should cope with rev cards being relearnt
                c.Due = 0;
                c.Interval = 100;
                c.Type = CardType.Review;
                c.Queue = 2;
                c.SaveChangesToDatabase();
                col.Reset();
                c = col.Sched.PopCard();
                col.Sched.AnswerCard(c, Sched.AnswerEase.Again);
                Assert.IsTrue(c.Due > Now());
                Assert.AreEqual(1, c.Queue);
                Assert.AreEqual(CardType.Review, c.Type);
                col.Sched.SuspendCards(cidArray);
                col.Sched.UnsuspendCards(cidArray);
                c.LoadFromDatabase();
                Assert.AreEqual(2, c.Queue);
                Assert.AreEqual(CardType.Review, c.Type);
                Assert.AreEqual(1, c.Due);

                //Should cope with cards in cram decks
                c.Due = 1;
                c.SaveChangesToDatabase();
                var cram = col.Deck.NewDynamicDeck("tmp");
                col.Sched.RebuildDyn();
                c.LoadFromDatabase();
                Assert.AreNotEqual(1, c.Due);
                Assert.AreNotEqual(1, c.DeckId);
                col.Sched.SuspendCards(cidArray);
                c.LoadFromDatabase();
                Assert.AreEqual(1, c.Due);
                Assert.AreEqual(1, c.DeckId);
            }
        }

        [TestMethod]
        public async Task TestCram()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                var f = col.NewNote();
                f.SetItem("Front", "one");
                col.AddNote(f);
                var c = f.Cards()[0];
                c.Interval = 100;
                c.Type = CardType.Review;
                c.Queue = 2;

                //Due in 25 days, so it's been waiting 75 days
                c.Due = col.Sched.Today + 25;
                c.TimeModified = 1;
                c.Factor = 2500;
                c.StartTimer();
                c.SaveChangesToDatabase();
                col.Reset();
                List<int> expected = new List<int>() { 0, 0, 0 };
                var actual = col.Sched.Counts();
                Assert.IsTrue(Utils.CompareLists(expected, actual.ToList()), "Actual: " + actual.ToString());
                var cardCopy = c.ShallowClone();

                //Create a dynamic deck and refresh it
                var did = col.Deck.NewDynamicDeck("Cram");
                col.Sched.RebuildDyn(did);
                col.Reset();

                //Should appear as new in the deck list
                var dueList = col.Sched.DeckDueList();
                dueList.Sort();
                Assert.AreEqual(1, dueList[0].NewCount);

                //And should appear in the counts
                expected = new List<int>() { 1, 0, 0 };
                actual = col.Sched.Counts();
                Assert.IsTrue(Utils.CompareLists(expected, actual.ToList()), "Actual: " + actual.ToString());

                //Grab it and check estimates
                c = col.Sched.PopCard();
                Assert.AreEqual(2, col.Sched.AnswerButtons(c));
                Assert.AreEqual(600, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Again));
                Assert.AreEqual(138 * 60 * 60 * 24, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Hard));
                var cram = col.Deck.Get(did);
                JsonArray ja = new JsonArray();
                ja.Add(JsonValue.CreateNumberValue(1));
                ja.Add(JsonValue.CreateNumberValue(10));
                cram["delays"] = ja;
                Assert.AreEqual(3, col.Sched.AnswerButtons(c));
                Assert.AreEqual(60, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Again));
                Assert.AreEqual(600, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Hard));
                Assert.AreEqual(138 * 60 * 60 * 24, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Good));
                col.Sched.AnswerCard(c, Sched.AnswerEase.Hard);
                //Elapsed time was 75 days 
                //factor = 2.5+1.2/2 = 1.85 
                //int(75*1.85) = 138
                Assert.AreEqual(138, c.Interval);
                Assert.AreEqual(138, c.OriginalDue);
                Assert.AreEqual(1, c.Queue);

                //Should be logged as a cram rep
                Assert.AreEqual(3, col.Database.QueryScalar<int>("select type from revlog order by id desc limit 1"));

                //Check invervals again
                Assert.AreEqual(60, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Again));
                Assert.AreEqual(138 * 60 * 60 * 24, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Hard));
                Assert.AreEqual(138 * 60 * 60 * 24, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Good));

                //When it graduates, due is updated
                c = col.Sched.PopCard();
                col.Sched.AnswerCard(c, Sched.AnswerEase.Hard);
                Assert.AreEqual(138, c.Interval);
                Assert.AreEqual(138, c.Due);
                Assert.AreEqual(2, c.Queue);

                //And it will move back to the previous deck
                Assert.AreEqual(1, c.DeckId);

                //Cram the deck again
                col.Sched.RebuildDyn(did);
                col.Reset();
                c = col.Sched.PopCard();

                //Check ivls again - passing should be idempotent
                Assert.AreEqual(60, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Again));
                Assert.AreEqual(600, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Hard));
                Assert.AreEqual(138 * 60 * 60 * 24, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Good));
                col.Sched.AnswerCard(c, Sched.AnswerEase.Hard);
                Assert.AreEqual(138, c.Interval);
                Assert.AreEqual(138, c.OriginalDue);

                //Fail
                col.Sched.AnswerCard(c, Sched.AnswerEase.Again);
                Assert.AreEqual(60, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Again));
                Assert.AreEqual(600, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Hard));
                Assert.AreEqual(86400, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Good));

                //Delete the deck, returning the card mid-study
                col.Deck.Remove(col.Deck.Selected());
                Assert.AreEqual(1, col.Sched.DeckDueList().Count);
                c.LoadFromDatabase();
                Assert.AreEqual(1, c.Interval);
                Assert.AreEqual(col.Sched.Today+1, c.Due);

                //Make it due
                col.Reset();
                expected = new List<int>() { 0, 0, 0 };
                actual = col.Sched.Counts();
                Assert.IsTrue(Utils.CompareLists(expected, actual.ToList()), "Actual: " + actual.ToString());
                c.Due = -5;
                c.Interval = 100;
                c.SaveChangesToDatabase();
                col.Reset();
                expected = new List<int>() { 0, 0, 1 };
                actual = col.Sched.Counts();
                Assert.IsTrue(Utils.CompareLists(expected, actual.ToList()), "Actual: " + actual.ToString());

                //Cram again
                did = col.Deck.NewDynamicDeck("Cram");
                col.Sched.RebuildDyn(did);
                col.Reset();
                expected = new List<int>() { 0, 0, 1 };
                actual = col.Sched.Counts();
                Assert.IsTrue(Utils.CompareLists(expected, actual.ToList()), "Actual: " + actual.ToString());
                c.LoadFromDatabase();
                Assert.AreEqual(4, col.Sched.AnswerButtons(c));

                //Add a sibling so we can test minSpace, etc
                var c2 = c.ShallowClone();
                c2.Id = 123;
                c2.Ord = 1;
                c2.Due = 325;
                c2.SaveChangesToDatabase();

                //Should be able to answer it
                c = col.Sched.PopCard();
                col.Sched.AnswerCard(c, Sched.AnswerEase.Easy);

                //It should have been moved back to the original deck
                Assert.AreEqual(1, c.DeckId);
            }
        }

        [TestMethod]
        public async Task TestCramRemove()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                //Add a note
                var f = col.NewNote();
                f.SetItem("Front", "one");
                col.AddNote(f);
                var c = f.Cards()[0];

                var oldDue = c.Due;
                var did = col.Deck.NewDynamicDeck("Cram");
                col.Sched.RebuildDyn(did);
                col.Reset();
                c = col.Sched.PopCard();
                col.Sched.AnswerCard(c, Sched.AnswerEase.Hard);

                //Answering the card will put it in the learning queue
                Assert.AreEqual(CardType.Learn, c.Type);
                Assert.AreEqual(1, c.Queue);
                Assert.AreNotEqual(c.Due, oldDue);

                //If we terminate cramming prematurely it should be set back to new
                col.Sched.EmptyDyn(did);
                c.LoadFromDatabase();
                Assert.AreEqual(CardType.New, c.Type);
                Assert.AreEqual(0, c.Queue);
                Assert.AreEqual(oldDue, c.Due);
            }
        }

        [TestMethod]
        public async Task TestCramResched()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                //Add a note
                var f = col.NewNote();
                f.SetItem("Front", "one");
                col.AddNote(f);
      
                var did = col.Deck.NewDynamicDeck("Cram");
                var cram = col.Deck.Get(did);
                cram["resched"] = JsonValue.CreateBooleanValue(false);
                col.Sched.RebuildDyn(did);
                col.Reset();

                //Graduate should return it to new
                var c = col.Sched.PopCard();
                Assert.AreEqual(60, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Again));
                Assert.AreEqual(600, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Hard));
                Assert.AreEqual(0, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Good));
                Assert.AreEqual("(end)", col.Sched.NextIntervalString(c, Sched.AnswerEase.Good));

                col.Sched.AnswerCard(c, Sched.AnswerEase.Good);
                Assert.AreEqual(0, c.Queue);
                Assert.AreEqual(CardType.New, c.Type);

                //Undue reviews should also be unaffected
                c.Interval = 100;
                c.Type = CardType.Review;
                c.Queue = 2;
                c.Due = col.Sched.Today + 25;
                c.Factor = 2500;
                c.SaveChangesToDatabase();
                var cardCopy = c.ShallowClone();
                col.Sched.RebuildDyn(did);
                col.Reset();
                c = col.Sched.PopCard();
                Assert.AreEqual(600, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Again));
                Assert.AreEqual(0, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Hard));
                Assert.AreEqual(0, col.Sched.NextIntervalInSeconds(c, Sched.AnswerEase.Good));

                col.Sched.AnswerCard(c, Sched.AnswerEase.Hard);
                Assert.AreEqual(100, c.Interval);
                Assert.AreEqual(col.Sched.Today + 25, c.Due);

                //Check failure too
                c = cardCopy;
                c.SaveChangesToDatabase();
                col.Sched.RebuildDyn(did);
                col.Reset();
                c = col.Sched.PopCard();
                col.Sched.AnswerCard(c, Sched.AnswerEase.Again);
                col.Sched.EmptyDyn(did);
                c.LoadFromDatabase();
                Assert.AreEqual(100, c.Interval);
                Assert.AreEqual(col.Sched.Today + 25, c.Due);

                //Fail+grad early
                c = cardCopy;
                c.SaveChangesToDatabase();
                col.Sched.RebuildDyn(did);
                col.Reset();
                c = col.Sched.PopCard();
                col.Sched.AnswerCard(c, Sched.AnswerEase.Again);
                col.Sched.AnswerCard(c, Sched.AnswerEase.Good);
                col.Sched.EmptyDyn(did);
                c.LoadFromDatabase();
                Assert.AreEqual(100, c.Interval);
                Assert.AreEqual(col.Sched.Today + 25, c.Due);

                //Due cards - pass
                c = cardCopy;
                c.Due = -25;
                c.SaveChangesToDatabase();
                col.Sched.RebuildDyn(did);
                col.Reset();
                c = col.Sched.PopCard();
                col.Sched.AnswerCard(c, Sched.AnswerEase.Good);
                col.Sched.EmptyDyn(did);
                c.LoadFromDatabase();
                Assert.AreEqual(100, c.Interval);
                Assert.AreEqual(-25, c.Due);

                //Fail
                c = cardCopy;
                c.Due = -25;
                c.SaveChangesToDatabase();
                col.Sched.RebuildDyn(did);
                col.Reset();
                c = col.Sched.PopCard();
                col.Sched.AnswerCard(c, Sched.AnswerEase.Again);
                col.Sched.EmptyDyn(did);
                c.LoadFromDatabase();
                Assert.AreEqual(100, c.Interval);
                Assert.AreEqual(-25, c.Due);

                //Fail with norma grad
                c = cardCopy;
                c.Due = -25;
                c.SaveChangesToDatabase();
                col.Sched.RebuildDyn(did);
                col.Reset();
                c = col.Sched.PopCard();
                col.Sched.AnswerCard(c, Sched.AnswerEase.Again);
                col.Sched.AnswerCard(c, Sched.AnswerEase.Good);
                col.Sched.EmptyDyn(did);
                c.LoadFromDatabase();
                Assert.AreEqual(100, c.Interval);
                Assert.AreEqual(-25, c.Due);
            }
        }


        [TestMethod]
        public async Task TestOrdCycle()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                //Add two more templates and set second active
                var m = col.Models.GetCurrent();
                var mm = col.Models;
                var t = mm.NewTemplate("Reverse");
                t["qfmt"] = JsonValue.CreateStringValue("{{Back}}");
                t["afmt"] = JsonValue.CreateStringValue("{{Front}}");
                mm.AddTemplate(m, t);
                t = mm.NewTemplate("f2");
                t["qfmt"] = JsonValue.CreateStringValue("{{Front}}");
                t["afmt"] = JsonValue.CreateStringValue("{{Back}}");
                mm.AddTemplate(m, t);
                mm.Save(m);

                //Disable bury sibling first
                col.Sched.BurySiblingsOnAnswer = false;

                //Create a new note; it should have 3 cards
                var f = col.NewNote();
                f.SetItem("Front", "1");
                f.SetItem("Back", "1");
                col.AddNote(f);
                Assert.AreEqual(3, col.CardCount());
                col.Reset();

                //Ordinals should arrive in order
                Assert.AreEqual(0, col.Sched.PopCard().Ord);
                Assert.AreEqual(1, col.Sched.PopCard().Ord);
                Assert.AreEqual(2, col.Sched.PopCard().Ord);
            }
        }

        [TestMethod]
        public async Task TestCountsIdx()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                //Create a new note; it should have 3 cards
                var f = col.NewNote();
                f.SetItem("Front", "1");
                f.SetItem("Back", "1");
                col.AddNote(f);
                col.Reset();

                var expected = new List<int>() { 1, 0, 0 };
                var actual = col.Sched.Counts();
                Assert.IsTrue(Utils.CompareLists(expected, actual.ToList()), actual.ToString());
                var c = col.Sched.PopCard();

                //Counter's been decremented but idx indicates 1
                expected = new List<int>() { 0, 0, 0 };
                actual = col.Sched.Counts();
                Assert.IsTrue(Utils.CompareLists(expected, actual.ToList()), actual.ToString());
                Assert.AreEqual(0, col.Sched.CountIdx(c));

                //Answer to move to learn queue
                col.Sched.AnswerCard(c, Sched.AnswerEase.Again);
                expected = new List<int>() { 0, 2, 0 };
                actual = col.Sched.Counts();
                Assert.IsTrue(Utils.CompareLists(expected, actual.ToList()), actual.ToString());

                //Fetching again will decrement the count
                c = col.Sched.PopCard();
                expected = new List<int>() { 0, 0, 0 };
                actual = col.Sched.Counts();
                Assert.IsTrue(Utils.CompareLists(expected, actual.ToList()), actual.ToString());
                Assert.AreEqual(1, col.Sched.CountIdx(c));

                //Answering should add it back again
                col.Sched.AnswerCard(c, Sched.AnswerEase.Again);
                expected = new List<int>() { 0, 2, 0 };
                actual = col.Sched.Counts();
                Assert.IsTrue(Utils.CompareLists(expected, actual.ToList()), actual.ToString());
            }
        }

        [TestMethod]
        public async Task TestRepCounts()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                //Create a new note; it should have 3 cards
                var f = col.NewNote();
                f.SetItem("Front", "one");
                col.AddNote(f);
                col.Reset();

                //lrnReps should be accurate on pass/fail
                var expected = new List<int>() { 1, 0, 0 };
                var actual = col.Sched.Counts();
                Assert.IsTrue(Utils.CompareLists(expected, actual.ToList()), actual.ToString());

                col.Sched.AnswerCard(col.Sched.PopCard(), Sched.AnswerEase.Again);
                expected = new List<int>() { 0, 2, 0 };
                actual = col.Sched.Counts();
                Assert.IsTrue(Utils.CompareLists(expected, actual.ToList()), actual.ToString());

                col.Sched.AnswerCard(col.Sched.PopCard(), Sched.AnswerEase.Again);
                expected = new List<int>() { 0, 2, 0 };
                actual = col.Sched.Counts();
                Assert.IsTrue(Utils.CompareLists(expected, actual.ToList()), actual.ToString());

                col.Sched.AnswerCard(col.Sched.PopCard(), Sched.AnswerEase.Hard);
                expected = new List<int>() { 0, 1, 0 };
                actual = col.Sched.Counts();
                Assert.IsTrue(Utils.CompareLists(expected, actual.ToList()), actual.ToString());

                col.Sched.AnswerCard(col.Sched.PopCard(), Sched.AnswerEase.Again);
                expected = new List<int>() { 0, 2, 0 };
                actual = col.Sched.Counts();
                Assert.IsTrue(Utils.CompareLists(expected, actual.ToList()), actual.ToString());

                col.Sched.AnswerCard(col.Sched.PopCard(), Sched.AnswerEase.Hard);
                expected = new List<int>() { 0, 1, 0 };
                actual = col.Sched.Counts();
                Assert.IsTrue(Utils.CompareLists(expected, actual.ToList()), actual.ToString());

                col.Sched.AnswerCard(col.Sched.PopCard(), Sched.AnswerEase.Hard);
                expected = new List<int>() { 0, 0, 0 };
                actual = col.Sched.Counts();
                Assert.IsTrue(Utils.CompareLists(expected, actual.ToList()), actual.ToString());

                //Initial pass should be correct too
                f = col.NewNote();
                f.SetItem("Front", "two");
                col.AddNote(f);
                col.Reset();
                
                col.Sched.AnswerCard(col.Sched.PopCard(), Sched.AnswerEase.Hard);
                expected = new List<int>() { 0, 1, 0 };
                actual = col.Sched.Counts();
                Assert.IsTrue(Utils.CompareLists(expected, actual.ToList()), actual.ToString());

                col.Sched.AnswerCard(col.Sched.PopCard(), Sched.AnswerEase.Again);
                expected = new List<int>() { 0, 2, 0 };
                actual = col.Sched.Counts();
                Assert.IsTrue(Utils.CompareLists(expected, actual.ToList()), actual.ToString());

                col.Sched.AnswerCard(col.Sched.PopCard(), Sched.AnswerEase.Good);
                expected = new List<int>() { 0, 0, 0 };
                actual = col.Sched.Counts();
                Assert.IsTrue(Utils.CompareLists(expected, actual.ToList()), actual.ToString());

                //Immediate graduate should work
                f = col.NewNote();
                f.SetItem("Front", "three");
                col.AddNote(f);
                col.Reset();

                col.Sched.AnswerCard(col.Sched.PopCard(), Sched.AnswerEase.Good);
                expected = new List<int>() { 0, 0, 0 };
                actual = col.Sched.Counts();
                Assert.IsTrue(Utils.CompareLists(expected, actual.ToList()), actual.ToString());

                //And failing a review should too
                var c = f.Cards()[0];
                c.Type = CardType.Review;
                c.Queue = 2;
                c.Due = col.Sched.Today;
                c.SaveChangesToDatabase();
                col.Reset();
                expected = new List<int>() { 0, 0, 1 };
                actual = col.Sched.Counts();
                Assert.IsTrue(Utils.CompareLists(expected, actual.ToList()), actual.ToString());

                col.Sched.AnswerCard(col.Sched.PopCard(), Sched.AnswerEase.Again);
                expected = new List<int>() { 0, 1, 0 };
                actual = col.Sched.Counts();
                Assert.IsTrue(Utils.CompareLists(expected, actual.ToList()), actual.ToString());
            }
        }

        [TestMethod]
        public async Task TestTime()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                Note f;
                Card c;
                for(int i = 0; i < 5; i++)
                {
                    f = col.NewNote();
                    f.SetItem("Front", "num " + i.ToString());
                    col.AddNote(f);
                    c = f.Cards()[0];
                    c.Type = CardType.Review;
                    c.Queue = 2;
                    c.Due = 0;
                    c.SaveChangesToDatabase();
                }

                //Fail the first one
                col.Reset();
                c = col.Sched.PopCard();

                //Set a a fail delay of 1 second so we don't have to wait
                JsonValue jv = JsonValue.CreateNumberValue(1.0 / 60.0);
                col.Sched.CardConf(c)
                    .GetNamedObject("lapse")
                    .GetNamedArray("delays")[0] = jv;
                col.Sched.AnswerCard(c, Sched.AnswerEase.Again);

                //The next card should be another review
                c = col.Sched.PopCard();
                Assert.AreEqual(2, c.Queue);

                //But if we wait for a second, the failed card should come back
                await Task.Delay(TimeSpan.FromSeconds(2));
                c = col.Sched.PopCard();
                Assert.AreEqual(1, c.Queue);
            }
        }

        [TestMethod]
        public async Task TestCollapse()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                var f = col.NewNote();
                f.SetItem("Front", "one");
                col.AddNote(f);
                col.Reset();

                //Test collapsing
                var c = col.Sched.PopCard();
                col.Sched.AnswerCard(c, Sched.AnswerEase.Again);
                c = col.Sched.PopCard();
                col.Sched.AnswerCard(c, Sched.AnswerEase.Good);
                Assert.IsNull(col.Sched.PopCard());
            }
        }

        [TestMethod]
        public async Task TestDeckDue()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                var f = col.NewNote();
                f.SetItem("Front", "one");
                col.AddNote(f);

                //And one that's a child
                f = col.NewNote();
                f.SetItem("Front", "two");
                var default1 = col.Deck.AddOrResuedDeck("Default::1");
                f.Model["did"] = JsonValue.CreateNumberValue((long)default1);
                col.AddNote(f);

                //Make it a review card
                var c = f.Cards()[0];
                c.Queue = 2;
                c.Due = 0;
                c.SaveChangesToDatabase();

                //Add one more with a new deck
                f = col.NewNote();
                f.SetItem("Front", "two");
                var foobar = col.Deck.AddOrResuedDeck("foo::bar");
                f.Model["did"] = JsonValue.CreateNumberValue((long)foobar);
                col.AddNote(f);

                //And one that's a sibling
                f = col.NewNote();
                f.SetItem("Front", "three");
                var foobaz = col.Deck.AddOrResuedDeck("foo::baz");
                f.Model["did"] = JsonValue.CreateNumberValue((long)foobaz);
                col.AddNote(f);
                col.Reset();

                Assert.AreEqual(5, col.Deck.DeckDict.Count);

                var cnts = col.Sched.DeckDueList();
                Assert.AreEqual("Default", cnts[0].Names[0]);
                Assert.AreEqual(1, cnts[0].DeckId);
                Assert.AreEqual(0, cnts[0].ReviewCount);
                Assert.AreEqual(0, cnts[0].LearnCount);
                Assert.AreEqual(1, cnts[0].NewCount);

                cnts = col.Sched.DeckDueList();
                Assert.AreEqual("Default::1", cnts[1].Names[0]);
                Assert.AreEqual((long)default1, cnts[1].DeckId);
                Assert.AreEqual(1, cnts[1].ReviewCount);
                Assert.AreEqual(0, cnts[1].LearnCount);
                Assert.AreEqual(0, cnts[1].NewCount);

                cnts = col.Sched.DeckDueList();
                Assert.AreEqual("foo", cnts[2].Names[0]);
                Assert.AreEqual((long)col.Deck.AddOrResuedDeck("foo"), cnts[2].DeckId);
                Assert.AreEqual(0, cnts[2].ReviewCount);
                Assert.AreEqual(0, cnts[2].LearnCount);
                Assert.AreEqual(0, cnts[2].NewCount);

                cnts = col.Sched.DeckDueList();
                Assert.AreEqual("foo::bar", cnts[3].Names[0]);
                Assert.AreEqual((long)foobar, cnts[3].DeckId);
                Assert.AreEqual(0, cnts[3].ReviewCount);
                Assert.AreEqual(0, cnts[3].LearnCount);
                Assert.AreEqual(1, cnts[3].NewCount);

                cnts = col.Sched.DeckDueList();
                Assert.AreEqual("foo::baz", cnts[4].Names[0]);
                Assert.AreEqual((long)foobaz, cnts[4].DeckId);
                Assert.AreEqual(0, cnts[4].ReviewCount);
                Assert.AreEqual(0, cnts[4].LearnCount);
                Assert.AreEqual(1, cnts[4].NewCount);

                var tree = col.Sched.DeckDueTree();
                Assert.AreEqual("Default", tree[0].Names[0]);

                //Sum of child and parent
                Assert.AreEqual(1, tree[0].DeckId);
                Assert.AreEqual(1, tree[0].ReviewCount);
                Assert.AreEqual(1, tree[0].NewCount);

                //Child count is just review
                Assert.AreEqual("1", tree[0].Children[0].Names[0]);
                Assert.AreEqual(default1, tree[0].Children[0].DeckId);
                Assert.AreEqual(1, tree[0].Children[0].ReviewCount);
                Assert.AreEqual(0, tree[0].Children[0].LearnCount);

                //Code should not fail if a card has an invalid deck
                c.OriginalDeckId = 12345;
                c.SaveChangesToDatabase();
                col.Sched.DeckDueList();
                col.Sched.DeckDueTree();
            }
        }

        [TestMethod]
        public async Task TestDeckTree()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                col.Deck.AddOrResuedDeck("new::b::c");
                col.Deck.AddOrResuedDeck("new2");

                //New should not appear twice in tree
                var names = (from s in col.Sched.DeckDueTree() select s.Names[0]).ToList();
                names.Remove("new");
                Assert.IsFalse(names.Contains("new"));
            }
        }

        [TestMethod]
        public async Task TestDeckFlow()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                var f = col.NewNote();
                f.SetItem("Front", "one");
                col.AddNote(f);

                //And one that's a child
                f = col.NewNote();
                f.SetItem("Front", "two");
                var default1 = col.Deck.AddOrResuedDeck("Default::2");
                f.Model["did"] = JsonValue.CreateNumberValue((long)default1);
                col.AddNote(f);

                //And another that's higher up
                f = col.NewNote();
                f.SetItem("Front", "three");
                default1 = col.Deck.AddOrResuedDeck("Default::1");
                f.Model["did"] = JsonValue.CreateNumberValue((long)default1);
                col.AddNote(f);

                //Should get top level one first, then ::1, then ::2
                col.Reset();
                var expected = new List<int>() { 3, 0, 0 };
                var actual = col.Sched.Counts();
                Assert.IsTrue(Utils.CompareLists(expected, actual.ToList()), actual.ToString());
                foreach(string s in new string[] {"one", "three", "two" })
                {
                    var c = col.Sched.PopCard();
                    Assert.AreEqual(s, c.LoadNote().GetItem("Front"));
                    col.Sched.AnswerCard(c, Sched.AnswerEase.Hard);
                }
            }
        }

        [TestMethod]
        public async Task TestReorder()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                //Add a note with default deck
                var f = col.NewNote();
                f.SetItem("Front", "one");
                col.AddNote(f);
                var f2 = col.NewNote();
                f2.SetItem("Front","two");
                col.AddNote(f2);
                Assert.AreEqual(2, f2.Cards()[0].Due);
                bool found = false;
                //50/50 chance of being reordered
                for(int i = 0; i < 20; i++)
                {
                    col.Sched.RandomizeCards(1);
                    if (f.Cards()[0].Due != f.Id)
                    {
                        found = true;
                        break;
                    }
                }
                Assert.IsTrue(found);
                col.Sched.OrderCards(1);
                Assert.AreEqual(1, f.Cards()[0].Due);

                //Shifting
                var f3 = col.NewNote();
                f3.SetItem("Front", "three");
                col.AddNote(f3);
                var f4 = col.NewNote();
                f4.SetItem("Front", "four");
                col.AddNote(f4);

                Assert.AreEqual(1, f.Cards()[0].Due);
                Assert.AreEqual(2, f2.Cards()[0].Due);
                Assert.AreEqual(3, f3.Cards()[0].Due);
                Assert.AreEqual(4, f4.Cards()[0].Due);

                long[] cid = new long[] { f3.Cards()[0].Id, f4.Cards()[0].Id };
                col.Sched.SortCards(cid, 1, shift: true);
                Assert.AreEqual(3, f.Cards()[0].Due);
                Assert.AreEqual(4, f2.Cards()[0].Due);
                Assert.AreEqual(1, f3.Cards()[0].Due);
                Assert.AreEqual(2, f4.Cards()[0].Due);
            }
        }

        [TestMethod]
        public async Task TestForget()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                var f = col.NewNote();
                f.SetItem("Front", "one");
                col.AddNote(f);
                var c = f.Cards()[0];
                c.Queue = 2;
                c.Type = CardType.Review;
                c.Interval = 100;
                c.Due = 0;
                c.SaveChangesToDatabase();
                col.Reset();
                List<int> expected = new List<int>() { 0, 0, 1 };
                var actual = col.Sched.Counts();
                Assert.IsTrue(Utils.CompareLists(expected, actual.ToList()), actual.ToString());

                col.Sched.ResetCards(new long[] { c.Id });
                col.Reset();
                expected = new List<int>() { 1, 0, 0 };
                actual = col.Sched.Counts();
                Assert.IsTrue(Utils.CompareLists(expected, actual.ToList()), actual.ToString());
            }
        }

        [TestMethod]
        public async Task TestResched()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                var f = col.NewNote();
                f.SetItem("Front", "one");
                col.AddNote(f);
                var c = f.Cards()[0];
                col.Sched.RescheduleIntoReviewCards(new long[] { c.Id }, 0, 0);
                c.LoadFromDatabase();
                Assert.AreEqual(col.Sched.Today, c.Due);
                Assert.AreEqual(1, c.Interval);
                Assert.AreEqual(2, c.Queue);
                Assert.AreEqual(CardType.Review, c.Type);
                col.Sched.RescheduleIntoReviewCards(new long[] { c.Id }, 1, 1);
                c.LoadFromDatabase();
                Assert.AreEqual(col.Sched.Today + 1, c.Due);
                Assert.AreEqual(1, c.Interval);
            }
        }

        [TestMethod]
        public async Task TestNoteLearn()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                var f = col.NewNote();
                f.SetItem("Front", "one");
                col.AddNote(f);
                var c = f.Cards()[0];
                c.Type = CardType.Review;
                c.Queue = 2;
                c.Due = 0;
                c.Factor = 2500;
                c.Reps = 3;
                c.Lapses = 1;
                c.Interval = 100;
                c.StartTimer();
                c.SaveChangesToDatabase();
                col.Reset();
                col.Sched.AnswerCard(c, Sched.AnswerEase.Again);
                JsonArray ja = new JsonArray();
                col.Sched.CardConf(c)["lapse"].GetObject()["delays"] = ja;
                col.Sched.AnswerCard(c, Sched.AnswerEase.Again);
            }
        }

        [TestMethod]
        public async Task TestFailMult()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                var f = col.NewNote();
                f.SetItem("Front", "one");
                f.SetItem("Back", "two");
                col.AddNote(f);
                var c = f.Cards()[0];
                c.Type = CardType.Review;
                c.Queue = 2;
                c.Interval = 100;
                c.Due = col.Sched.Today - c.Interval;
                c.Factor = 2500;
                c.Reps = 3;
                c.Lapses = 1;
                c.StartTimer();
                c.SaveChangesToDatabase();
                JsonValue ja = JsonValue.CreateNumberValue(0.5);
                col.Sched.CardConf(c)["lapse"].GetObject()["mult"] = ja;
                c = col.Sched.PopCard();
                col.Sched.AnswerCard(c, Sched.AnswerEase.Again);
                Assert.AreEqual(50, c.Interval);
                col.Sched.AnswerCard(c, Sched.AnswerEase.Again);
                Assert.AreEqual(25, c.Interval);
            }
        }

    }
}
