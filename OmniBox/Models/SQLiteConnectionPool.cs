using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace OmniBox
{
    public static class SQLiteConnectionPool
    {
        private static readonly object syncRoot = new object();

        private static readonly ConcurrentDictionary<Guid, AsyncLocal<SQLiteConnection>> pool
            = new ConcurrentDictionary<Guid, AsyncLocal<SQLiteConnection>>();

        private static readonly ConcurrentDictionary<Guid, SQLiteConnection> globalPool
            = new ConcurrentDictionary<Guid, SQLiteConnection>();

        private static readonly ConcurrentDictionary<Guid, List<WeakReference<SQLiteConnection>>> disposables
            = new ConcurrentDictionary<Guid, List<WeakReference<SQLiteConnection>>>();

        static SQLiteConnectionPool()
        {
            SQLiteConnection.ConnectionPool = new StrongReferencedSQLiteConnectionPool();
        }

        public static Guid AddConnection(SQLiteConnectionStringBuilder connectionString, bool memory = false)
        {
            {
                connectionString.Pooling = true;

                var con = new SQLiteConnection(connectionString.ConnectionString);
                var key = Guid.NewGuid();

                if (memory)
                    con.Open();

                lock (syncRoot)
                {
                    globalPool.TryAdd(key, con);
                    Reset(key);
                }

                return key;
            }
        }

        public static void Reset(Guid key) =>
                    pool[key] = new AsyncLocal<SQLiteConnection>();

        public static void RemoveConnection(Guid key)
        {
#if DEBUG
            if (!globalPool.ContainsKey(key))
                throw new InvalidOperationException("Attempted to remove a connection using an invalid key.");
#endif
            lock (syncRoot)
            {
                try
                { }
                finally
                {
                    // Ensure that this connection is not cloned again
                    if (globalPool.TryRemove(key, out var connection))
                    {
                        // Remove the entry from the pool
                        pool.TryRemove(key, out var value);

                        foreach (var checkedOut in disposables[key])
                            if (checkedOut.TryGetTarget(out var conn))
                                conn.Dispose();

                        //TODO: FIND A BETTER WAY
                        {
                            var conn = ((SQLiteConnection)connection.Clone());
                            using (conn.OpenAndReturn())
                                SQLiteConnection.ClearPool(conn);
                        }
                        connection.Dispose();

                        //availableConnections.Remove();
                        // Dispose of all existing clones
                        //foreach (var connection in value.Values)
                        //    connection.Dispose();
                    }
                }
            }
        }

        public static SQLCToken GetToken(Guid key, Func<SQLiteConnection, SQLiteCommand> commandCreator = null, bool? skipDispose = null,
            SQLiteTransaction transaction = null, bool skipDisposeCommand = false)
        {
#if DEBUG
            if (!globalPool.ContainsKey(key))
                throw new InvalidOperationException("Attempted to retrieve a connection using an invalid key.");
#endif
            var targetConnection = transaction?.Connection ?? pool[key].Value;

            if (targetConnection == null)
            {
                disposables.GetOrAdd(key, new List<WeakReference<SQLiteConnection>>())
                    .Add(new WeakReference<SQLiteConnection>(pool[key].Value = (SQLiteConnection)globalPool[key].Clone(), true));
                targetConnection = pool[key].Value;
            }

            return new SQLCToken(targetConnection, key, commandCreator, skipDispose ?? transaction != null, skipDisposeCommand);
        }

        public struct SQLCToken
            : IDisposable
        {
            private readonly bool skipDispose;
            private readonly bool skipDisposeCommand;
            private readonly Guid key;

            public readonly SQLiteCommand Command;
            public readonly SQLiteConnection Connection;

            public SQLCToken(SQLiteConnection conn, Guid key, Func<SQLiteConnection, SQLiteCommand> commandCreator, bool skipDispose, bool skipDisposeCommand)
            {
                Connection = conn;
                this.skipDispose = skipDispose;
                this.skipDisposeCommand = skipDisposeCommand;
                Command = commandCreator?.Invoke(Connection) ?? new SQLiteCommand(Connection);

                // This is for transactions because we keep the connection open while they are out
                if (Connection.State != System.Data.ConnectionState.Open)
                    Connection.Open();

                this.key = key;
            }

            public void Dispose()
            {
                if (!skipDisposeCommand)
                    Command.Dispose();

                if (!skipDispose)
                    Connection?.Dispose();

                Reset(key);
            }
        }
    }
}
