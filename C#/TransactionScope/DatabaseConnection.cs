using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;

// See TransactionScope.cs for some notes. We work with TransactionScope to give us ambient transactions.

namespace ABCEnterpriseLibrary
{
	/// <summary>
	/// Represents a connection to a SQL Server database using a connection string or <see cref="DatabaseConnectionProfile"/>. This class cannot be inherited.
	/// </summary>
	public sealed class DatabaseConnection : IDisposable
	{
		#region -- Private member variables --

		private bool disposed = false;

		private SqlConnection connection = null;

		private SqlTransaction transaction = null;

		private bool ownsConnection = false;

		private readonly string connectionString;

		private readonly int commandTimeout;

		private List<string> messages = null;

		#endregion

		#region -- Constructors --

		/// <summary>
		/// Creates a new <see cref="DatabaseConnection"/> for the given <paramref name="database">connection string or database connection profile.</paramref>.
		/// </summary>
		/// <param name="database">The connection string or database connection profile name.</param>
		/// <exception cref="ArgumentException">The supplied <paramref name="database"/> is null or an empty string, or no database connection profile exists with the given name, or the database connection profile has an invalid property.</exception>
		/// <exception cref="InvalidOperationException">The server name or database name was not specified in the database connection profile or connection string.</exception>
		/// <exception cref="SqlException">A connection-level error occurred while opening the connection.</exception>
		public DatabaseConnection(string database)
		{
			ArgumentHelper.AssertNotNullOrEmpty("database", database);

			if(DatabaseConnectionProfile.IsConnectionString(database))
			{
				this.connectionString = database;

				this.commandTimeout = DatabaseConnectionProfile.DefaultCommandTimeout;
			}
			else
			{
				DatabaseConnectionProfile profile = new DatabaseConnectionProfile(database);
				{
					this.connectionString = profile.ConnectionString;

					this.commandTimeout = profile.CommandTimeout;
				}
			}

			this.Initialize();
		}

		/// <summary>
		/// Creates a new <see cref="DatabaseConnection"/> for the given <paramref name="profile">database connection profile</paramref>.
		/// </summary>
		/// <param name="profile">The <see cref="DatabaseConnectionProfile"/> to use for the connection.</param>
		/// <exception cref="ArgumentNullException">The supplied <paramref name="profile"/> is null.</exception>
		/// <exception cref="InvalidOperationException">The server name or database name was not specified in the database connection profile.</exception>
		/// <exception cref="SqlException">A connection-level error occurred while opening the connection.</exception>
		public DatabaseConnection(DatabaseConnectionProfile profile)
		{
			ArgumentHelper.AssertNotNull("profile", profile);

			this.connectionString = profile.ConnectionString;

			this.commandTimeout = profile.CommandTimeout;

			this.Initialize();
		}

		private void Initialize()
		{
			this.ownsConnection = false;

			if(!TransactionScope.GetCurrentConnection(this.connectionString, out this.connection, out this.transaction))
			{
				try
				{
					this.connection = new SqlConnection(this.connectionString);

					this.connection.Open();

					this.ownsConnection = true;
				}
				catch
				{
					if(this.connection != null)
					{
						this.connection.Dispose();
					}

					throw;
				}
			}
		}

		#endregion

		#region -- Public Properties --

		/// <summary>
		/// Returns the connection string used to make this connection.
		/// </summary>
		public string ConnectionString
		{
			get
			{
				AssertNotDisposed();

				return this.connectionString;
			}
		}

		/// <summary>
		/// Returns the default command timeout used when creating new commands with this connection.
		/// </summary>
		public int CommandTimeout
		{
			get
			{
				AssertNotDisposed();

				return this.commandTimeout;
			}
		}

		/// <summary>
		/// Determines whether messages from the SQL Server are saved in the <see cref="Messages"/> property.
		/// </summary>
		public bool SaveMessages
		{
			get
			{
				AssertNotDisposed();

				return messages != null;
			}

			set
			{
				AssertNotDisposed();

				if(value)
				{
					if(messages == null)
					{
						messages = new List<string>();

						connection.InfoMessage += new SqlInfoMessageEventHandler(connection_InfoMessage);
					}
				}
				else
				{
					if(messages != null)
					{
						messages = null;

						connection.InfoMessage -= connection_InfoMessage;
					}
				}
			}
		}

		/// <summary>
		/// The saved SQL Server messages. <see cref="SaveMessages"/> must be set to true.
		/// </summary>
		public ReadOnlyCollection<string> Messages
		{
			get
			{
				AssertNotDisposed();
				
				if(messages != null)
				{
					return new ReadOnlyCollection<string>(messages);
				}
				else
				{
					throw new InvalidOperationException("SaveMessages has not been set to true for this connection.");
				}
			}
		}

		#endregion

		#region -- Public Methods --

		/// <summary>
		/// Creates a new <see cref="DatabaseCommand"/>.
		/// </summary>
		/// <param name="sql">The full SQL statement to use for the command.</param>
		/// <returns>A new <see cref="DatabaseCommand"/>.</returns>
		public DatabaseCommand CreateCommand(string sql)
		{
			return CreateCommand(sql, null, null, CommandType.Text, this.commandTimeout);
		}

