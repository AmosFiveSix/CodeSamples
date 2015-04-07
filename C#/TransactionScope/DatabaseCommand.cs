using System;
using System.Collections;
using System.Data;
using System.Data.SqlClient;
using System.Globalization;
using System.Xml;

namespace ABCEnterpriseLibrary
{
	/// <summary>
	/// Represents a SQL statement or stored procedure to execute against a SQL Server database. This class cannot be inherited.
	/// </summary>
	public sealed class DatabaseCommand : IDisposable
	{
		private SqlCommand command = null;

		private bool disposed = false;

		#region -- Constructors --

		internal DatabaseCommand(SqlConnection connection, SqlTransaction transaction, CommandType type, string text, int timeout, IDictionary parameters, DatabaseReadableEntity parametersEntity)
		{
			try
			{
				this.command = connection.CreateCommand();

				this.command.CommandType = type;

				this.command.CommandText = text;

				this.command.CommandTimeout = timeout;

				if((type == CommandType.Text) && (parametersEntity != null))
				{
					DataHelper.EntityToParameters(this.command, text, parametersEntity);
				}

				if(parameters != null)
				{
					DataHelper.DictionaryToParameters(this.command, parameters);
				}

				if(transaction != null)
				{
					this.command.Transaction = transaction;
				}
			}
			catch
			{
				if(this.command != null)
				{
					this.command.Dispose();
				}

				throw;
			}
		}

		#endregion

		#region -- Public Properties --

		/// <summary>
		/// Gets or sets the Transact-SQL statement, table name or stored procedure to execute at the data source.
		/// </summary>
		public string CommandText
		{
			get
			{
				AssertNotDisposed();

				return this.command.CommandText;                
			}

			set
			{
				AssertNotDisposed();

				this.command.CommandText = value;
			}
		}

		/// <summary>
		/// Gets or sets the wait time before terminating the attempt to execute a command and generating an error.
		/// </summary>
		public int CommandTimeout
		{
			get
			{
				AssertNotDisposed();

				return this.command.CommandTimeout;
			}

			set
			{
				AssertNotDisposed();

				this.command.CommandTimeout = value;
			}
		}

		/// <summary>
		/// Gets or sets a value indicating how the <see cref="CommandText"/> property is to be interpreted.
		/// </summary>
		public CommandType CommandType
		{
			get
			{
				AssertNotDisposed();

				return this.command.CommandType;
			}

			set
			{
				AssertNotDisposed();

				this.command.CommandType = value;
			}
		}

		/// <summary>
		/// Gets the SQL parameters collection.
		/// </summary>
		public SqlParameterCollection Parameters
		{
			get
			{
				AssertNotDisposed();

				return this.command.Parameters;
			}
		}

		/// <summary>
		/// Gets the internal SqlCommand used by the DatabaseCommand.
		/// </summary>
		public SqlCommand SqlCommand
		{
			get
			{
				AssertNotDisposed();

				return this.command;
			}
		}

		#endregion

		#region -- Public Methods --

		#region -- Synchronous Methods --

		/// <summary>
		/// Executes a Transact-SQL statement against the connection and returns the number of rows affected.
		/// </summary>
		/// <returns>The number of rows affected.</returns>
		public int ExecuteNonQuery()
		{
			AssertNotDisposed();

			return this.command.ExecuteNonQuery();
		}

		/// <summary>
		/// Sends the CommandText to the connection and builds a <see cref="DatabaseReader"/>.
		/// </summary>
		/// <returns>A <see cref="DatabaseReader"/> object. You must dispose of the DatabaseReader when you are done with it.</returns>
		public DatabaseReader ExecuteReader()
		{
			return this.ExecuteReader(CommandBehavior.Default);
		}

		/// <summary>
		/// Sends the CommandText to the connection and builds a <see cref="DatabaseReader"/>.
		/// </summary>
		/// <param name="behavior">One of the CommandBehavior values.</param>
		/// <returns>A <see cref="DatabaseReader"/> object. You must dispose of the DatabaseReader when you are done with it.</returns>
		public DatabaseReader ExecuteReader(CommandBehavior behavior)
		{
			SqlDataReader reader = null;

			AssertNotDisposed();

			try
			{
				reader = this.command.ExecuteReader(behavior);

				return new DatabaseReader(reader);
			}
			catch
			{
				if(reader != null)
				{
					reader.Dispose();
				}

				throw;
			}
		}

		/// <summary>
		/// Executes the query, and returns the first column of the first row in the result set returned by the query. Additional columns or rows are ignored.
		/// </summary>
		/// <returns>The first column of the first row in the result set, or a null reference if the result set is empty. Returns a maximum of 2033 characters.</returns>
		public object ExecuteScalar()
		{
			AssertNotDisposed();

			return this.command.ExecuteScalar();
		}

		/// <summary>
		/// Sends the CommandText to the connection and builds a DataTable containing an in-memory copy of the results of the command. If tableName is ommitted, "DataTable1" is used.
		/// </summary>
		/// <returns>Returns a DataTable object containing all of the data read from the results of the command. You must dispose of the DataTable when you are done with it.</returns>
		public DataTable ExecuteTable()
		{
			return this.ExecuteTable("DataTable1");
		}

