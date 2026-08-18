using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using CursorDesk.Core;

namespace CursorDesk.Storage
{
    public sealed class SqliteStore
    {
        private readonly string _connectionString;

        public SqliteStore(string databasePath)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
            {
                throw new ArgumentException("Database path is required.", nameof(databasePath));
            }

            var dir = Path.GetDirectoryName(databasePath);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            _connectionString = "Data Source=" + databasePath + ";Version=3;Foreign Keys=True;";
            EnsureSchema();
        }

        public static SqliteStore CreateDefault()
        {
            var path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "cursordesk.sessions.db");
            return new SqliteStore(path);
        }

        public long Insert(Session session)
        {
            if (session == null)
            {
                throw new ArgumentNullException("session");
            }

            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "INSERT INTO Sessions (Model, Prompt, Result, CreatedAt) " +
                    "VALUES (@model, @prompt, @result, @createdAt); " +
                    "SELECT last_insert_rowid();";
                Add(cmd, "@model", session.Model ?? string.Empty);
                Add(cmd, "@prompt", session.Prompt ?? string.Empty);
                Add(cmd, "@result", session.Result ?? string.Empty);
                Add(cmd, "@createdAt", session.CreatedAt.ToUniversalTime().ToString("o"));
                var id = Convert.ToInt64(cmd.ExecuteScalar());
                session.Id = id;
                return id;
            }
        }

        public IList<Session> List()
        {
            var list = new List<Session>();
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "SELECT Id, Model, Prompt, Result, CreatedAt " +
                    "FROM Sessions ORDER BY Id DESC";
                using (var reader = cmd.ExecuteReader())
                {
                    while (reader.Read())
                    {
                        list.Add(ReadSession(reader));
                    }
                }
            }

            return list;
        }

        private void EnsureSchema()
        {
            using (var conn = Open())
            using (var cmd = conn.CreateCommand())
            {
                cmd.CommandText =
                    "CREATE TABLE IF NOT EXISTS Sessions (" +
                    "Id INTEGER PRIMARY KEY AUTOINCREMENT, " +
                    "Model TEXT, " +
                    "Prompt TEXT, " +
                    "Result TEXT, " +
                    "CreatedAt TEXT" +
                    ");";
                cmd.ExecuteNonQuery();
            }
        }

        private SQLiteConnection Open()
        {
            var conn = new SQLiteConnection(_connectionString);
            conn.Open();
            return conn;
        }

        private static Session ReadSession(IDataRecord row)
        {
            var createdRaw = row["CreatedAt"] == DBNull.Value ? null : Convert.ToString(row["CreatedAt"]);
            DateTime createdAt;
            if (!DateTime.TryParse(createdRaw, null, System.Globalization.DateTimeStyles.RoundtripKind, out createdAt))
            {
                createdAt = DateTime.UtcNow;
            }

            return new Session
            {
                Id = Convert.ToInt64(row["Id"]),
                Model = row["Model"] == DBNull.Value ? string.Empty : Convert.ToString(row["Model"]),
                Prompt = row["Prompt"] == DBNull.Value ? string.Empty : Convert.ToString(row["Prompt"]),
                Result = row["Result"] == DBNull.Value ? string.Empty : Convert.ToString(row["Result"]),
                CreatedAt = createdAt
            };
        }

        private static void Add(SQLiteCommand cmd, string name, object value)
        {
            var p = cmd.CreateParameter();
            p.ParameterName = name;
            p.Value = value ?? DBNull.Value;
            cmd.Parameters.Add(p);
        }
    }
}