		/// <summary>
		/// Creates a new <see cref="DatabaseCommand"/>.
		/// </summary>
		/// <param name="sql">The SQL statement or stored procedure name.</param>
		/// <param name="type">The type of command to create.</param>
		/// <returns>A new <see cref="DatabaseCommand"/>.</returns>
		/// <exception cref="ObjectDisposedException">The DatabaseConnection has already been disposed.</exception>
		public DatabaseCommand CreateCommand(string sql, CommandType type)
		{
			return CreateCommand(sql, null, null, type, this.commandTimeout);
		}

		/// <summary>
		/// Creates a new <see cref="DatabaseCommand"/>.
		/// </summary>
		/// <param name="sql">The full SQL statement to use for the command.</param>
		/// <param name="parameters">Parameters to use with the supplied SQL. May be null.</param>
		/// <returns>A new <see cref="DatabaseCommand"/>.</returns>
		/// <exception cref="ObjectDisposedException">The DatabaseConnection has already been disposed.</exception>
		public DatabaseCommand CreateCommand(string sql, IDictionary parameters)
		{
			return CreateCommand(sql, parameters, null, CommandType.Text, this.commandTimeout);
		}

		/// <summary>
		/// Creates a new <see cref="DatabaseCommand"/>.
		/// </summary>
		/// <param name="sql">The full SQL statement to use for the command. The SQL may contain parameters matching the names of the supplied entity's properties. The current values of those properties will be passed as the parameter values.</param>
		/// <param name="parametersEntity">The entity from which to get parameter values. May be null</param>
		/// <returns>A new <see cref="DatabaseCommand"/>.</returns>
		/// <exception cref="ObjectDisposedException">The DatabaseConnection has already been disposed.</exception>
		public DatabaseCommand CreateCommand(string sql, DatabaseReadableEntity parametersEntity)
		{
			return CreateCommand(sql, null, parametersEntity, CommandType.Text, this.commandTimeout);
		}

		/// <summary>
		/// Creates a new <see cref="DatabaseCommand"/>.
		/// </summary>
		/// <param name="sql">The full SQL statement to use for the command. The SQL may contain parameters matching the names of the supplied entity's properties. The current values of those properties will be passed as the parameter values.</param>
		/// <param name="parameters">Additional parameters to use with the supplied SQL. May be null.</param>
		/// <param name="parametersEntity">The entity from which to get parameter values. May be null</param>
		/// <returns>A new <see cref="DatabaseCommand"/>.</returns>
		/// <exception cref="ObjectDisposedException">The DatabaseConnection has already been disposed.</exception>
		public DatabaseCommand CreateCommand(string sql, IDictionary parameters, DatabaseReadableEntity parametersEntity)
		{
			return CreateCommand(sql, parameters, parametersEntity, CommandType.Text, this.commandTimeout);
		}

		/// <summary>
		/// Creates a new <see cref="DatabaseCommand"/>.
		/// </summary>
		/// <param name="sql">The SQL statement or stored procedure name.</param>
		/// <param name="parameters">Parameters to use with the command. May be null.</param>
		/// <param name="type">The type of command to create.</param>
		/// <returns>A new <see cref="DatabaseCommand"/>.</returns>
		/// <exception cref="ObjectDisposedException">The DatabaseConnection has already been disposed.</exception>
		public DatabaseCommand CreateCommand(string sql, IDictionary parameters, CommandType type)
		{
			return CreateCommand(sql, parameters, null, type, this.commandTimeout);
		}

		/// <summary>
		/// Creates a new <see cref="DatabaseCommand"/>.
		/// </summary>
		/// <param name="sql">The SQL statement or stored procedure name.</param>
		/// <param name="parameters">Parameters to use with the command. May be null.</param>
		/// <param name="parametersEntity">The entity from which to get parameter values. May be null</param>
		/// <param name="type">The type of command to create.</param>
		/// <param name="timeout">The command timeout to use.</param>
		/// <returns>A new <see cref="DatabaseCommand"/>.</returns>
		/// <exception cref="ObjectDisposedException">The DatabaseConnection has already been disposed.</exception>
		public DatabaseCommand CreateCommand(string sql, IDictionary parameters, DatabaseReadableEntity parametersEntity, CommandType type, int timeout)
		{
			AssertNotDisposed();

			return new DatabaseCommand(this.connection, this.transaction, type, sql, timeout, parameters, parametersEntity);
		}

		#endregion

		#region -- Events --

		private void connection_InfoMessage(object sender, SqlInfoMessageEventArgs e)
		{
			messages.Add(e.Message);
		}

		#endregion

		#region -- Private Methods --

		private void AssertNotDisposed()
		{
			if(this.disposed)
			{
				throw new ObjectDisposedException("DatabaseConnection");
			}
		}

		#endregion

		#region -- IDisposable Implementation --

		/// <summary>
		/// Closes the database connection. You must call this method when you are done with the object.
		/// </summary>
		public void Dispose()
		{
			// Since this class is sealed we don't need a protected Dispose(bool) method. Since we don't have any unmanaged resources we don't need a finalizer.

			if(!this.disposed)
			{
				if(this.ownsConnection && (this.connection != null))
				{
					this.connection.Dispose();
				}

				this.transaction = null;

				this.connection = null;

				this.disposed = true;
			}
		}

		#endregion
	}
}
