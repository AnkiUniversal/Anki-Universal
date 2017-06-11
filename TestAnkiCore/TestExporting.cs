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
using AnkiU.AnkiCore.Exporter;
using AnkiU.AnkiCore.Importer;
using AnkiU;

namespace TestAnkiCore
{
    [TestClass]
    public class TestExporting
    {
        public StorageFolder tempFolder;
        public StorageFolder tempExport;
        public Collection sourceCollection;
        [TestInitialize()]
        public async Task Setup()
        {
            tempFolder = await Utils.localFolder.CreateFolderAsync("tempFolder");
            tempExport = await Utils.localFolder.CreateFolderAsync("tempExport");

            sourceCollection = await Utils.GetEmptyCollection(tempFolder);
            var f = sourceCollection.NewNote();
            f.SetItem("Front", "foo");
            f.SetItem("Back", "bar");
            f.Tags.Add("tag");
            f.Tags.Add("tag2");
            sourceCollection.AddNote(f);

            //With a different deck;
            f = sourceCollection.NewNote();
            f.SetItem("Front", "baz");
            f.SetItem("Back", "qux");
            long did = (long)sourceCollection.Deck.AddOrResuedDeck("new deck");
            f.Model["did"] = JsonValue.CreateNumberValue(did);
            sourceCollection.AddNote(f);
        }

        [TestCleanup()]
        public async Task Clean()
        {
            if (sourceCollection != null)
                sourceCollection.Close();

            if (tempFolder != null)
                await tempFolder.DeleteAsync();

            if (tempExport != null)
                await tempExport.DeleteAsync();

            tempFolder = null;
            tempExport = null;
        }

        [TestMethod]
        public async Task TestExportAnki()
        {
            long did = (long)sourceCollection.Deck.AddOrResuedDeck("test");
            var dobj = sourceCollection.Deck.Get(did);
            var confId = sourceCollection.Deck.CreateNewConfiguration("newConf");
            var conf = sourceCollection.Deck.GetConf(confId);
            conf.GetNamedObject("new")["perDay"] = JsonValue.CreateNumberValue(5);
            sourceCollection.Deck.Save(conf);
            sourceCollection.Deck.SetConf(dobj, confId);

            //Export
            AnkiExporter export = new AnkiExporter(sourceCollection);
            await export.ExportInto(tempExport, "ankitest.anki2");

            //Exporting should not have changed conf for original deck
            conf = sourceCollection.Deck.ConfForDeckId(did);
            Assert.AreEqual(1, conf.GetNamedNumber("id"));

            //Connect to new deck
            var col2 = await Storage.OpenOrCreateCollection(tempExport, "ankitest.anki2");
            Assert.AreEqual(2, col2.CardCount());

            //As scheduling was reset, should also revert decks to default conf
            long? newDid = col2.Deck.AddOrResuedDeck("test", false);
            Assert.IsNotNull(newDid);
            var conf2 = col2.Deck.ConfForDeckId(did);
            Assert.AreEqual(20, conf2.GetNamedObject("new").GetNamedNumber("perDay"));

            //Conf should be 1
            dobj = col2.Deck.Get(did);
            Assert.AreEqual(1, dobj.GetNamedNumber("conf"));


            //Try again, limited to a deck
            col2.Close();
            await tempExport.DeleteAsync();
            tempExport = await Utils.localFolder.CreateFolderAsync("tempExport");
            export = new AnkiExporter(sourceCollection, 1);
            await export.ExportInto(tempExport, "ankitest.anki2");

            col2 = await Storage.OpenOrCreateCollection(tempExport, "ankitest.anki2");
            Assert.AreEqual(1, col2.CardCount());

            col2.Close();
        }

