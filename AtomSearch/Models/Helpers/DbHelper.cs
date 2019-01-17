using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AtomSearch
{
    public static class DbHelper
    {
        // DB
        /*
         * CommandText
         *    Compared against with string difference matcher, PRIMARY KEY TEXT
         * DependentUpon?
         *     Plugin this depends on
         * UseCount (better name?)
         *     self explanatory
         */

        public const string CommandTextColumnName = "CommandText";
        public const string UsagesTextColumnName = "Uses";
        public const string MainTableName = "main";

        private static readonly string updateCommand =
        @"INSERT OR REPLACE INTO main (CommandText, Uses) VALUES
          (
              @command,
              IFNULL((SELECT Uses + 1 FROM main WHERE CommandText = @command),1)
          );";

        private static SQLiteCommand incrementCommandUsagesCommand;

        static DbHelper()
        {
            incrementCommandUsagesCommand = new SQLiteCommand(updateCommand);
            incrementCommandUsagesCommand.Parameters.Add("@command", DbType.String);
            incrementCommandUsagesCommand.Prepare();
        }

        public static void IncrementCommandUsages(string command)
        {
            using (var conn = new SQLiteConnection(SettingsHelper.DbPath))
            {
                incrementCommandUsagesCommand.Parameters["@command"].Value = command;
                incrementCommandUsagesCommand.Connection = conn;
                incrementCommandUsagesCommand.ExecuteNonQuery();
            }
        }

        public static IDataReader GetCommandUsages(string executionText, IEnumerable<(string name, string command)> parameters)
        {
            var conn = new SQLiteConnection(SettingsHelper.DbPath);
            using (var command = new SQLiteCommand(executionText, conn))
            {
                foreach (var (name, comm) in parameters)
                    command.Parameters.AddWithValue(name, comm);

                return new DataReaderWrapper(command.ExecuteReader(CommandBehavior.CloseConnection), conn);
            }
        }

        public static IEnumerable<IDataRecord> Enumerate(this IDataReader reader)
        {
            while (reader.Read())
                yield return reader;
        }
    }
}
