using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using SQLiteHandlePoolQueue = OmniBox.StrongReferencedSQLiteConnectionPool.PoolQueue<System.Runtime.InteropServices.CriticalHandle>;

namespace OmniBox
{
    /// <summary>
    /// This default method implementations in this class should not be used by
    /// applications that make use of COM (either directly or indirectly) due
    /// to possible deadlocks that can occur during finalization of some COM
    /// objects.
    /// </summary>
    internal class StrongReferencedSQLiteConnectionPool : ISQLiteConnectionPool
    {
        /// <summary>
        /// Keeps track of connections made on a specified file.  The PoolVersion
        /// dictates whether old objects get returned to the pool or discarded
        /// when no longer in use.
        /// </summary>
        /// <typeparam name="T"></typeparam>
        internal sealed class PoolQueue<T>
        {
            /// <summary>
            /// The queue of weak references to the actual database connection
            /// handles.
            /// </summary>
            internal readonly Queue<T> Queue =
                new Queue<T>();

            /// <summary>
            /// This pool version associated with the database connection
            /// handles in this pool queue.
            /// </summary>
            internal int poolVersion;

            /// <summary>
            /// The maximum size of this pool queue.
            /// </summary>
            internal int maxPoolSize;

            /// <summary>
            /// Constructs a connection pool queue using the specified version
            /// and maximum size.  Normally, all the database connection
            /// handles in this pool are associated with a single database file
            /// name.
            /// </summary>
            /// <param name="version">
            /// The initial pool version for this connection pool queue.
            /// </param>
            /// <param name="maxSize">
            /// The initial maximum size for this connection pool queue.
            /// </param>
            internal PoolQueue(
                int version,
                int maxSize
                )
            {
                poolVersion = version;
                maxPoolSize = maxSize;
            }
        }

        /// <summary>
        /// This field is used to synchronize access to the private static data
        /// in this class.
        /// </summary>
        private static readonly object _syncRoot = new object();

