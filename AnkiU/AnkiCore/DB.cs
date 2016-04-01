using System;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using SQLite.Net;
using SQLite.Net.Platform.WinRT;

namespace AnkiU.AnkiCore
{
    public class DBCorruptException : Exception
    {
        public string message;
        public DBCorruptException(string message, SQLiteException error)
            : base(message, error)
        {

        }
    }

    public class DB : IDisposable
    {
        private static string[] MOD_SQLS = new string[] { "insert", "update", "delete" };

        private SQLiteConnection dbConnection;

        private bool isModified = false;
        public bool IsModified { get { return isModified; } set { isModified = value; } }

        public string GetPath()
        {
            return dbConnection.DatabasePath;
        }

        public DB(string pathToFile)
        {
            try
            {
                dbConnection = new SQLiteConnection(new SQLitePlatformWinRT(), pathToFile);
                isModified = false;
            }
            catch (SQLiteException e)
            {
                if (dbConnection == null)
                    throw new DBCorruptException("Can't open the database!", e);
            }
        }

        public void Execute(string sql)
        {
            CheckIfModfied(sql);
            dbConnection.Execute(sql);
        }

        public void Execute(string sql, params object[] obj)
        {
            CheckIfModfied(sql);
            dbConnection.Execute(sql, obj);
        }

        private void CheckIfModfied(string sql)
        {
            string s = sql.Trim().ToLower();
            foreach (string stmt in MOD_SQLS)
                if (s.StartsWith(stmt))
                {
                    isModified = true;
                    return;
                }
        }

        public void ExecuteMany(string sql, params object[] list)
        {
            isModified = true;

            dbConnection.RunInTransaction(() =>
            {
                foreach (object obj in list)
                    dbConnection.Execute(sql, obj);
            });
        }

        public void ExecuteScript(string sql)
        {
            isModified = true;
            string[] queries = sql.Split(';');
            foreach (string q in queries)
                dbConnection.Execute(q);
        }

        public T QueryScalar<T>(string query)
        {
            return dbConnection.ExecuteScalar<T>(query);
        }

        public List<T> QueryColumn<T>(string query) where T : class
        {
            return dbConnection.Query<T>(query);
        }

        public List<T> QueryColumn<T>(string query, params object[] args) where T : class
        {
            return dbConnection.Query<T>(query, args);
        }

        public List<T> QueryFirstRow<T>(string query) where T : class
        {
            string s = " limit 1";
            return dbConnection.Query<T>(query + s);
        }

        public List<T> QueryFirstRow<T>(string query, params object[] args) where T : class
        {
            string s = " limit 1";
            return dbConnection.Query<T>(query + s, args);
        }

        public void CreateTable<T>(SQLite.Net.Interop.CreateFlags flag = SQLite.Net.Interop.CreateFlags.None)
        {
            dbConnection.CreateTable<T>(flag);
        }

        public void CreateTable(Type type, SQLite.Net.Interop.CreateFlags flag = SQLite.Net.Interop.CreateFlags.None)
        {
            dbConnection.CreateTable(type, flag);
        }

        public void Commit()
        {
            dbConnection.Commit();
        }

        public void RollBack()
        {
            dbConnection.Rollback();
        }

        public void Insert(object obj)
        {
            isModified = true;
            dbConnection.Insert(obj);
        }

        public void Close()
        {
            dbConnection.Close();
        }
   
        public void Dispose()
        {
            dbConnection.Close();
        }
    }
}
