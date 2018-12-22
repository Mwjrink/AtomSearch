using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Threading;

using Param = System.ValueTuple<string, System.Data.DbType, object>;
using Pool = OmniBox.SQLiteConnectionPool;

namespace OmniBox
{
    public static class DatabaseExtensionMethods
    {
        #region Methods

        public static IEnumerable<IDataRecord> Enumerate(this IDataReader reader)
        {
            while (reader.Read())
                yield return reader;
        }

        #endregion Methods
    }

    public sealed class SQLiteDatabase
            : IDisposable
    {
        #region Static

        public const string FullUriSharedMemoryString = "file:{0}?mode=memory&cache=shared";

        public static string DebugLogFilename => "DatabaseDebugLog.txt";

        public static SQLiteConnectionStringBuilder MakeSharedMemory(SQLiteConnectionStringBuilder bob, string sharedDatabaseId)
        {
            bob.DataSource = null;
            bob.Uri = null;

            bob.FullUri = String.Format(FullUriSharedMemoryString, sharedDatabaseId);
            bob.ToFullPath = false;

            return bob;
        }

        public static SQLiteConnectionStringBuilder GetDefault()
        {
            var bob = new SQLiteConnectionStringBuilder()
            {
                Version = 3,
                JournalMode = SQLiteJournalModeEnum.Wal,
                SyncMode = SynchronizationModes.Normal,
                BaseSchemaName = "main",
                Pooling = true,
                BusyTimeout = 5 * 60 * 1000, // 5 minutes
                PrepareRetries = 3,
                WaitTimeout = 5 * 60 * 1000
            };

            bob.Add("busy_timeout", 5 * 60 * 1000); // 5 minutes

            return bob;
        }

        #endregion Static

        #region Fields

        private readonly AsyncLocal<Tuple<Guid, SQLiteTransaction>> transaction
            = new AsyncLocal<Tuple<Guid, SQLiteTransaction>>();

        private readonly List<WeakReference<SQLiteTransaction>> disposableTransactions
            = new List<WeakReference<SQLiteTransaction>>();

        private Guid connectionKey;

        private AtomicBool disposing;

        public bool IsMemory { get; }

        #endregion Fields

        #region Constructors

        private SQLiteDatabase()
        {
            var connectionBuilder = GetDefault();
            connectionBuilder.JournalMode = SQLiteJournalModeEnum.Memory;
            connectionBuilder.SyncMode = SynchronizationModes.Off;
            MakeSharedMemory(connectionBuilder, Guid.NewGuid().ToString().Replace("-", String.Empty));

            IsMemory = true;

            connectionKey = Pool.AddConnection(connectionBuilder);
        }

        private SQLiteDatabase(Guid connectionKey, bool isMemory = false)
        {
            this.connectionKey = connectionKey;
            IsMemory = isMemory;
        }

        public SQLiteDatabase(string filepath)
        {
            var conBuilder = GetDefault();
            conBuilder.DataSource = filepath;

            connectionKey = Pool.AddConnection(conBuilder);
        }

        public static SQLiteDatabase GetMemoryDb()
        {
            return new SQLiteDatabase();
        }

        public static SQLiteDatabase LoadDbIntoMemoryNoSync(string filepath)
        {
            var memoryConnectionBuilder = GetDefault();
            memoryConnectionBuilder.JournalMode = SQLiteJournalModeEnum.Memory;
            memoryConnectionBuilder.SyncMode = SynchronizationModes.Off;
            memoryConnectionBuilder.Add("temp_store", "memory");
            MakeSharedMemory(memoryConnectionBuilder, Guid.NewGuid().ToString().Replace("-", String.Empty));

            var fileConnectionBuilder = GetDefault();
            fileConnectionBuilder.DataSource = filepath;
            fileConnectionBuilder.JournalMode = SQLiteJournalModeEnum.Memory;
            fileConnectionBuilder.SyncMode = SynchronizationModes.Full;

            var memoryKey = Pool.AddConnection(memoryConnectionBuilder);

            using (var memToken = Pool.GetToken(memoryKey))
            using (var file = new SQLiteConnection(fileConnectionBuilder.ConnectionString).OpenAndReturn())
                file.BackupDatabase(memToken.Connection, "main", "main", -1, null, 0);

            return new SQLiteDatabase(memoryKey, true);
        }

        public void Backup(string destPath)
        {
            var builder = GetDefault();
            builder.DataSource = destPath;
            builder.JournalMode = SQLiteJournalModeEnum.Memory;
            builder.Add("locking_mode", "EXCLUSIVE");

            using (var conn = new SQLiteConnection(builder.ConnectionString).OpenAndReturn())
            using (var token = Pool.GetToken(connectionKey))
                token.Connection.BackupDatabase(conn, "main", "main", -1, null, 0);
        }

        #endregion Constructors

        #region Transactions

        public Guid BeginTransaction()
        {
            var key = Guid.NewGuid();

            using (var token = Pool.GetToken(connectionKey, skipDispose: true))
                transaction.Value = (key, token.Connection.BeginTransaction()).ToTuple();

            disposableTransactions.Add(new WeakReference<SQLiteTransaction>(transaction.Value.Item2));

            return key;
        }

        public void CommitTransaction(Guid key)
        {
            if (transaction.Value.Item1 == key)
            {
                using (var token = Pool.GetToken(connectionKey, transaction: transaction.Value.Item2, skipDispose: false))
                    transaction.Value.Item2.Commit();

                transaction.Value.Item2.Dispose();
                transaction.Value = null;
            }
            else
                throw new InvalidOperationException("Attempted to commit a transaction with the wrong key provided.");
        }

        public void RevertTransaction(Guid key)
        {
            if (transaction.Value.Item1 == key)
            {
                using (var token = Pool.GetToken(connectionKey, transaction: transaction.Value.Item2, skipDispose: false))
                    transaction.Value.Item2.Rollback();

                transaction.Value.Item2.Dispose();
                transaction.Value = null;
            }
            else
                throw new InvalidOperationException("Attempted to revert a transaction with the wrong key provided.");
        }

        #endregion Transactions

        #region Public Methods

        private SQLiteCommand CreateCommand(SQLiteConnection connection, string commandText, IEnumerable<Param> parameters = null, Guid? key = null)
        {
            if (transaction?.Value?.Item1 != key)
                throw new InvalidOperationException("Attempted to create a command without having the correct transaction key.");

            var command = new SQLiteCommand(connection);

            command.CommandText = commandText;
            foreach (var param in parameters ?? Enumerable.Empty<Param>())
                command.Parameters.Add(param.Item1, param.Item2).Value = param.Item3;

            if (key.HasValue)
                command.Transaction = transaction.Value.Item2;

            return command;
        }

        internal SQLiteCommand PrepareInsertCommand(string tableName, IEnumerable<(string, DbType)> columns, Guid key)
        {
            var execute = "INSERT OR REPLACE INTO " + tableName + "(" + String.Join(", ", columns.Select(x => x.Item1)) + ") " +
                "VALUES (" + String.Join(", ", columns.Select(x => "@" + x.Item1 + "Param")) + ");";

            return PrepareCommand(execute, columns, key);
        }

        internal SQLiteCommand PrepareCommand(string execute, IEnumerable<(string, DbType)> columns = null, Guid? key = null)
        {
            if (transaction?.Value?.Item1 == key)
            {
                var command = new SQLiteCommand(execute);
                command.Transaction = transaction.Value.Item2;

                if (columns != null)
                    command.Parameters.AddRange(columns.Select(x => new SQLiteParameter("@" + x.Item1 + "Param", x.Item2)).ToArray());

                command.Prepare();

                return command;
            }
            else
                throw new InvalidOperationException("Attempted to prepare statement without having the correct transaction key.");
        }

        internal void ExecuteCommand(SQLiteCommand command)
        {
            using (var token = Pool.GetToken(connectionKey, conn =>
            {
                command.Connection = conn;
                return command;
            }, transaction: command.Transaction, skipDisposeCommand: true))
                token.Command.ExecuteNonQuery();
        }

        public IDataReader GetReader(string commandText, IEnumerable<Param> parameters = null, Guid? key = null)
        {
            using (var token = Pool.GetToken(connectionKey, conn => CreateCommand(conn, commandText, parameters, key), transaction: key != null ? transaction.Value.Item2 : null))
                return token.Command.ExecuteReader();
        }

        public int ExecuteNonQuery(string commandText, IEnumerable<Param> parameters = null, Guid? key = null)
        {
            using (var token = Pool.GetToken(connectionKey, conn => CreateCommand(conn, commandText, parameters, key), transaction: key != null ? transaction.Value.Item2 : null))
                return token.Command.ExecuteNonQuery();
        }

        public object ExecuteScalar(string commandText, IEnumerable<Param> parameters = null, Guid? key = null)
        {
            using (var token = Pool.GetToken(connectionKey, conn => CreateCommand(conn, commandText, parameters, key), transaction: key != null ? transaction.Value.Item2 : null))
                return token.Command.ExecuteScalar();
        }

        public void Delete(string tableName, string whereText, IEnumerable<Param> whereParams = null, Guid? key = null)
        {
            ExecuteNonQuery("delete from " + tableName + " where " + whereText + ";", whereParams, key);
        }

        public void Insert(string tableName, IEnumerable<Param> data, Guid? key = null)
        {
            var execute = "INSERT OR REPLACE INTO " +
                tableName + "(" + String.Join(", ", data.Select(x => x.Item1)) + ") " +
                "VALUES (" + String.Join(", ", data.Select(x => "@" + x.Item1 + "Param")) + ");";
            ExecuteNonQuery(execute, data.Select(x => ("@" + x.Item1 + "Param", x.Item2, x.Item3)), key);
        }

        public void ClearDB(Guid key)
        {
            ExecuteNonQuery("PRAGMA foreign_keys = OFF", key: key);
            var tables = GetReader("select NAME from SQLITE_MASTER where type='table' order by NAME;", key: key);
            foreach (var table in tables.Enumerate())
                ClearTable(table.GetString(0), key);
            ExecuteNonQuery("PRAGMA foreign_keys = ON", key: key);
        }

        public void ClearTable(string table, Guid key)
        {
            ExecuteNonQuery(String.Format("DELETE FROM {0};", table), key: key);
        }

        #endregion Public Methods

        #region Methods

        public void Dispose()
        {
            if (disposing.FalseToTrue())
            {
                foreach (var weak in disposableTransactions)
                    if (weak.TryGetTarget(out var target))
                        target?.Dispose();

                Pool.RemoveConnection(connectionKey);

                transaction.Value?.Item2?.Dispose();

                GC.Collect();
                GC.WaitForPendingFinalizers();
            }
        }

        #endregion Methods
    }
}
