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

namespace TestAnkiCore
{
    [TestClass]
    public class TestDB
    {
        public static StorageFolder localFolder = ApplicationData.Current.LocalFolder;
        public static string name = "testInit.db";
        public static string pathToFile = localFolder.Path + "\\" + name;
        public DB initDB;
        public Contacts[] contactArray = new Contacts[] {
                new Contacts("C1", "Phone", 0),
                new Contacts("E1", "Phone", 0),
                new Contacts("A1", "Phone", 0),
                new Contacts("B1", "Phone", 1),
                new Contacts("D1", "Phone", 0),
                new Contacts("G1", "Phone", 1),
                new Contacts("H1", "Phone", 0),
                new Contacts("F1", "Phone", 1)
        };

        public SubContacts[] subContactArray = new SubContacts[] {
                new SubContacts("C1", 0),
                new SubContacts("E1", 0),
                new SubContacts("A1", 0),
                new SubContacts("B1", 1),
                new SubContacts("D1", 0),
        };

        [TestInitialize()]
        public void Setup()
        {
            using (initDB = new DB(pathToFile))
            {
                initDB.RunInTransaction(() =>
                {
                    initDB.CreateTable<Contacts>();
                    foreach (Contacts c in contactArray)
                    {
                        initDB.Insert(c);
                    }
                    initDB.CreateTable<SubContacts>();
                    foreach (SubContacts c in subContactArray)
                    {
                        initDB.Insert(c);
                    }
                });
            }
        }

        [TestCleanup()]
        public void Clear()
        {
            using (initDB = new DB(pathToFile))
            {
                initDB.DeleteAll<Contacts>();
                initDB.DeleteAll<SubContacts>();
            }
        }

        [TestMethod]
        public async Task TestCreateDataBaseConnection()
        {
            try
            {
                string newpathToFile = localFolder.Path + "\\" + "testOpen.db";
                using (DB testDB = new DB(newpathToFile))
                {
                    StorageFile newFile = (StorageFile)(await localFolder.TryGetItemAsync("testOpen.db"));
                    Assert.IsNotNull(newFile);
                }
            }
            catch
            {
                Assert.Fail();
            }
        }

        [TestMethod]
        public async Task TestNonAsciiName()
        {
            try
            {
                string newName = "神のthần.db";
                string newpathToFile = localFolder.Path + "\\" + newName;
                using (DB testDB = new DB(newpathToFile))
                {
                    StorageFile newFile = (StorageFile)(await localFolder.TryGetItemAsync(newName));
                    Assert.IsNotNull(newFile);
                }
            }
            catch
            {
                Assert.Fail();
            }
        }

        [TestMethod]
        public void TestInvalidFilePath()
        {
            try
            {
                string path = @"E:\Temp";
                string newpathToFile = path + "\\" + name;
                using (DB newDB = new DB(pathToFile)) { }
            }
            catch (DBCorruptException e)
            {
                StringAssert.Contains(e.Message, "Can't open the database!");
            }
        }

        [TestMethod]
        public void TestQueryColumn()
        {
            using (DB newDB = new DB(pathToFile))
            {
                Contacts[] existingContacts = newDB.QueryColumn<Contacts>("select * from Contacts").ToArray();
                for (int j = 0; j < contactArray.Length; j++)
                {
                    Assert.AreEqual<string>(existingContacts[j].Name, contactArray[j].Name);
                    Assert.AreEqual<string>(existingContacts[j].PhoneNumber, contactArray[j].PhoneNumber);
                }
            }
        }

        [TestMethod]
        public void TestQueryCount()
        {
            using (DB newDB = new DB(pathToFile))
            {
                var count = newDB.QueryScalar<int>("select count() from Contacts where Id = 0");
                int i = 0;
                foreach (Contacts c in contactArray)
                    if (c.Id == 0)
                        i++;
                Assert.AreEqual(count, i);
            }
        }

        [TestMethod]
        public void TestQueryColumnString()
        {
            using (DB newDB = new DB(pathToFile))
            {
                var contacts = newDB.QueryColumn<Contacts>("select Name from Contacts");
                List<string> existingName = (from s in contacts select s.Name).ToList();
                for (int i = 0; i < contactArray.Length; i++)
                {
                    Assert.AreEqual(existingName[i], contactArray[i].Name);
                }
            }
        }

        [TestMethod]
        public void TestQueryFirstRow()
        {
            using (DB newDB = new DB(pathToFile))
            {
                var existingContacts = newDB.QueryFirstRow<Contacts>("select * from Contacts").ToArray();
                Assert.AreEqual(existingContacts.Length, 1);
                Assert.AreEqual<string>(existingContacts[0].Name, contactArray[0].Name);
                Assert.AreEqual<string>(existingContacts[0].PhoneNumber, contactArray[0].PhoneNumber);
            }
        }

        [TestMethod]
        public void TestQueryScalar()
        {
            using (DB newDB = new DB(pathToFile))
            {
                string name = newDB.QueryScalar<string>("select Name from Contacts");
                Assert.AreEqual<string>(contactArray[0].Name, name);

                int id = newDB.QueryScalar<int>("select ID from Contacts");
                Assert.AreEqual<int>(contactArray[0].Id, id);
            }
        }

        [TestMethod]
        public void TestNameMapping()
        {
            using (DB newDB = new DB(pathToFile))
            {
                NameMapping[] existingContacts = 
                    newDB.QueryColumn<NameMapping>("select Id, Name, PhoneNumber from Contacts").ToArray();
                for (int j = 0; j < contactArray.Length; j++)
                {
                    Assert.AreEqual<string>(existingContacts[j].n, contactArray[j].Name);
                    Assert.AreEqual<string>(existingContacts[j].p, contactArray[j].PhoneNumber);
                }
            }
        }

