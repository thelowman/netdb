using System;
using System.Data.SqlClient;
using System.Data.SqlTypes;
using System.Collections.Generic;
using System.Text;

namespace netdb
{
    public abstract class Sql : IDisposable
    {
        private SqlConnection _connection = null;
        private SqlCommand _command = null;
        private SqlDataReader _reader = null;
        private StringBuilder _stringBuilder = new StringBuilder();
        private Exception _exception = null;





        /// <summary>
        /// This static member keeps a running list of active instances.
        /// It has proven useful in resolving issues with improperly
        /// disposing of a connection.
        /// </summary>
        public static List<Sql> ActiveInstances = new List<Sql>();
        /// <summary>
        /// Override this value with the appropriate connection string.
        /// </summary>
        protected abstract string ConnectionString { get; }
        /// <summary>
        /// Messages returned from the database are collected in this
        /// generic list.  These messages can be retrieved through the
        /// public Messages property.
        /// </summary>
        protected List<string> _messages = new List<string>();

        
        
        
        /// <summary>
        /// Provides access to the underlying data reader.
        /// </summary>
        public SqlDataReader Reader { get { return _reader; } }
        /// <summary>
        /// Gets the list of messages returned by the data base.
        /// </summary>
        public string[] Messages { get { return _messages.ToArray(); } }
        /// <summary>
        /// Reflects some information about the state of the database.
        /// </summary>
        public override string ToString()
        {
            if (_reader != null)
            {
                if (_reader.IsClosed) return "Reader Closed";
                else return "Open";
            }
            else
            {
                if (_connection != null)
                {
                    return "Conn " + _connection.State.ToString();
                }
            }
            return "Closed";
        }




        /// <summary>
        /// The protected base class constructor.
        /// </summary>
        protected Sql()
        {
            lock (ActiveInstances)
            {
                ActiveInstances.Add(this);
            }
        }
        ~Sql()
        {
            CleanUp();
        }
        public void Dispose()
        {
            CleanUp();
            lock (ActiveInstances)
            {
                ActiveInstances.Remove(this);
            }
        }
        /// <summary>
        /// General purpose method to get rid of any objects
        /// that may be open.
        /// </summary>
        private void CleanUp()
        {
            if (_reader != null)
            {
                if (!_reader.IsClosed) _reader.Close();
                _reader.Dispose();
                _reader = null;
            }
            if (_command != null) _command.Dispose();
            if (_connection != null)
            {
                _connection.Dispose();
                _connection = null;
            }
        }
        /// <summary>
        /// Opens the database connection.  If the connection fails
        /// the connection is disposed of and an exception will be 
        /// stored in the _exception property.
        /// </summary>
        /// <returns>true if the connection is opened</returns>
        protected bool OpenConnection()
        {
            _messages.Clear();

            if (_connection == null)
                _connection = new SqlConnection(ConnectionString);

            if (_connection.State != System.Data.ConnectionState.Open)
            {
                try { _connection.Open(); }
                catch (Exception ex)
                {
                    _exception = ex;
                    _connection.Dispose();
                    _connection = null;
                    return false;
                }
                // handle incoming messages
                _connection.InfoMessage += new SqlInfoMessageEventHandler(_connection_InfoMessage);
            }
            return true;
        }
        /// <summary>
        /// Accumulates messages and adds them to the _messages list.
        /// </summary>
        private void _connection_InfoMessage(object sender, SqlInfoMessageEventArgs e)
        {
            _messages.Add(e.Message);
        }







        // EXECUTING COMMANDS AND READERS --------------------
        protected void Open(string query)
        {
            if (!OpenConnection())
            {
                Exception ex = _exception;
                throw ex;
            }
            _command = new SqlCommand(query, _connection);
            try { _reader = _command.ExecuteReader(); }
            catch (Exception ex)
            {
                CleanUp();
                throw ex;
            }
        }
        protected string[] Execute(string query)
        {
            if (!OpenConnection())
            {
                Exception ex = _exception;
                throw ex;
            }
            _command = new SqlCommand(query, _connection);
            try { _command.ExecuteNonQuery(); }
            catch (Exception ex)
            {
                CleanUp();
                throw ex;
            }
            string[] messages = _messages.ToArray();
            CleanUp();
            return messages;
        }