        /// <summary>
        /// The dictionary of connection pools, based on the normalized file
        /// name of the SQLite database.
        /// </summary>
        private static readonly SortedList<string, SQLiteHandlePoolQueue> _queueList =
            new SortedList<string, SQLiteHandlePoolQueue>(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// The default version number new pools will get.
        /// </summary>
        private static int _poolVersion = 1;

        /// <summary>
        /// The number of connections successfully opened from any pool.
        /// This value is incremented by the Remove method.
        /// </summary>
        private static int _poolOpened = 0;

        /// <summary>
        /// The number of connections successfully closed from any pool.
        /// This value is incremented by the Add method.
        /// </summary>
        private static int _poolClosed = 0;

        private static readonly Type _sqliteConnectionHandleType;

        static StrongReferencedSQLiteConnectionPool()
        {
            _sqliteConnectionHandleType = typeof(SQLiteConnection).GetMethod(nameof(SQLiteConnection.CreateHandle)).ReturnType;
        }

        /// <summary>
        /// Counts the number of pool entries matching the specified file name.
        /// </summary>
        /// <param name="fileName">
        /// The file name to match or null to match all files.
        /// </param>
        /// <param name="counts">
        /// The pool entry counts for each matching file.
        /// </param>
        /// <param name="openCount">
        /// The total number of connections successfully opened from any pool.
        /// </param>
        /// <param name="closeCount">
        /// The total number of connections successfully closed from any pool.
        /// </param>
        /// <param name="totalCount">
        /// The total number of pool entries for all matching files.
        /// </param>
        public void GetCounts(
            string fileName,
            ref Dictionary<string, int> counts,
            ref int openCount,
            ref int closeCount,
            ref int totalCount
            )
        {
            lock (_syncRoot)
            {
                openCount = _poolOpened;
                closeCount = _poolClosed;

                if (counts == null)
                {
                    counts = new Dictionary<string, int>(
                        StringComparer.OrdinalIgnoreCase);
                }

                if (fileName != null)
                {
                    if (_queueList.TryGetValue(fileName, out var queue))
                    {
                        Queue<CriticalHandle> poolQueue = queue.Queue;
                        int count = poolQueue?.Count ?? 0;

                        counts.Add(fileName, count);
                        totalCount += count;
                    }
                }
                else
                {
                    foreach (KeyValuePair<string, SQLiteHandlePoolQueue> pair in _queueList)
                    {
                        if (pair.Value == null)
                            continue;

                        Queue<CriticalHandle> poolQueue = pair.Value.Queue;
                        int count = poolQueue?.Count ?? 0;

                        counts.Add(pair.Key, count);
                        totalCount += count;
                    }
                }
            }
        }

        /// <summary>
        /// Disposes of all pooled connections associated with the specified
        /// database file name.
        /// </summary>
        /// <param name="fileName">
        /// The database file name.
        /// </param>
        public void ClearPool(string fileName)
        {
            lock (_syncRoot)
            {
                if (_queueList.TryGetValue(fileName, out var queue))
                {
                    queue.poolVersion++;

                    Queue<CriticalHandle> poolQueue = queue.Queue;
                    if (poolQueue == null)
                        return;

                    while (poolQueue.Count > 0)
                    {
                        var handle = poolQueue.Dequeue();
                        if (IsSQLiteConnectionHandle(handle))
                        {
                            handle.Dispose();
                            GC.KeepAlive(handle);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// Disposes of all pooled connections.
        /// </summary>
        public void ClearAllPools()
        {
            lock (_syncRoot)
            {
                foreach (KeyValuePair<string, SQLiteHandlePoolQueue> pair in _queueList)
                {
                    if (pair.Value == null)
                        continue;

                    Queue<CriticalHandle> poolQueue = pair.Value.Queue;

                    while (poolQueue.Count > 0)
                    {
                        var handle = poolQueue.Dequeue();

                        if (IsSQLiteConnectionHandle(handle))
                        {
                            handle.Dispose();
                            GC.KeepAlive(handle);
                        }
                    }

                    // NOTE: Keep track of the highest revision so we can
                    //       go one higher when we are finished.
                    if (_poolVersion <= pair.Value.poolVersion)
                        _poolVersion = pair.Value.poolVersion + 1;
                }

                // NOTE: All pools are cleared and we have a new highest
                //       version number to force all old version active
                //       items to get discarded instead of going back to
                //       the queue when they are closed.  We can get away
                //       with this because we have pumped up the pool
                //       version out of range of all active connections,
                //       so they will all get discarded when they try to
                //       put themselves back into their pools.
                _queueList.Clear();
            }
        }

        /// <summary>
        /// Adds a connection to the pool of those associated with the
        /// specified database file name.
        /// </summary>
        /// <param name="fileName">
        /// The database file name.
        /// </param>
        /// <param name="obj">
        /// The database connection handle as an object.
        /// </param>
        /// <param name="version">
        /// The connection pool version at the point the database connection
        /// handle was received from the connection pool.  This is also the
        /// connection pool version that the database connection handle was
        /// created under.
        /// </param>
        public void Add(
            string fileName,
#pragma warning disable RCS1168 // Parameter name differs from base name.
            object obj,
#pragma warning restore RCS1168 // Parameter name differs from base name.
            int version
            )
        {
            var handle = obj as CriticalHandle;
            if (!IsSQLiteConnectionHandle(handle))
                return;

            lock (_syncRoot)
            {
                // NOTE: If the queue does not exist in the pool, then it
                //       must have been cleared sometime after the
                //       connection was created.
                if (_queueList.TryGetValue(fileName, out var queue)
                    && (version == queue.poolVersion))
                {
                    ResizePool(queue, true);

                    Queue<CriticalHandle> poolQueue = queue.Queue;
                    if (poolQueue == null)
                        return;

                    poolQueue.Enqueue(handle);
                    Interlocked.Increment(ref _poolClosed);
                }
                else
                {
                    handle.Close();
                }

                GC.KeepAlive(handle);
            }
        }

        /// <summary>
        /// Removes a connection from the pool of those associated with the
        /// specified database file name with the intent of using it to
        /// interact with the database.
        /// </summary>
        /// <param name="fileName">
        /// The database file name.
        /// </param>
        /// <param name="maxPoolSize">
        /// The new maximum size of the connection pool for the specified
        /// database file name.
        /// </param>
        /// <param name="version">
        /// The connection pool version associated with the returned database
        /// connection handle, if any.
        /// </param>
        /// <returns>
        /// An SQLiteConnectionHandle
        /// The database connection handle associated with the specified
        /// database file name or null if it cannot be obtained.
        /// </returns>
        public object Remove(
            string fileName,
            int maxPoolSize,
            out int version
            )
        {
            int localVersion;
            Queue<CriticalHandle> poolQueue;

            // NOTE: This lock cannot be held while checking the queue for
            //       available connections because other methods of this
            //       class are called from the GC finalizer thread and we
            //       use the WaitForPendingFinalizers method (below).
            //       Holding this lock while calling that method would
            //       therefore result in a deadlock.  Instead, this lock
            //       is held only while a temporary copy of the queue is
            //       created, and if necessary, when committing changes
            //       back to that original queue prior to returning from
            //       this method.
            lock (_syncRoot)
            {
                // NOTE: Default to the highest pool version.
                version = _poolVersion;

                // NOTE: If we didn't find a pool for this file, create one
                //       even though it will be empty.  We have to do this
                //       here because otherwise calling ClearPool() on the
                //       file will not work for active connections that have
                //       never seen the pool yet.
                if (!_queueList.TryGetValue(fileName, out var queue))
                {
                    queue = new SQLiteHandlePoolQueue(_poolVersion, maxPoolSize);
                    _queueList.Add(fileName, queue);

                    return null;
                }

                // NOTE: We found a pool for this file, so use its version
                //       number.
                version = localVersion = queue.poolVersion;
                queue.maxPoolSize = maxPoolSize;

                // NOTE: Now, resize the pool to the new maximum size, if
                //       necessary.
                ResizePool(queue, false);

                // NOTE: Try and get a pooled connection from the queue.
                poolQueue = queue.Queue;
                if (poolQueue == null)
                    return null;

                // NOTE: Temporarily tranfer the queue for this file into
                //       a local variable.  The queue for this file will
                //       be modified and then committed back to the real
                //       pool list (below) prior to returning from this
                //       method.
                _queueList.Remove(fileName);
                poolQueue = new Queue<CriticalHandle>(poolQueue);
            }

            try
            {
                while (poolQueue.Count > 0)
                {
                    var handle = poolQueue.Dequeue();
                    if (IsSQLiteConnectionHandle(handle))
                        continue;

                    // BUGFIX: For ticket [996d13cd87], step #1.  After
                    //         this point, make sure that the finalizer for
                    //         the connection handle just obtained from the
                    //         queue cannot START running (i.e. it may
                    //         still be pending but it will no longer start
                    //         after this point).
                    GC.SuppressFinalize(handle);

                    try
                    {
                        // BUGFIX: For ticket [996d13cd87], step #2.  Now,
                        //         we must wait for all pending finalizers
                        //         which have STARTED running and have not
                        //         yet COMPLETED.  This must be done just
                        //         in case the finalizer for the connection
                        //         handle just obtained from the queue has
                        //         STARTED running at some point before
                        //         SuppressFinalize was called on it.
                        //         After this point, checking properties of
                        //         the connection handle (e.g. IsClosed)
                        //         should work reliably without having to
                        //         worry that they will (due to the
                        //         finalizer) change out from under us.
                        GC.WaitForPendingFinalizers();

                        // BUGFIX: For ticket [996d13cd87], step #3.  Next,
                        //         verify that the connection handle is
                        //         actually valid and [still?] not closed
                        //         prior to actually returning it to our
                        //         caller.
                        if (!handle.IsInvalid && !handle.IsClosed)
                        {
                            Interlocked.Increment(ref _poolOpened);
                            return handle;
                        }
                    }
                    finally
                    {
                        // BUGFIX: For ticket [996d13cd87], step #4.  Next,
                        //         we must re-register the connection
                        //         handle for finalization now that we have
                        //         a strong reference to it (i.e. the
                        //         finalizer will not run at least until
                        //         the connection is subsequently closed).
                        GC.ReRegisterForFinalize(handle);
                    }

                    GC.KeepAlive(handle);
                }
            }
            finally
            {
                // BUGFIX: For ticket [996d13cd87], step #5.  Finally,
                //         commit any changes to the pool/queue for this
                //         database file.
                lock (_syncRoot)
                {
                    // NOTE: We must check [again] if a pool exists for
                    //       this file because one may have been added
                    //       while the search for an available connection
                    //       was in progress (above).
                    Queue<CriticalHandle> newPoolQueue;
                    bool addPool;

                    if (_queueList.TryGetValue(fileName, out var queue))
                    {
                        addPool = false;
                    }
                    else
                    {
                        addPool = true;
                        queue = new SQLiteHandlePoolQueue(localVersion, maxPoolSize);
                    }

                    newPoolQueue = queue.Queue;

                    while (poolQueue.Count > 0)
                        newPoolQueue.Enqueue(poolQueue.Dequeue());

                    ResizePool(queue, false);

                    if (addPool)
                        _queueList.Add(fileName, queue);
                }
            }

            return null;
        }

        /// <summary>
        /// We do not have to thread-lock anything in this function, because it
        /// is only called by other functions above which already take the lock.
        /// </summary>
        /// <param name="queue">
        /// The pool queue to resize.
        /// </param>
        /// <param name="add">
        /// If a function intends to add to the pool, this is true, which
        /// forces the resize to take one more than it needs from the pool.
        /// </param>
        private void ResizePool(
            SQLiteHandlePoolQueue queue,
            bool add
            )
        {
            int target = queue.maxPoolSize;

            if (add && target > 0)
                target--;

            Queue<CriticalHandle> poolQueue = queue.Queue;
            if (poolQueue == null)
                return;

            while (poolQueue.Count > target)
            {
                var handle = poolQueue.Dequeue();
                if (IsSQLiteConnectionHandle(handle))
                {
                    handle.Dispose();
                    GC.KeepAlive(handle);
                }
            }
        }

        private bool IsSQLiteConnectionHandle(CriticalHandle handle)
        {
            if (handle is null)
                return false;

            // if I can just get the type from somewhere and store it I can compare it here
            return handle.GetType() == _sqliteConnectionHandleType;//typeof(SQLiteConnectionHandle);
        }
    }
}
