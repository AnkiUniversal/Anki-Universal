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
using AnkiU.AnkiCore.Importer;
using AnkiU;

namespace TestAnkiCore
{
    //These tests are now obsolete 
    [TestClass, Ignore]
    public class TestImporting
    {
        public StorageFolder tempFolder;
        private string defaultDeckId = "1";

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
        public async Task TestAnki2MediaDupes()
        {
            long mid;
            string exportFileName = "testAnki2MediaDupe.apkg";
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                //Add a note
                Note n = col.NewNote();
                n.SetItem("Front", "[sound:foo.mp3]");
                mid = (long)n.Model.GetNamedNumber("id");
                col.AddNote(n);                
                var folder = await col.Media.MediaFolder.CreateFolderAsync(defaultDeckId);
                //Add that sound to the media folder
                using (FileStream file = new FileStream(folder.Path + "\\" + "foo.mp3", FileMode.Create))
                {
                    byte[] buf = Encoding.UTF8.GetBytes("foo");
                    file.Write(buf, 0, buf.Length);
                }
                AnkiU.AnkiCore.Exporter.AnkiPackageExporter exporter = new AnkiU.AnkiCore.Exporter.AnkiPackageExporter(col);
                await exporter.ExportInto(tempFolder, exportFileName);
            }

            //It should be imported correctly into an empty deck
            StorageFile exportedFiles = await tempFolder.GetFileAsync(exportFileName);
            StorageFolder tempFolder2 = await Utils.localFolder.CreateFolderAsync("tempFolder2");
            using (Collection empty = await Utils.GetEmptyCollection(tempFolder2))
            {
                Importer imp = new AnkiPackageImporter(empty, exportedFiles);
                await imp.Run();
                
                var expected = new List<string> { "foo.mp3" };
                var folder = await empty.Media.MediaFolder.GetFolderAsync(defaultDeckId);
                var storageFiles = await folder.GetFilesAsync();
                var actual = (from s in storageFiles select s.Name).ToList();
                Assert.IsTrue(actual.SequenceEqual(expected), actual.PrintArray());

                //And importing again will not duplicate, as the file content matches
                var cardList = empty.Database.QueryColumn<CardIdOnlyTable>("select id from cards");
                empty.RemoveCardsAndNoteIfNoCardsLeft((from c in cardList select c.Id).ToArray());
                imp = new Anki2Importer(empty, tempFolder, "test.anki2");
                await imp.Run();
                storageFiles = await folder.GetFilesAsync();
                actual = (from s in storageFiles select s.Name).ToList();
                Assert.IsTrue(actual.SequenceEqual(expected), actual.PrintArray());

                var n = empty.GetNote(empty.Database.QueryScalar<long>("select id from notes"));
                Assert.IsTrue(n.Fields[0].Contains("foo.mp3"), n.Fields[0]);

                //If the local file content is different, an import should trigger a rename
                cardList = empty.Database.QueryColumn<CardIdOnlyTable>("select id from cards");
                empty.RemoveCardsAndNoteIfNoCardsLeft((from c in cardList select c.Id).ToArray());

                using (FileStream file = new FileStream(empty.Media.MediaFolder.Path + "\\" + "foo.mp3", FileMode.Create))
                {
                    byte[] buf = Encoding.UTF8.GetBytes("bar");
                    file.Write(buf, 0, buf.Length);
                }
                imp = new Anki2Importer(empty, tempFolder, "test.anki2");
                await imp.Run();
                storageFiles = await empty.Media.MediaFolder.GetFilesAsync();
                actual = (from s in storageFiles select s.Name).ToList();
                expected = new List<string> { "foo.mp3", String.Format("foo_{0}.mp3", mid) };
                Assert.IsTrue(actual.SequenceEqual(expected), actual.PrintArray());

                n = empty.GetNote(empty.Database.QueryScalar<long>("select id from notes"));
                Assert.IsTrue(n.Fields[0].Contains("_"), n.Fields[0]);

                //If the localized media file already exists, we rewrite the note and media
                cardList = empty.Database.QueryColumn<CardIdOnlyTable>("select id from cards");
                empty.RemoveCardsAndNoteIfNoCardsLeft((from c in cardList select c.Id).ToArray());

                using (FileStream file = new FileStream(empty.Media.MediaFolder.Path + "\\" + "foo.mp3", FileMode.Open))
                {
                    byte[] buf = Encoding.UTF8.GetBytes("bar");
                    file.Write(buf, 0, buf.Length);
                }
                imp = new Anki2Importer(empty, tempFolder, "test.anki2");
                await imp.Run();
                storageFiles = await empty.Media.MediaFolder.GetFilesAsync();
                actual = (from s in storageFiles select s.Name).ToList();
                expected = new List<string> { "foo.mp3", String.Format("foo_{0}.mp3", mid) };
                Assert.IsTrue(actual.SequenceEqual(expected), actual.PrintArray());

                n = empty.GetNote(empty.Database.QueryScalar<long>("select id from notes"));
                Assert.IsTrue(n.Fields[0].Contains("_"), n.Fields[0]);
            }

