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
        public StorageFolder localFolder;
        public string name = "abc.db";
        public Contacts[] contactArray;
        public string pathToFile;
        public DB initDB;

        [TestInitialize()]
        public void Setup()
        {
            localFolder = ApplicationData.Current.LocalFolder;
            pathToFile = localFolder.Path + "\\" + name;

            contactArray = new Contacts[] {
                new Contacts("A1", "A1", 0),
                new Contacts("B1", "B1", 1),
                new Contacts("C1", "C1", 2) };

            using (initDB = new DB(pathToFile))
            {
                initDB.CreateTable<Contacts>();
                foreach (Contacts c in contactArray)
                {
                    initDB.Insert(c);
                }
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
            }
        }


        [TestMethod]
        public void TestExecute()
        {
            using (DB newDB = new DB(pathToFile))
            {
                newDB.Execute("INSERT INTO Contacts VALUES (111,'TestExcute','0000','2006-01-05')");
            }

            using (DB newDB = new DB(pathToFile))
            {
                Contacts[] existingContacts =
                    newDB.QueryFirstRow<Contacts>("select Id, Name, PhoneNumber, CreationDate from Contacts WHERE Name='TestExcute'").ToArray();
                Assert.IsNotNull(existingContacts);
                Assert.AreEqual(existingContacts[0].Id, 111);
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
