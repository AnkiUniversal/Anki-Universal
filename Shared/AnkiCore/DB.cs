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

namespace Shared.AnkiCore
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
        private SQLiteConnection dbConnection;

        public string GetPath()
        {
            return dbConnection.DatabasePath;
        }

        public DB(string absolutePathToFile)
        {
            try
            {
                dbConnection = new SQLiteConnection(new SQLitePlatformWinRT(), absolutePathToFile);
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

        public bool HasTable<T>() where T : class
        {            
            var name = typeof(T).Name;
            var count = dbConnection.ExecuteScalar<int>("SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = ?", name);
            if (count > 0)
                return true;
            else
                return false;
        }

        public bool HasTable<T>(string name) where T : class
        {
            var count = dbConnection.ExecuteScalar<int>("SELECT 1 FROM sqlite_master WHERE type = 'table' AND name = ?", name);
            if (count > 0)
                return true;
            else
                return false;
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