        [TestMethod]
        public void TestQuerySubClass()
        {
            using (DB newDB = new DB(pathToFile))
            {
                SubContacts[] existingContacts =
                    newDB.QueryColumn<SubContacts>("select Id, Name, PhoneNumber from Contacts").ToArray();
                for (int j = 0; j < contactArray.Length; j++)
                {
                    Assert.AreEqual(existingContacts[j].Name, contactArray[j].Name);
                    Assert.AreEqual(existingContacts[j].Id, contactArray[j].Id);
                }

                var contacts =
                    newDB.QueryColumn<SubContacts>("select Id, Name from Contacts where PhoneNumber = \"Phone\" ").ToArray();
                for (int j = 0; j < contactArray.Length; j++)
                {
                    Assert.AreEqual(contacts[j].Name, contactArray[j].Name);                    
                }
            }
        }

        [TestMethod]
        public void TestQueryTwoTablesCondition()
        {
            using (DB newDB = new DB(pathToFile))
            {
                var existingContacts =
                    newDB.QueryColumn<Contacts>("select c.Id, c.Name from Contacts c, SubContacts s where c.Name == s.Name");
                int length = Math.Min(contactArray.Length, subContactArray.Length);
                dynamic array;
                if (length == contactArray.Length)
                    array = contactArray;
                else
                    array = subContactArray;
                for (int j = 0; j < length; j++)
                {
                    Assert.AreEqual(existingContacts[j].Name, array[j].Name);
                    Assert.AreEqual(existingContacts[j].Id, array[j].Id);
                }
            }
        }

        [TestMethod]
        public void TestExceptionQueryTwoTablesCondition()
        {
            try
            {
                using (DB newDB = new DB(pathToFile))
                {
                    var existingContacts =
                        newDB.QueryColumn<Contacts>("select Id, Name from Contacts c, SubContacts s where c.Name = s.Name");
                }

                Assert.Fail();
            }
            catch(SQLiteException e)
            {
                StringAssert.Contains(e.Message, "ambiguous column name:");
            }
        }

        [TestMethod]
        public void TestExecute()
        {
            using (DB newDB = new DB(pathToFile))
            {
                newDB.Execute("INSERT INTO Contacts VALUES (111,'TestExcute','0000','2006-01-05')");

                Contacts[] existingContacts =
                    newDB.QueryFirstRow<Contacts>("select Id, Name, PhoneNumber, CreationDate from Contacts WHERE Name='TestExcute'").ToArray();
                Assert.IsNotNull(existingContacts);
                Assert.AreEqual(existingContacts[0].Id, 111);
            }
        }

        [TestMethod]
        public void TestExecuteMany()
        {
            List<object[]> obj = new List<object[]>();
            obj.Add(new object[] { contactArray[0].Id + 999, contactArray[0].Name,
                                   contactArray[0].PhoneNumber, contactArray[0].CreationDate });
            obj.Add(new object[] { contactArray[0].Id + 999, contactArray[1].Name,
                                   contactArray[1].PhoneNumber, contactArray[1].CreationDate });

            using (DB newDB = new DB(pathToFile))
            {
                newDB.ExecuteMany("insert or replace into Contacts VALUES (?,?,?,?)", obj);
 
                Contacts[] existingContacts =
                    newDB.QueryFirstRow<Contacts>("select Id, Name from Contacts WHERE Id=?", 
                                                    contactArray[0].Id + 999).ToArray();
                Assert.IsNotNull(existingContacts);
                foreach(var c in existingContacts)
                    Assert.AreEqual(contactArray[0].Id + 999, c.Id);
            }
        }

    }

    public class Contacts
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string PhoneNumber { get; set; }
        public string CreationDate { get; set; }

        public Contacts()
        {
        }
        public Contacts(string name, string phone_no) 
            : this(name, phone_no, 0)
        {
        }
        public Contacts(string Name, string PhoneNumber, int Id)
        {
            this.Name = Name;
            this.PhoneNumber = PhoneNumber;
            this.Id = Id;
            CreationDate = DateTime.Now.ToString();
        }
    }

    [SQLite.Net.Attributes.Table("Contacts")]
    public class NameMapping
    {
        [SQLite.Net.Attributes.Column("Id")]
        public int i { get; set; }
        [SQLite.Net.Attributes.Column("Name")]
        public string n { get; set; }
        [SQLite.Net.Attributes.Column("PhoneNumber")]
        public string p { get; set; }
        [SQLite.Net.Attributes.Column("CreationDate")]
        public string c { get; set; }
        
        public NameMapping()
        {
        }
        public NameMapping(string Name, string PhoneNumber)
            : this(Name, PhoneNumber, 0)
        {
        }
        public NameMapping(string Name, string PhoneNumber, int Id)
            : this(Name, PhoneNumber, 0, DateTime.Now.ToString())
        {
        }
        public NameMapping(string Name, string PhoneNumber, int Id, string CreationDate)
        {
            this.n = Name;
            this.p = PhoneNumber;
            this.i = Id;
            this.c = CreationDate;
        }
    }

    public class SubContacts
    {
        public int Id { get; set; }
        public string Name { get; set; }

        public SubContacts()
        {
        }
        public SubContacts(string Name,  int Id)
        {
            this.Name = Name;
            this.Id = Id;
        }
    }
}