        // NONSTATIC DATA METHODS ----------------------------
        /// <summary>
        /// Exchanges the currently open recordset for a
        /// new one based on the SQL query passed
        /// </summary>
        /// <param name="query">The source for the new recordset</param>
        public void SwapRecords(string query)
        {
            Close();
            if (_command == null) _command = new SqlCommand(query, _connection);
            else
            {
                _command.CommandText = query;
            }
            _reader = _command.ExecuteReader();
        }
        /// <summary>
        /// Closes and disposes of any open recordsets but keeps the
        /// connection open
        /// </summary>
        public void Close()
        {
            if (_reader != null)
            {
                if (!_reader.IsClosed) _reader.Close();
                Reader.Dispose();
                _reader = null;
            }
        }







        // READING DATA --------------------------------------
        public bool HasRows
        {
            get
            {
                EnsureDBValid();
                return _reader.HasRows;
            }
        }
        public object this[int index]
        {
            get
            {
                EnsureDBValid();
                try
                {
                    object result = _reader[index];
                    return result;
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }
        public object this[string name]
        {
            get
            {
                EnsureDBValid();
                try
                {
                    object result = _reader[name];
                    return result;
                }
                catch (Exception ex)
                {
                    throw ex;
                }
            }
        }
        public bool Read()
        {
            EnsureDBValid();
            try
            {
                bool result = _reader.Read();
                return result;
            }
            catch (Exception ex)
            {
                throw ex;
            }
        }

        // DATA ABOUT RESULTS --------------------------------
        public string GetColumnName(int index)
        {
            EnsureDBValid();
            return _reader.GetName(index);
        }
        public int ColumnCount
        {
            get
            {
                EnsureDBValid();
                return _reader.FieldCount;
            }
        }


        // CONVENIENCE ---------------------------------------
        // The GetDateTime() methods below don't return SQL min/max
        // they return DateTime min/max values.  These are here
        // just in case we need them for something else
        public static DateTime MinDate
        {
            get { return (DateTime)SqlDateTime.MinValue; }
        }
        public static DateTime MaxDate
        {
            get { return (DateTime)SqlDateTime.MaxValue; }
        }




        // UTILITIES -----------------------------------------
        private void EnsureDBValid()
        {
            if (_reader == null)
                throw new InvalidOperationException("There are no records available.");
            if (_reader.IsClosed)
                throw new InvalidOperationException("The record set is closed");
        }


        public bool IsNull(string column)
        {
            if (_reader == null) return true;
            return _reader[column] == DBNull.Value;
        }
        public bool IsNull(int columnIndex)
        {
            if (_reader == null) return true;
            return _reader[columnIndex] == DBNull.Value;
        }

        public string GetString(int column)  // contains an experiment to break down possible failures
        {
            EnsureDBValid();
            try { return _reader[column].ToString(); }
            catch { return ""; }
        }
        public string GetString(string columnName)
        {
            EnsureDBValid();
            try { return _reader[columnName].ToString(); }
            catch { return ""; }
        }
        public int GetInt(int column)
        {
            EnsureDBValid();
            try { return (int)_reader[column]; }
            catch { return 0; }
        }
        public int GetInt(string columnName)
        {
            EnsureDBValid();
            // Ran into an issue.  Sometimes we get an error "cannot unbox _reader[columnName] as int
            //try { return (int)_reader[columnName]; }
            try { return int.Parse(_reader[columnName].ToString()); }
            catch { return 0; }
        }
        public decimal GetDecimal(int column)
        {
            EnsureDBValid();
            try { return (decimal)_reader[column]; }
            catch { return 0; }
        }
        public decimal GetDecimal(string columnName)
        {
            EnsureDBValid();
            try { return (decimal)_reader[columnName]; }
            catch { return 0; }
        }
        public double GetDouble(int column)
        {
            EnsureDBValid();
            try { return (double)_reader[column]; }
            catch { return 0.0; }
        }
        public double GetDouble(string columnName)
        {
            EnsureDBValid();
            try { return (double)_reader[columnName]; }
            catch { return 0.0; }
        }
        public bool GetBoolean(int column)
        {
            EnsureDBValid();
            try { return (bool)_reader[column]; }
            catch { return false; }
        }
        public bool GetBoolean(string columnName)
        {
            EnsureDBValid();
            try { return (bool)_reader[columnName]; }
            catch { return false; }
        }
        public DateTime GetDateTime(int column)
        {
            EnsureDBValid();
            DateTime retVal = DateTime.Now;
            try { retVal = DateTime.Parse(_reader[column].ToString()); }
            catch { return DateTime.MinValue; }
            if (retVal == (DateTime)SqlDateTime.MinValue) return DateTime.MinValue;
            if (retVal == (DateTime)SqlDateTime.MaxValue) return DateTime.MaxValue;
            return retVal;
        }
        public DateTime GetDateTime(string columnName)
        {
            EnsureDBValid();
            DateTime retVal = DateTime.Now;
            try { retVal = DateTime.Parse(_reader[columnName].ToString()); }
            catch { return DateTime.MinValue; }
            if (retVal == (DateTime)SqlDateTime.MinValue) return DateTime.MinValue;
            if (retVal == (DateTime)SqlDateTime.MaxValue) return DateTime.MaxValue;
            return retVal;
        }

        public static string SqlQualify(int value) { return value.ToString(); }
        public static string SqlQualify(double value) { return value.ToString(); }
        public static string SqlQualify(float value) { return value.ToString(); }
        public static string SqlQualify(string value)
        {
            if (value == null) return "NULL";
            string sqlStr = "N'";
            sqlStr += value.Replace("'", "''");
            sqlStr += "'";
            return sqlStr;
        }
        public static string SqlQualify(bool value)
        {
            if (value) return "1";
            else return "0";
        }
        public static string SqlQualify(DateTime value)
        {
            DateTime safeDate = value;
            if (value < (DateTime)SqlDateTime.MinValue)
                safeDate = (DateTime)SqlDateTime.MinValue;
            if (value > (DateTime)SqlDateTime.MaxValue)
                safeDate = (DateTime)SqlDateTime.MaxValue;

            //ex. CONVERT(DATETIME, '1987-12-21 15:15:12', 102)
            string sqlStr = "";
            sqlStr += "CONVERT(DATETIME, '";
            sqlStr += safeDate.Year.ToString() + "-";
            sqlStr += safeDate.Month.ToString() + "-";
            sqlStr += safeDate.Day.ToString() + " ";
            sqlStr += safeDate.Hour.ToString() + ":";
            sqlStr += safeDate.Minute.ToString() + ":";
            sqlStr += safeDate.Second.ToString() + "', 102)";

            return sqlStr;
        }

        /// <summary>
        /// Under certain circumstances such as an optional foreign key
        /// it may be preferable to use dbNull instead
        /// of putting zero into an integer column.
        /// </summary>
        /// <param name="value">the int value to qualify</param>
        /// <param name="nullIfZero">substitute null instead of zero</param>
        /// <returns>string</returns>
        public static string SqlQualify(int value, bool nullIfZero)
        {
            if (value == 0) return "NULL";
            else return value.ToString();
        }

        public static string SqlQualify(object value)
        {
            if (value == null) return "NULL";
            Type valueType = value.GetType();
            if (valueType == typeof(string)) return SqlQualify((string)value);
            if (valueType == typeof(bool)) return SqlQualify((bool)value);
            if (valueType == typeof(DateTime)) return SqlQualify((DateTime)value);

            // for anything else, it has to be numeric or you have to pass it in already qualified
            return value.ToString();
        }
    }
}