        [TestMethod]
        public async Task TestExportAnkiDue()
        {
            sourceCollection.Crt -= 86400 * 10;
            sourceCollection.Sched.Reset();
            var c = sourceCollection.Sched.PopCard();
            sourceCollection.Sched.AnswerCard(c, Sched.AnswerEase.Hard);
            sourceCollection.Sched.AnswerCard(c, Sched.AnswerEase.Hard);

            //Should have ivl of 1, due on day 11
            Assert.AreEqual(1, c.Interval);
            Assert.AreEqual(11, c.Due);
            Assert.AreEqual(10, sourceCollection.Sched.Today);

            //Export
            var export = new AnkiExporter(sourceCollection);
            export.IncludeSched = true;
            await export.ExportInto(tempExport, "ankitest.anki2");

            //Importing into a new col, the due date should be equivalent
            var tempfolder2 = await Utils.localFolder.CreateFolderAsync("tempfolder2");
            using (Collection col2 = await Utils.GetEmptyCollection(tempfolder2))
            {
                var imp = new Anki2Importer(col2, tempExport, "ankitest.anki2");
                await imp.Run();
                c = col2.GetCard(c.Id);
                col2.Sched.Reset();
                Assert.AreEqual(1, c.Due - col2.Sched.Today);
            }
            await tempfolder2.DeleteAsync();
        }


        [TestMethod, Ignore]
        public async Task TestExportAnkiPkgSimpleCollection()
        {
            string fileName = "今日.mp3";
            string path = sourceCollection.Media.MediaFolder.Path + "\\" + fileName;
            //Add a test file to the media folder
            using (FileStream file = new FileStream(path, FileMode.Create))
            {
                byte[] buf = Encoding.UTF8.GetBytes("test");
                file.Write(buf, 0, buf.Length);
            }

            var n = sourceCollection.NewNote();
            n.SetItem("Front", "[sound:今日.mp3]");
            sourceCollection.AddNote(n);
            var export = new AnkiPackageExporter(sourceCollection);
            await export.ExportInto(tempExport, "ankitest.apkg");

            var tempfolder2 = await Utils.localFolder.CreateFolderAsync("tempfolder2");
            using (Collection col2 = await Utils.GetEmptyCollection(tempfolder2))
            {
                var fileToImport = await tempExport.GetFileAsync("ankitest.apkg");
                var imp = new AnkiPackageImporter(col2, fileToImport);
                await imp.Run();
                Assert.AreEqual(3, col2.CardCount());
                StorageFile mediaFile = await col2.Media.MediaFolder.TryGetItemAsync(fileName) as StorageFile;
                Assert.IsNotNull(mediaFile);
                Assert.AreEqual(fileName, mediaFile.Name);
            }
        }

        /// <summary>
        /// Stress test. No longer needed
        /// </summary>
        /// <returns></returns>
        [TestMethod, Ignore]
        public async Task TestExportAnkiPkgFullCollection()
        {
            //Copy apkg file to test folder 
            string assetFileName = @"ms-appx:///TestAssets/collection.apkg";
            StorageFile assetFile = await StorageFile.GetFileFromApplicationUriAsync(
                                        new Uri(assetFileName));
            await assetFile.CopyAsync(Utils.localFolder, "collection.apkg");

            //Empty cards and notes
            var listNids = sourceCollection.Database.QueryColumn<NoteTable>("select id from notes");
            var nids = (from s in listNids select s.Id).ToArray();
            sourceCollection.RemoveNotesAndCards(nids);
            Assert.AreEqual(0, sourceCollection.CardCount());

            //First import all cards and notes
            var fileToImport = await tempExport.GetFileAsync("collection.apkg");
            var import = new AnkiPackageImporter(sourceCollection, fileToImport);
            await import.Run();
            import.Close();
            //Get number of cards and mdeia files
            int numberOfCardBefore = sourceCollection.CardCount();
            int numberOfMediaFileBefore = (await sourceCollection.Media.MediaFolder.GetFilesAsync()).Count();

            //Export it
            var export = new AnkiPackageExporter(sourceCollection);
            await export.ExportInto(tempExport, "collection.apkg");

            //Reimport it again
            var tempfolder2 = await Utils.localFolder.CreateFolderAsync("tempfolder2");
            using (Collection col2 = await Utils.GetEmptyCollection(tempfolder2))
            {
                import = new AnkiPackageImporter(col2, fileToImport);
                await import.Run();
                import.Close();
                //Get number of cards and mdeia files
                int numberOfCardAfter = col2.CardCount();
                int numberOfMediaFileAfter = (await col2.Media.MediaFolder.GetFilesAsync()).Count();

                Assert.AreEqual(numberOfCardBefore, numberOfCardAfter);
                Assert.AreEqual(numberOfMediaFileBefore, numberOfMediaFileAfter);
            }
            await tempfolder2.DeleteAsync();

        }

    }
}
