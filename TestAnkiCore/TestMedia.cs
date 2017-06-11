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

namespace TestAnkiCore
{
    [TestClass]
    public class TestMedia
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
        public async Task TestAdd()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                string fileName = @"foo.jpg";
                string pathToFile = CreateFileInTempFolder(fileName);
                // new file, should preserve name
                StorageFile storagefile = await tempFolder.GetFileAsync(fileName);
                string r = await col.Media.AddFile(storagefile);
                Assert.AreEqual(r, fileName);

                r = await col.Media.AddFile(storagefile);
                Assert.AreEqual("foo (1).jpg", r);
            }
        }

        private string CreateFileInTempFolder(string fileName)
        {
            string pathToFile = tempFolder.Path + @"\" + fileName;
            using (FileStream file = new FileStream(pathToFile, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                byte[] data = Encoding.UTF8.GetBytes("hello");
                file.Write(data, 0, data.Length);
            }
            return pathToFile;
        }

        private void CreateFile(string absolutePath, string text)
        {
            using (FileStream file = new FileStream(absolutePath, FileMode.Create, FileAccess.Write, FileShare.Read))
            {
                byte[] data = Encoding.UTF8.GetBytes(text);
                file.Write(data, 0, data.Length);
            }
        }

        [TestMethod]
        public async Task TestFilesInStr()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                string[] fileName = new string[] { "foo.jpg", "bar.jpg" };

                var strMid = col.Models.ThisModels.Keys.ToArray()[0];
                long mid = long.Parse(strMid);
                List<string> actual;

                actual = col.Media.FileNameInMediaFolder(mid, "aoeu");
                Assert.AreEqual(0, actual.Count);

                actual = col.Media.FileNameInMediaFolder(mid, "aoeu<img src='foo.jpg'>ao");
                Assert.AreEqual(actual.Count, 1);
                Assert.AreEqual(actual[0], fileName[0]);

                actual = col.Media.FileNameInMediaFolder(mid, "aoeu<img src='foo.jpg' style='test'>ao");
                Assert.AreEqual(actual.Count, 1);
                Assert.AreEqual(actual[0], fileName[0]);

                actual = col.Media.FileNameInMediaFolder(mid, "aoeu<img src=\"foo.jpg\">ao");
                Assert.AreEqual(actual.Count, 1);
                Assert.AreEqual(actual[0], fileName[0]);

                actual = col.Media.FileNameInMediaFolder(mid, "aoeu<img src=foo.jpg style=bar>ao");
                Assert.AreEqual(actual.Count, 1);
                Assert.AreEqual(actual[0], fileName[0]);

                actual = col.Media.FileNameInMediaFolder(mid, "aoeu<img src='foo.jpg'><img src=\"bar.jpg\">ao");
                Assert.AreEqual(actual.Count, 2);
                for (int i = 0; i < actual.Count; i++)
                    Assert.AreEqual(actual[i], fileName[i]);

                fileName = new string[] { "one", "two" };
                actual = col.Media.FileNameInMediaFolder(mid, "<img src=one><img src=two>");
                Assert.AreEqual(actual.Count, 2);
                for (int i = 0; i < actual.Count; i++)
                    Assert.AreEqual(actual[i], fileName[i]);

                fileName = new string[] { "foo.jpg", "fo" };
                actual = col.Media.FileNameInMediaFolder(mid, "aoeu<img src=\"foo.jpg\"><img class=yo src=fo>ao");
                Assert.AreEqual(actual.Count, 2);
                for (int i = 0; i < actual.Count; i++)
                    Assert.AreEqual(actual[i], fileName[i]);

                actual = col.Media.FileNameInMediaFolder(mid, "aou[sound:foo.mp3]aou");
                Assert.AreEqual(actual.Count, 1);
                Assert.AreEqual(actual[0], "foo.mp3");
            }
        }

        [TestMethod]
        public async Task TestStrip()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                string str = col.Media.Strip("aoeu");
                Assert.AreEqual("aoeu", str);

                str = col.Media.Strip("aoeu[sound:foo.mp3]aoeu");
                Assert.AreEqual("aoeuaoeu", str);

                str = col.Media.Strip("aoeu[sound:foo.mp3]aoeu");
                Assert.AreEqual("aoeuaoeu", str);
            }
        }

        [TestMethod]
        public async Task TestEscapeImages()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                string esp = col.Media.EscapeImages("aoeu");
                Assert.AreEqual("aoeu", esp);

                esp = col.Media.EscapeImages("<img src='http://foo.com'>");
                Assert.AreEqual("<img src='http://foo.com'>", esp);

                esp = col.Media.EscapeImages("<img src=\"foo bar.jpg\">");
                Assert.AreEqual("<img src=\"foo%20bar.jpg\">", esp);
            }
        }

        [TestMethod]
        public async Task TestDeckIntegration()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                string fileName = "fake.png";
                string pathToFile = CreateFileInTempFolder(fileName);
                StorageFile storagefile = await tempFolder.GetFileAsync(fileName);
                string r = await col.Media.AddFile(storagefile, 1);

                //Add a note which reference the media file
                Note f = col.NewNote();
                f.SetItem("Front", "one");
                f.SetItem("Back", "<img src='fake.png'>");
                col.AddNote(f);

                //and one which references a non-esixtent file
                f = col.NewNote();
                f.SetItem("Front", "one");
                f.SetItem("Back", "<img src='fake2.png'>");
                col.AddNote(f);

                //add another file which isn't used
                using (FileStream file = new FileStream(col.Media.MediaFolder.Path + "\\1\\" + "foo.jpg", 
                                                        FileMode.Create, FileAccess.Write, FileShare.Read))
                {
                    byte[] data = Encoding.UTF8.GetBytes("test");
                    file.Write(data, 0, data.Length);
                }

                //check media
                Media.CheckResults ret = await col.Media.CheckMissingAndUnusedFiles();
                Assert.AreEqual(1, ret.UnusedFiles.Count);
                Assert.AreEqual(1, ret.MisingFiles.Count);
                Assert.AreEqual("fake2.png", ret.MisingFiles[0].Key);
                Assert.AreEqual("foo.jpg", ret.UnusedFiles[0].Key);
            }
        }

        private List<string> Added(Collection col)
        {
            var list = col.Media
                       .Database.QueryColumn<MediaTable>("select fname from media where csum is not null");
            return (from s in list select s.RelativePathName).ToList();
        }

        private List<string> Removed(Collection col)
        {
            var list = col.Media
                       .Database.QueryColumn<MediaTable>("select fname from media where csum is null");
            return (from s in list select s.RelativePathName).ToList();
        }

        [TestMethod, Obsolete, Ignore]
        public async Task TestScanChanges()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                Assert.IsTrue(await col.Media.Changed() != null);
                Assert.AreEqual(0, Added(col).Count);
                Assert.AreEqual(0, Removed(col).Count);

                //Add a file
                string fileName = "foo.jpg";
                string pathToFile = CreateFileInTempFolder(fileName);
                StorageFile storagefile = await tempFolder.GetFileAsync(fileName);
                string r = await col.Media.AddFile(storagefile);

                //should have been logged
                await col.Media.ScanForChangesAsync();
                Assert.AreEqual(1, Added(col).Count);
                Assert.AreEqual(0, Removed(col).Count);

                //if we modify it the cache won't notice
                pathToFile = col.Media.MediaFolder.Path + "\\" + fileName;
                Utils.WriteToFile(pathToFile, "world");
                Assert.AreEqual(1, Added(col).Count);
                Assert.AreEqual(0, Removed(col).Count);

                //But if we add another file, it will
                pathToFile = col.Media.MediaFolder.Path + "\\" + fileName + "2";
                CreateFile(pathToFile, "yo");
                await col.Media.ScanForChangesAsync(true);
                Assert.AreEqual(2, Added(col).Count);
                Assert.AreEqual(0, Removed(col).Count);

                //Deletions should get noticed too
                File.Delete(pathToFile);
                await col.Media.ScanForChangesAsync(true);
                Assert.AreEqual(1, Added(col).Count);
                Assert.AreEqual(1, Removed(col).Count);
            }
        }

        [TestMethod]
        public async Task TestStripIllegal()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                string badStr = "a:b|cd\\e/f\0g*h";
                string goodStr = "abcdefgh";
                Assert.AreEqual(goodStr, col.Media.StripIllegal(badStr));
                for(int i = 0; i < badStr.Length; i++)
                {
                    char c = badStr[i];
                    bool bad = col.Media.HasIllegal("something" + c + "morestring");
                    if (bad)
                        Assert.AreEqual(-1, goodStr.IndexOf(c));
                    else
                        Assert.IsTrue(goodStr.IndexOf(c) != -1);
                }
            }
        }


        [TestMethod]
        public async Task TestStripIllegalWithSquareBrackets()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                string badStr = "[something]Some Name (Full Version).flac";
                string goodStr = "somethingSomeName(FullVersion).flac";
                Assert.AreEqual(goodStr, col.Media.StripIllegal(badStr));
            }
        }

        [TestMethod]
        public async Task TestAddIllegalWithSquareBrackets()
        {
            using (Collection col = await Utils.GetEmptyCollection(tempFolder))
            {
                string badStr = "[something]Some Name (Full Version).flac";
                string goodStr = "somethingSomeName(FullVersion).flac";
                
                string pathToFile = CreateFileInTempFolder(badStr);                
                StorageFile storagefile = await tempFolder.GetFileAsync(badStr);
                string fileNameInMediaFolder = await col.Media.AddFile(storagefile);
                Assert.AreEqual(goodStr, fileNameInMediaFolder);
            }
        }


    }
}
