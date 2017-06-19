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
using AnkiU.AnkiCore.Sync;
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
    [TestClass, Obsolete]
    public class TestSync
    {
        public StorageFolder tempFolder;
        public StorageFolder tempFolder2;

        public Collection colClient;
        public Collection colServer;

        public Syncer client;
        public LocalServer server;

        [TestInitialize()]
        public async Task Setup()
        {
            tempFolder = await Utils.localFolder.CreateFolderAsync("tempFolder");
            tempFolder2 = await Utils.localFolder.CreateFolderAsync("tempFolder2");
        }

        [TestCleanup()]
        public async Task Clean()
        {
            if (colClient != null)
                colClient.Close();
            if (colServer != null)
                colServer.Close();

            if (tempFolder != null)
                await tempFolder.DeleteAsync();
            if (tempFolder2 != null)
                await tempFolder2.DeleteAsync();

            tempFolder = null;
            tempFolder2 = null;
        }

        private async Task SetupBasic()
        {
            colClient = await Utils.GetEmptyCollection(tempFolder);
            var f = colClient.NewNote();
            f.SetItem("Front", "foo");
            f.SetItem("Back", "bar");
            f.Tags.Add("foo");
            colClient.AddNote(f);
            colClient.Reset();
            colClient.Sched.AnswerCard(colClient.Sched.PopCard(), Sched.AnswerEase.Easy);

            colServer = await Utils.GetEmptyCollection(tempFolder2, true);
            f = colServer.NewNote();
            f.SetItem("Front", "bar");
            f.SetItem("Back", "bar");
            f.Tags.Add("bar");
            colServer.AddNote(f);
            colServer.Reset();
            colServer.Sched.AnswerCard(colServer.Sched.PopCard(), Sched.AnswerEase.Easy);
            
            //Start with same schema and sync time
            colClient.Scm = 0;
            colServer.Scm = 0;

            //And same modified time, so sync does nothing
            long t = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            colClient.Save(mod: t);
            colServer.Save(mod: t);
            server = new LocalServer(colServer);
            client = new Syncer(colClient, server);
        }

        public async Task SetupModified()
        {
            await SetupBasic();
            //Mark colClient as changed
            await Task.Delay(TimeSpan.FromSeconds(0.1));
            colClient.SetIsModified();
            colClient.Save();
        }

        [TestMethod]
        public async Task TestNochange()
        {
            await SetupBasic();
            string[] result = await client.Sync();
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("noChanges", result[0]);
        }

        [TestMethod]
        public async Task TestChangedSchema()
        {
            await SetupModified();

            colClient.Scm += 1;
            colClient.SetIsModified();
            string[] result = await client.Sync();
            Assert.AreEqual(1, result.Length);
            Assert.AreEqual("fullSync", result[0]);
        }

        private void Check(int num)
        {
            int actual;
            foreach(var col in new Collection[] {colClient, colServer })
            {
                foreach(var t in new string[] { "revlog", "notes", "cards" })
                {
                    actual = col.Database.QueryScalar<int>("select count() from " + t);
                    Assert.AreEqual(num, actual);
                }
                Assert.AreEqual(4*num, col.Models.All().Count);
                //The default deck and config have an id of 1, so always 1
                Assert.AreEqual(1, col.Deck.All().Count);
                Assert.AreEqual(5, col.Deck.DeckConf.Count);
                Assert.AreEqual(num, col.Tags.All().Count);
            }
        }

        public async Task TestSyncSuccess()
        {
            await SetupModified();

            Check(1);

            var origUsn = colClient.Usn;
            
            string[] result = await client.Sync();
            Assert.AreEqual("success", result[0]);

            //Last sync times and mod times should agree
            Assert.AreEqual(colClient.TimeModified, colServer.TimeModified);
            Assert.AreEqual(colClient.GetUsnForSync, colServer.GetUsnForSync);
            Assert.AreEqual(colClient.TimeModified, colClient.LastSync);
            Assert.AreNotEqual(origUsn, colClient.GetUsnForSync);

            //Because everything was created separately it will be merged in. in
            //actual use, we use a full sync to ensure a common starting point.
            Check(2);

            //repeating it does nothing
            result = await client.Sync();
            Assert.AreEqual("noChanges", result[0]);

            //If we bump mod time, the decks will sync but should remain the same.
            colClient.SetIsModified();
            colClient.Save();
            result = await client.Sync();
            Assert.AreEqual("success", result[0]);

            Check(2);

            //Crt should be synced
            colClient.Crt = 123;
            colClient.SetIsModified();
            result = await client.Sync();
            Assert.AreEqual("success", result[0]);
            Assert.AreEqual(colClient.Crt, colServer.Crt);
        }

        [TestMethod]
        public async Task TestModels()
        {
            await TestSyncSuccess();

            //Update model one
            var cm = colClient.Models.GetCurrent();
            cm["name"] = JsonValue.CreateStringValue("new");
            await Task.Delay(TimeSpan.FromSeconds(1));
            colClient.Models.Save(cm);
            colClient.Save();
            string str = colServer.Models.Get((long)JsonHelper.GetNameNumber(cm,"id")).GetNamedString("name");
            Assert.IsTrue(str.StartsWith("Basic"), "Not start with Basic");

            var result = await client.Sync();
            Assert.AreEqual("success", result[0]);
            str = colServer.Models.Get((long)JsonHelper.GetNameNumber(cm,"id")).GetNamedString("name");
            Assert.AreEqual("new", str);

            //Deleting triggers a full sync
            colClient.Scm = 0;
            colServer.Scm = 0;
            
            // In java ver exception is throw to ask outer code to handle user's choice
            // of whether to do a full sync or not.
            // Here we use event 
            colClient.ConfirmModSchemaEvent += () => { return true; };

            colClient.Models.Remove(cm);
            colClient.Save();
            result = await client.Sync();
            Assert.AreEqual("fullSync", result[0]);
        }

        [TestMethod]
        public async Task TestNotes()
        {
            await TestSyncSuccess();

            // Modifications should be synced
            var nid = colClient.Database.QueryScalar<long>("select id from notes");
            var note = colClient.GetNote(nid);
            Assert.AreNotEqual("abc", note.GetItem("Front"));
            note.SetItem("Front", "abc");
            note.SaveChangesToDatabase();
            colClient.Save();
            Assert.AreEqual("success", (await client.Sync())[0]);
            Assert.AreEqual("abc", colServer.GetNote(nid).GetItem("Front"));

            //Deletions too
            nid = colClient.Database.QueryScalar<long>("select id from notes where id = ?", nid);
            Assert.IsTrue(nid > 0);
            colClient.RemoveNotesAndCards(new long[] { nid});
            colClient.Save();
            Assert.AreEqual("success", (await client.Sync())[0]);
            nid = colClient.Database.QueryScalar<long>("select id from notes where id = ?", nid);
            Assert.AreEqual(0, nid);
            nid = colServer.Database.QueryScalar<long>("select id from notes where id = ?", nid);
            Assert.AreEqual(0, nid);
        }

        [TestMethod]
        public async Task TestCards()
        {
            await TestSyncSuccess();

            // Modifications should be synced
            var nid = colClient.Database.QueryScalar<long>("select id from notes");
            var note = colClient.GetNote(nid);
            var card = note.Cards()[0];
            //Answer the card locally
            card.StartTimer();
            colClient.Sched.AnswerCard(card, Sched.AnswerEase.Easy);
            Assert.AreEqual(2, card.Reps);
            colClient.Save();
            Assert.AreEqual(1, colServer.GetCard(card.Id).Reps);
            Assert.AreEqual("success", (await client.Sync())[0]);
            Assert.AreEqual(2, colServer.GetCard(card.Id).Reps);

            //If it's modified on both sides , later mod time should win
            List<List<Collection>> testList = new List<List<Collection>>();
            testList.Add(new List<Collection>());
            testList[0].Add(colClient);
            testList[0].Add(colServer);
            testList.Add(new List<Collection>());
            testList[1].Add(colServer);
            testList[1].Add(colClient);
            foreach(var test in testList)
            {
                await Task.Delay(TimeSpan.FromSeconds(1));
                var c = test[0].GetCard(card.Id);
                c.Reps = 5;
                c.SaveChangesToDatabase();
                test[0].Save();

                await Task.Delay(TimeSpan.FromSeconds(1));
                c = test[1].GetCard(card.Id);
                c.Reps = 3;
                c.SaveChangesToDatabase();
                test[1].Save();
                Assert.AreEqual("success", (await client.Sync())[0]);
                Assert.AreEqual(3, test[1].GetCard(card.Id).Reps);
                Assert.AreEqual(3, test[0].GetCard(card.Id).Reps);
            }

            //Removals should work too
            colClient.RemoveCardsAndNoteIfNoCardsLeft(new long[] { card.Id });
            colClient.Save();
            Assert.IsTrue(colServer.Database.QueryScalar<int>("select 1 from cards where id = ?", card.Id) > 0);
            Assert.AreEqual("success", (await client.Sync())[0]);
            Assert.AreEqual(0, colServer.Database.QueryScalar<int>("select 1 from cards where id = ?", card.Id));
        }

        [TestMethod]
        public async Task TestTags()
        {
            await TestSyncSuccess();

            Assert.IsTrue(colClient.Tags.All().SequenceEqual(colServer.Tags.All()));

            colClient.Tags.Register(new List<string> { "abc" });
            colServer.Tags.Register(new List<string> { "xyz" });
            Assert.IsFalse(colClient.Tags.All().SequenceEqual(colServer.Tags.All()));

            colClient.Save();
            await Task.Delay(TimeSpan.FromSeconds(0.1));
            colServer.Save();
            Assert.AreEqual("success", (await client.Sync())[0]);
            Assert.IsTrue(colClient.Tags.All().SequenceEqual(colServer.Tags.All()));
        }

        [TestMethod]
        public async Task TestDecks()
        {
            await TestSyncSuccess();

            Assert.AreEqual(1, colClient.Deck.All().Count);
            Assert.AreEqual(colClient.Deck.All().Count, colServer.Deck.All().Count);

            colClient.Deck.AddOrResuedDeck("new");
            Assert.AreNotEqual(colClient.Deck.All().Count, colServer.Deck.All().Count);

            await Task.Delay(TimeSpan.FromSeconds(0.1));
            colServer.Deck.AddOrResuedDeck("new2");
            colClient.Save();
            await Task.Delay(TimeSpan.FromSeconds(0.1));
            colServer.Save();

            Assert.AreEqual("success", (await client.Sync())[0]);
            Assert.IsTrue(colClient.Tags.All().SequenceEqual(colServer.Tags.All()));
            Assert.AreEqual(colClient.Deck.All().Count, colServer.Deck.All().Count);
            Assert.AreEqual(3, colClient.Deck.All().Count);

            Assert.AreEqual(60, JsonHelper.GetNameNumber(colClient.Deck.ConfForDeckId(1),"maxTaken"));
            colServer.Deck.ConfForDeckId(1)["maxTaken"] = JsonValue.CreateNumberValue(30);
            colServer.Deck.Save(colServer.Deck.ConfForDeckId(1));
            colServer.Save();
            Assert.AreEqual("success", (await client.Sync())[0]);
            Assert.AreEqual(30, JsonHelper.GetNameNumber(colClient.Deck.ConfForDeckId(1),"maxTaken"));
        }

        [TestMethod]
        public async Task TestConf()
        {
            await TestSyncSuccess();

            Assert.AreEqual(1, colServer.Conf["curDeck"].GetNumber());
            colClient.Conf["curDeck"] = JsonValue.CreateNumberValue(2);
            await Task.Delay(TimeSpan.FromSeconds(0.1));
            colClient.SetIsModified();
            colClient.Save();
            Assert.AreEqual("success", (await client.Sync())[0]);
            Assert.AreEqual(2, colServer.Conf["curDeck"].GetNumber());
        }

        [TestMethod]
        public async Task TestThreeway()
        {
            await TestSyncSuccess();

            colClient.Close(false);
            StorageFolder tempFolder3 = await Utils.localFolder.CreateFolderAsync("tempFolder3");
            foreach(var file in await tempFolder.GetFilesAsync())
            {
                await file.CopyAsync(tempFolder3);
            }
            colClient.ReOpen();
            Collection col3 = await Storage.OpenOrCreateCollection(tempFolder3, Utils.collectionName);
            var client2 = new Syncer(col3, server);
            Assert.AreEqual("noChanges", (await client2.Sync())[0]);

            //Client 1 adds a card at time 1
            await Task.Delay(TimeSpan.FromSeconds(1));
            var f = colClient.NewNote();
            f.SetItem("Front", "1");
            colClient.AddNote(f);
            colClient.Save();

            //At time 2, client 2 syncs to server
            await Task.Delay(TimeSpan.FromSeconds(1));
            col3.SetIsModified();
            col3.Save();
            Assert.AreEqual("success", (await client2.Sync())[0]);

            //At time 3, client 1 syncs, adding the older note
            await Task.Delay(TimeSpan.FromSeconds(1));
            Assert.AreEqual("success", (await client.Sync())[0]);
            Assert.AreEqual(colClient.NoteCount(), colServer.NoteCount());

            //Syncing client2 should pick it up
            Assert.AreEqual("success", (await client2.Sync())[0]);
            Assert.AreEqual(colClient.NoteCount(), colServer.NoteCount());
            Assert.AreEqual(colClient.NoteCount(), col3.NoteCount());

            col3.Close(false);
            await tempFolder3.DeleteAsync();
        }

        [TestMethod]
        public async Task TestThreeway2()
        {
            colClient = await Utils.GetEmptyCollection(tempFolder);
            var f = colClient.NewNote();
            f.SetItem("Front", "startingpoint");
            var nid = f.Id;
            colClient.AddNote(f);
            var cid = f.Cards()[0].Id;
            colClient.BeforeUpload();

            StorageFolder tempFolder3 = await Utils.localFolder.CreateFolderAsync("tempFolder3");

            //Start both clients and server off in this state
            foreach (var files in await tempFolder.GetFilesAsync())
            {
                await files.CopyAsync(tempFolder2);
                await files.CopyAsync(tempFolder3);
            }
            colClient = await Storage.OpenOrCreateCollection(tempFolder, Utils.collectionName);
            Collection colClient2 = await Storage.OpenOrCreateCollection(tempFolder3, Utils.collectionName);
            colServer = await Storage.OpenOrCreateCollection(tempFolder2, Utils.collectionName, server: true);

            //Modify colClient then sync colClient->colServer
            var n = colClient.GetNote(nid);
            var t = "firstmod";
            n.SetItem("Front", t);
            n.SaveChangesToDatabase();
            colClient.Database.Execute("update cards set mod = 1, usn = -1");
            var server = new LocalServer(colServer);
            var client1 = new Syncer(colClient, server);
            await client1.Sync();
            n.LoadFromDatabase();
            Assert.AreEqual(t, n.GetItem("Front"));
            Assert.AreEqual(t, colServer.GetNote(nid).GetItem("Front"));
            Assert.AreEqual(1, colServer.Database.QueryScalar<long>("select mod from cards"));

            //Sync sever to colClient2
            var client2 = new Syncer(colClient2, server);
            await client2.Sync();
            Assert.AreEqual(t, colClient2.GetNote(nid).GetItem("Front"));
            Assert.AreEqual(1, colClient2.Database.QueryScalar<long>("select mod from cards"));

            //Modify colClient1 and sync
            await Task.Delay(TimeSpan.FromSeconds(1));
            t = "secondmod";
            n = colClient.GetNote(nid);
            n.SetItem("Front", t);
            n.SaveChangesToDatabase();
            colClient.Database.Execute("update cards set mod=2, usn=-1");
            await client1.Sync();

            //Modify colClient2 and sync - both colClient2 and server should be the same
            await Task.Delay(TimeSpan.FromSeconds(1));
            var t2 = "thirdmod";
            n = colClient2.GetNote(nid);
            n.SetItem("Front", t2);
            n.SaveChangesToDatabase();
            colClient2.Database.Execute("update cards set mod=3, usn=-1");
            await client2.Sync();
            n.LoadFromDatabase();

            Assert.AreEqual(t2, n.GetItem("Front"));
            Assert.AreEqual(3, colClient2.Database.QueryScalar<long>("select mod from cards"));
            n = colServer.GetNote(nid);
            Assert.AreEqual(t2, n.GetItem("Front"));
            Assert.AreEqual(3, colClient2.Database.QueryScalar<long>("select mod from cards"));

            //And syncing c1 again should yield the updated note as well
            await client1.Sync();
            n = colServer.GetNote(nid);
            Assert.AreEqual(t2, n.GetItem("Front"));
            Assert.AreEqual(3, colClient2.Database.QueryScalar<long>("select mod from cards"));
            
            n = colClient.GetNote(nid);
            Assert.AreEqual(t2, n.GetItem("Front"));
            Assert.AreEqual(3, colClient2.Database.QueryScalar<long>("select mod from cards"));

            colClient2.Close(false);
            await tempFolder3.DeleteAsync();
        }

        [TestMethod]
        public async Task TestFilteredDelete()
        {
            await TestSyncSuccess();

            var nid = colClient.Database.QueryScalar<long>("select id from notes");
            var note = colClient.GetNote(nid);
            var card = note.Cards()[0];
            card.Type = CardType.Review;
            card.Interval = 10;
            card.Factor = 2500;
            card.Due = colClient.Sched.Today;
            card.SaveChangesToDatabase();

            //Put cards into a filtered deck
            var did = colClient.Deck.NewDynamicDeck("dyn");
            colClient.Sched.RebuildDyn(did);

            //Sync the filtered deck
            Assert.AreEqual("success", (await client.Sync())[0]);

            //Asnwer the card locally
            await Task.Delay(TimeSpan.FromSeconds(1));
            card.LoadFromDatabase();
            card.StartTimer();
            colClient.Sched.AnswerCard(card, Sched.AnswerEase.Easy);
            Assert.IsTrue(card.Interval > 10);

            //Delete the filtered deck
            colClient.Deck.Remove(did);

            //Sync again
            Assert.AreEqual("success", (await client.Sync())[0]);
            card.LoadFromDatabase();
            Assert.IsTrue(card.Interval > 10);
        }

    }
}
