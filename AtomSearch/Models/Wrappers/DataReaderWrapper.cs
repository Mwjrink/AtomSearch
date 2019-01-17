using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AtomSearch
{
    public struct DataReaderWrapper : IDataReader, IDisposable
    {
        private IDataReader backingReader;
        private IDbConnection connection;

        public DataReaderWrapper(IDataReader backingReader, IDbConnection connection)
        {
            this.backingReader = backingReader;
            this.connection = connection;
        }

        public object this[int i] => backingReader[i];

        public object this[string name] => backingReader[name];

        public int Depth => backingReader.Depth;

        public bool IsClosed => backingReader.IsClosed;

        public int RecordsAffected => backingReader.RecordsAffected;

        public int FieldCount => backingReader.FieldCount;

        public void Close() => backingReader.Close();

        public bool GetBoolean(int i) => backingReader.GetBoolean(i);

        public byte GetByte(int i) => backingReader.GetByte(i);

        public long GetBytes(int i, long fieldOffset, byte[] buffer, int bufferoffset, int length) => backingReader.GetBytes(i, fieldOffset, buffer, bufferoffset, length);

        public char GetChar(int i) => backingReader.GetChar(i);

        public long GetChars(int i, long fieldoffset, char[] buffer, int bufferoffset, int length) => backingReader.GetChars(i, fieldoffset, buffer, bufferoffset, length);

        public IDataReader GetData(int i) => backingReader.GetData(i);

        public string GetDataTypeName(int i) => backingReader.GetDataTypeName(i);

        public DateTime GetDateTime(int i) => backingReader.GetDateTime(i);

        public decimal GetDecimal(int i) => backingReader.GetDecimal(i);

        public double GetDouble(int i) => backingReader.GetDouble(i);

        public Type GetFieldType(int i) => backingReader.GetFieldType(i);

        public float GetFloat(int i) => backingReader.GetFloat(i);

        public Guid GetGuid(int i) => backingReader.GetGuid(i);

        public short GetInt16(int i) => backingReader.GetInt16(i);

        public int GetInt32(int i) => backingReader.GetInt32(i);

        public long GetInt64(int i) => backingReader.GetInt64(i);

        public string GetName(int i) => backingReader.GetName(i);

        public int GetOrdinal(string name) => backingReader.GetOrdinal(name);

        public DataTable GetSchemaTable() => backingReader.GetSchemaTable();

        public string GetString(int i) => backingReader.GetString(i);

        public object GetValue(int i) => backingReader.GetValue(i);

        public int GetValues(object[] values) => backingReader.GetValues(values);

        public bool IsDBNull(int i) => backingReader.IsDBNull(i);

        public bool NextResult() => backingReader.NextResult();

        public bool Read() => backingReader.Read();

        public void Dispose()
        {
            backingReader.Dispose();
            connection.Dispose();
        }
    }
}