		/// <summary>
		/// Sends the CommandText to the connection and builds a DataTable containing an in-memory copy of the results of the command.
		/// </summary>
		/// <param name="tableName">The name to give the table. If tableName is null or an empty string, a default name is used.</param>
		/// <returns>Returns a DataTable object containing all of the data read from the results of the command. You must dispose of the DataTable when you are done with it.</returns>
		public DataTable ExecuteTable(string tableName)
		{
			DataTable table = null;

			AssertNotDisposed();

			try
			{
				table = new DataTable(tableName);

				table.Locale = CultureInfo.InvariantCulture;	// See http://msdn.microsoft.com/library/ms182188%28VS.100%29.aspx

				using(SqlDataReader reader = this.command.ExecuteReader())
				{
					table.Load(reader);
				}

				return table;
			}
			catch
			{
				if(table != null)
				{
					table.Dispose();
				}

				throw;
			}
		}

		/// <summary>
		/// Sends the CommandText to the connection and builds an XML string containing the results.
		/// </summary>
		/// <param name="root">The tag name for the root element in the XML. All rows in the reader appear as root elements in the resulting XML. If there is more than one row in the reader this results in invalid XML.</param>
		/// <returns>An XML string.</returns>
		/// <remarks>
		/// The returns XML looks like this:
		/// <![CDATA[
		///		<root>
		///			<column1/>
		///			<column2/>
		///		</root>
		///		<root>
		///			<column1/>
		///			<column2/>
		///		</root>
		///	]]>
		/// </remarks>
		public string ExecuteXml(string root)
		{
			AssertNotDisposed();

			using(SqlDataReader reader = this.command.ExecuteReader())
			{
				return DataHelper.DataReaderToXml(reader, root);
			}
		}

		/// <summary>
		/// Sends the CommandText to the connection and builds an XML string containing the results.
		/// </summary>
		/// <param name="root">The tag name for the root element in the XML.</param>
		/// <param name="element">The tag name for each element below the root element.</param>
		/// <returns>An XML string.</returns>
		/// <remarks>
		/// The returned XML looks like this:
		/// <![CDATA[
		///		<root>
		///			<element>
		///				<column1/>
		///				<column2/>
		///			</elememt>
		///			<element>
		///				<column1/>
		///				<column2/>
		///			</elememt>
		///		</root>
		///	]]>
		/// </remarks>
		public string ExecuteXml(string root, string element)
		{
			AssertNotDisposed();

			using(SqlDataReader reader = this.command.ExecuteReader())
			{
				return DataHelper.DataReaderToXml(reader, root, element);
			}
		}

		/// <summary>
		/// Sends the CommandText to the Connection and builds an XmlReader object.
		/// </summary>
		/// <returns>An XmlReader object.</returns>
		public XmlReader ExecuteXmlReader()
		{
			AssertNotDisposed();

			return this.command.ExecuteXmlReader();
		}

		#endregion

		#region -- Parameter Methods --

		public IDataParameter AddParameter(string name, string value)
		{
			return this.AddParameter(name, DbType.AnsiString, ParameterDirection.Input, value);	// Note NOT NVarChar!
		}

		public IDataParameter AddParameter(string name, DateTime value)
		{
			return this.AddParameter(name, DbType.DateTime, ParameterDirection.Input, value);
		}

		public IDataParameter AddParameter(string name, bool value)
		{
			return this.AddParameter(name, DbType.Boolean, ParameterDirection.Input, value);
		}

		public IDataParameter AddParameter(string name, Decimal value)
		{
			return this.AddParameter(name, DbType.Currency, ParameterDirection.Input, value);
		}

		public IDataParameter AddParameter(string name, int value)
		{
			return this.AddParameter(name, DbType.Int32, ParameterDirection.Input, value);
		}

		public IDataParameter AddParameter(string name, DbType type, ParameterDirection direction)
		{
			return this.AddParameter(name, type, direction, null);
		}

		public IDataParameter AddOutputParameter(string name, DbType type)
		{
			return this.AddParameter(name, type, ParameterDirection.Output);
		}

		public IDataParameter AddReturnValueParameter(string name)
		{
			// In SQL Server return values are always INT.

			return this.AddParameter(name, DbType.Int32, ParameterDirection.ReturnValue);
		}

		public IDataParameter AddParameter(string name, DbType type, ParameterDirection direction, object value)
		{
			var parameter = new SqlParameter();

			parameter.ParameterName = name;

			parameter.DbType = type;

			parameter.Direction = direction;

			parameter.Value = value;

			this.command.Parameters.Add(parameter);

			return parameter;
		}

		public void AddParameters(IDictionary parameters)
		{
			DataHelper.DictionaryToParameters(this.command, parameters);
		}

		#endregion

		#endregion

		#region -- IDisposable Implementation --

		private void AssertNotDisposed()
		{
			if(this.disposed)
			{
				throw new ObjectDisposedException("DatabaseCommand");
			}
		}

		/// <summary>
		/// Disposes of the database command. You must call this method when you are done with the object.
		/// </summary>
		public void Dispose()
		{
			// Since this class is sealed, we don't need a protected Dispose(bool) method. Since we don't have any unmanaged object we don't need a finalizer.

			if(!this.disposed)
			{
				if(this.command != null)
				{
					this.command.Dispose();

					this.command = null;
				}

				this.disposed = true;
			}
		}

		#endregion
	}
}