            await tempFolder2.DeleteAsync();

        }

        [TestMethod]
        public async Task TestApkg()
        {
            //Copy apkg file to test folder 
            string assetFileName = @"ms-appx:///TestAssets/media.apkg";
            StorageFile assetFile = await StorageFile.GetFileFromApplicationUriAsync(
                                        new Uri(assetFileName));
            await assetFile.CopyAsync(Utils.localFolder, "media.apkg");

            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                var items = await col.Media.MediaFolder.GetItemsAsync();
                Assert.AreEqual(0, items.Count);

                StorageFile fileToImport = await Utils.localFolder.GetFileAsync("media.apkg");
                Importer import = new AnkiPackageImporter(col, fileToImport);
                await import.Run();

                items = await col.Media.MediaFolder.GetItemsAsync();
                Assert.AreEqual(1, items.Count);
                StorageFile file = items[0] as StorageFile;
                Assert.AreEqual("foo.wav", file.Name);

                //Importing again should be idempotent in terms of media
                var listCard = col.Database.QueryColumn<CardIdOnlyTable>("select id from cards");
                col.RemoveCardsAndNoteIfNoCardsLeft((from s in listCard select s.Id).ToArray());
                import = new AnkiPackageImporter(col, fileToImport);
                await import.Run();
                items = await col.Media.MediaFolder.GetItemsAsync();
                Assert.AreEqual(1, items.Count);
                file = items[0] as StorageFile;
                Assert.AreEqual("foo.wav", file.Name);

                //But if the local file has different data, it will rename
                listCard = col.Database.QueryColumn<CardIdOnlyTable>("select id from cards");

                col.RemoveCardsAndNoteIfNoCardsLeft((from s in listCard select s.Id).ToArray());

                using (FileStream stream = new FileStream(col.Media.MediaFolder.Path + "\\" + "foo.wav", FileMode.Create))
                {
                    byte[] buf = Encoding.UTF8.GetBytes("xyz");
                    stream.Write(buf, 0, buf.Length);
                }
                await import.Run();
                items = await col.Media.MediaFolder.GetItemsAsync();
                Assert.AreEqual(2, items.Count);

                import.Close();
            }

        }

        [TestMethod]
        public async Task TestAnki2Diffmodels()
        {
            //Copy apkg file to test folder 
            string assetFileName = @"ms-appx:///TestAssets/diffmodels2-1.apkg";
            StorageFile assetFile = await StorageFile.GetFileFromApplicationUriAsync(
                                        new Uri(assetFileName));
            await assetFile.CopyAsync(Utils.localFolder, "diffmodels2-1.apkg");

            assetFileName = @"ms-appx:///TestAssets/diffmodels2-2.apkg";
            assetFile = await StorageFile.GetFileFromApplicationUriAsync(
                                        new Uri(assetFileName));
            await assetFile.CopyAsync(Utils.localFolder, "diffmodels2-2.apkg");

            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                StorageFile fileToImport = await Utils.localFolder.GetFileAsync("diffmodels2-1.apkg");
                var imp = new AnkiPackageImporter(col, fileToImport);
                imp.SetDupeOnSchemaChange(true);
                await imp.Run();
                int before = col.NoteCount();
                Assert.AreEqual(1, before);
                Assert.AreEqual(1, col.CardCount());

                //Repeating the process should do nothing
                imp = new AnkiPackageImporter(col, fileToImport);
                imp.SetDupeOnSchemaChange(true);
                await imp.Run();
                Assert.AreEqual(before, col.NoteCount());
                Assert.AreEqual(1, col.CardCount());

                //Then the 2 card version
                fileToImport = await Utils.localFolder.GetFileAsync("diffmodels2-2.apkg");
                imp = new AnkiPackageImporter(col, fileToImport);
                imp.SetDupeOnSchemaChange(true);
                await imp.Run();
                int after = col.NoteCount();

                //As the model schemas differ, should have been imported as new model
                Assert.AreEqual(before + 1, after);

                //And the new model should have both cards
                Assert.AreEqual(3, col.CardCount());

                //Repeating the process should do nothing
                imp = new AnkiPackageImporter(col, fileToImport);
                imp.SetDupeOnSchemaChange(true);
                await imp.Run();
                after = col.NoteCount();
                Assert.AreEqual(before + 1, after);
                Assert.AreEqual(3, col.CardCount());

                imp.Close();
            }
        }

        [TestMethod]
        public async Task TestAnki2DiffModelTemplates()
        {
            //Different from the above as this one tests only the template text being
            //changed, not the number of cards/fields
            
            //Copy apkg file to test folder 
            string assetFileName = @"ms-appx:///TestAssets/diffmodeltemplates-1.apkg";
            StorageFile assetFile = await StorageFile.GetFileFromApplicationUriAsync(
                                        new Uri(assetFileName));
            await assetFile.CopyAsync(Utils.localFolder, "diffmodeltemplates-1.apkg");

            assetFileName = @"ms-appx:///TestAssets/diffmodeltemplates-2.apkg";
            assetFile = await StorageFile.GetFileFromApplicationUriAsync(
                                        new Uri(assetFileName));
            await assetFile.CopyAsync(Utils.localFolder, "diffmodeltemplates-2.apkg");

            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                StorageFile fileToImport = await Utils.localFolder.GetFileAsync("diffmodeltemplates-1.apkg");
                var imp = new AnkiPackageImporter(col, fileToImport);
                imp.SetDupeOnSchemaChange(true);
                await imp.Run();

                //The the version with updated template
                fileToImport = await Utils.localFolder.GetFileAsync("diffmodeltemplates-2.apkg");
                imp = new AnkiPackageImporter(col, fileToImport);
                imp.SetDupeOnSchemaChange(true);
                await imp.Run();

                //Collection should contain the note we imported
                Assert.AreEqual(1, col.NoteCount());

                //The front template should contain the text added in the 2nd package
                long tcid = col.FindCards("")[0];
                Note tnote = col.GetCard(tcid).LoadNote();
                StringAssert.Contains(col.FindTemplates(tnote)[0].GetNamedString("qfmt"), "Changed Front Template");
            }
        }

        [TestMethod]
        public async Task TestAnki2Updates()
        {

            //Copy apkg file to test folder 
            string assetFileName = @"ms-appx:///TestAssets/update1.apkg";
            StorageFile assetFile = await StorageFile.GetFileFromApplicationUriAsync(
                                        new Uri(assetFileName));
            await assetFile.CopyAsync(Utils.localFolder, "update1.apkg");

            assetFileName = @"ms-appx:///TestAssets/update2.apkg";
            assetFile = await StorageFile.GetFileFromApplicationUriAsync(
                                        new Uri(assetFileName));
            await assetFile.CopyAsync(Utils.localFolder, "update2.apkg");

            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                StorageFile fileToImport = await Utils.localFolder.GetFileAsync("update1.apkg");
                var imp = new AnkiPackageImporter(col, fileToImport);
                await imp.Run();

                Assert.AreEqual(0, imp.Dupes);
                Assert.AreEqual(1, imp.Added);
                Assert.AreEqual(0, imp.Updated);

                //Importing again should be idempotent
                imp = new AnkiPackageImporter(col, fileToImport);
                await imp.Run();

                Assert.AreEqual(1, imp.Dupes);
                Assert.AreEqual(0, imp.Added);
                Assert.AreEqual(0, imp.Updated);

                //Importing a newer note should update
                Assert.AreEqual(1, col.NoteCount());
                Assert.IsTrue(col.Database.QueryScalar<string>("select flds from notes").StartsWith("hello"), "Not start with: hello");

                fileToImport = await Utils.localFolder.GetFileAsync("update2.apkg");
                imp = new AnkiPackageImporter(col, fileToImport);
                await imp.Run();

                Assert.AreEqual(1, imp.Dupes);
                Assert.AreEqual(0, imp.Added);
                Assert.AreEqual(1, imp.Updated);
                Assert.IsTrue(col.Database.QueryScalar<string>("select flds from notes").StartsWith("goodbye"), "Not start with: goodbye");
            }
        }

    }
}
