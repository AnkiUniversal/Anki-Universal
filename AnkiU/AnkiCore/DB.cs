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
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using SQLite.Net;
using SQLite.Net.Platform.WinRT;
using System.Collections;

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

        public DB(string absolutePathToFile)
        {
            try
            {
                dbConnection = new SQLiteConnection(new SQLitePlatformWinRT(), absolutePathToFile);
                isModified = false;
            }
            catch (SQLiteException e)
            {
                if (dbConnection == null)
                {
                    string msg = String.Format("Can't open the database at {0}", absolutePathToFile);
                    throw new DBCorruptException(msg, e);
                }
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
            string s = sql.Trim().ToLowerInvariant();
            foreach (string stmt in MOD_SQLS)
                if (s.StartsWith(stmt))
                {
                    isModified = true;
                    return;
                }
        }

        public void ExecuteMany(string sql, List<object[]> list)
        {
            isModified = true;

            dbConnection.RunInTransaction(() =>
            {
                foreach (object[] obj in list)
                    dbConnection.Execute(sql, obj);
            });
        }

        public void RunInTransaction(Action action)
        {            
            dbConnection.RunInTransaction(action);
        }

        public bool HasTable<T>() where T : class
        {            
            var name = typeof(T).Name;
            var count = dbConnection.ExecuteScalar<int>("SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = ?", name);
            if (count > 0)
                return true;
            else
                return false;
        }

        public string SaveTransactionPoint()
        {
            return dbConnection.SaveTransactionPoint();
        }

        public void Rollback()
        {
            dbConnection.Rollback();
        }

        public void RollbackTo(string savepoint)
        {
            dbConnection.RollbackTo(savepoint);
        }

        public void BeginTransaction()
        {
            dbConnection.BeginTransaction();
        }

        public void Commit()
        {
            dbConnection.Commit();
        }

        public void ExecuteScript(string sql)
        {
            isModified = true;
            string[] queries = sql.Split(new string[] { ";" }, StringSplitOptions.RemoveEmptyEntries);
            dbConnection.RunInTransaction(() =>
            {
                foreach (string q in queries)
                    dbConnection.Execute(q);
            });
        }

        public T QueryScalar<T>(string query)
        {
            return dbConnection.ExecuteScalar<T>(query);
        }

        public T QueryScalar<T>(string query, params object[] obj)
        {
            return dbConnection.ExecuteScalar<T>(query, obj);
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

        public void Insert(object obj, Type type = null)
        {
            isModified = true;
            if (type != null)
                dbConnection.Insert(obj, type);
            else
                dbConnection.Insert(obj);
        }

        public void InsertOrReplace(object obj, Type type = null)
        {
            isModified = true;
            if (type != null)
                dbConnection.InsertOrReplace(obj, type);
            else
                dbConnection.InsertOrReplace(obj);
        }

        public void Update(object obj, Type type = null)
        {
            isModified = true;
            if (type != null)
                dbConnection.Update(obj, type);
            else
                dbConnection.Update(obj);            
        }

        public void DropTable<T>()
        {
            try
            {
                dbConnection.DropTable<T>();
            }
            catch //If we cannot drop table -> do nothing
            { }
        }

        public void InsertAll(IEnumerable obj, bool runInTransaction = true)
        {
            isModified = true;
            dbConnection.InsertAll(obj, runInTransaction);
        }

        //Mainly used for testing purpose
        public void DeleteAll<T>()
        {
            isModified = true;
            dbConnection.DeleteAll<T>();
        }

        public void Delete<T>(object primaryKey)
        {
            isModified = true;
            dbConnection.Delete<T>(primaryKey);
        }

        public void Close()
        {
            dbConnection.Close();
        }

        public TableQuery<T> GetTable<T>() where T : class
        {
            return dbConnection.Table<T>();
        }
   
        public void Dispose()
        {
            dbConnection.Close();
        }
    }
}
