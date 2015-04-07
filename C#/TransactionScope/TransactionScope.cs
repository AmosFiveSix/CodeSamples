using System;
using System.Data;
using System.Data.SqlClient;
using System.Threading;

// This implements something similar to .NET's TransactionScope. I needed a way to put SQL commands spread out across
// legacy code into a database transaction. The cost of modifying the legacy code to pass around a transaction was
// too high. I could not use .NET's TransactionScope because as soon as a second connection was opened (which happened
// all the time with the separate database calls in the legacy code) it needed to use MS Distributed Transaction
// Coordinator, which has to be installed and configured on the servers. I wanted to avoid that extra installation
// and support burden, so I wrote my own version of TransactionScope. It works in tandem with the DatabaseConnection
// class.

namespace ABCEnterpriseLibrary
{
	/// <summary>
	/// Provides additional options for creating a transaction scope.
	/// </summary>
	public enum TransactionScopeOption
	{
		/// <summary>
		/// A transaction is required by the scope. It uses an ambient transaction if one already exists. Otherwise, it creates a new transaction before entering the scope. This is the default value.
		/// </summary>
		Required = 0,

		/// <summary>
		/// A new transaction is always created for the scope. This is for cases where the contained code block does require a transaction for its consistency, and provides 
		/// a feature that demands that it be separate from any transaction that might already be active.  One typical example would be a function that provides activity 
		/// logging to, say, a database.  It may be implemented such that it required a transaction to provide consistency, but it couldn't accept an outer rollback to undo
		/// the record of the attempted activity.
		/// </summary>
		RequiresNew = 1,

		/// <summary>
		/// The ambient transaction context is suppressed when creating the scope. All operations within the scope are done without an ambient transaction context.
		/// </summary>
		Suppress = 2
	}

	/// <summary>
	/// Makes database access in a code block transactional. This class cannot be inherited.
	/// </summary>
	/// <remarks>
	/// This class works in a similar manner to the TransactionScope class in the .NET Framework System.Transactions namespace.
	/// </remarks>
	public sealed class TransactionScope : IDisposable
	{
		// Some useful resources on how this class works:

		// Transaction Isolation Levels
		// http://wiki/display/ABC/Transaction+Isolation+Levels

		// MSDN: TransactionScope Class
		// http://msdn.microsoft.com/en-us/library/system.transactions.transactionscope.aspx

		// System.Transactions.TransactionScope in .Net 
		// http://www.amosfivesix.com/blog/23-net/109-systemtransactionstransactionscope-in-net

		// ThreadStatic and Thread Local Storage in .Net 
		// http://www.amosfivesix.com/blog/23-net/158-threadstatic-and-thread-local-storage-in-net

		// Concerning the timeout period.  Yes, the connect timeout from connection string is used.  I verified this by looking at source code, see SqlInternalConnection.ExecuteTransactionYukon 
		// http://social.msdn.microsoft.com/forums/en-US/adodotnetdataproviders/thread/d9a98276-e201-4f38-abda-e4044df668c1/

		// The problem is, depending on the work having been done by the various commands, and the state of the server and network when you finally call commit, 
		// the commit itself may take some amount of time (transaction log management, or a temporary network issue, whatever)   So it can happen that commit
		// takes longer than whatever the ConnectionTimeout value is.  Typically you'd want this to be small - 15 seconds is the default if you don't specify 
		// otherwise. The thing that is killing me here is that should your commit take more than X seconds, you will receive a SqlException from the commit call, 
		// even though the underlying transaction in the server has succeeded, or may eventually succeed, and your data will be committed. See, the timeout is
		// between the client and server "connection", not the database operation itself.
		// http://social.msdn.microsoft.com/forums/en-US/adodotnetdataproviders/thread/26d54516-74b7-4a54-b879-85536e4b605f/

		#region -- Static Members --

		[ThreadStatic]
		private static TransactionScope current;

		internal static TransactionScope GetCurrent(string connectionString)
		{
			// We start with the scope at the bottom of the chain and then walk the chain back towards the top until we find a matching connection string or we reach the top.

			TransactionScope scope = TransactionScope.current;

			while(scope != null)
			{
				if(scope.connectionString.Equals(connectionString, StringComparison.OrdinalIgnoreCase))
				{
					break;
				}

				scope = scope.previous;
			}

			return scope;
		}

		internal static bool GetCurrentConnection(string connectionString, out SqlConnection connection, out SqlTransaction transaction)
		{
			TransactionScope scope = TransactionScope.GetCurrent(connectionString);

			if((scope != null) && (scope.HasTransaction))
			{
				connection = scope.currentConnection.SqlConnection;

				transaction = scope.currentConnection.SqlTransaction;

				return true;
			}
			else
			{
				connection = null;

				transaction = null;

				return false;
			}
		}

		#endregion

		#region -- Instance Members --

		private TransactionScope previous;			// A link to the previous/parent/upper scope. This becomes the current scope when we're disposed of.

		private Thread thread;						// The thread this scope was created on, so we can ensure we're disposed of on the same thread.

		private Connection currentConnection;		// The connection and transaction that this scope is using. May have been created by an earlier scope.

		private Connection createdConnection;		// If this scope needed to create a new connection and transaction, this is it. Otherwise this is null.

		private string connectionString;			// The connection string that this scope is for.

		private bool complete = false;				// Has the user called the Complete() method?

		private bool disposed = false;				// Has the user called the Dispose() method?

		/// <summary>
		/// Creates a new <see cref="TransactionScope"/> for the given <paramref name="database">database</paramref>.
		/// </summary>
		/// <param name="database">The connection string or database connection profile name.</param>
		/// <exception cref="ArgumentException">The supplied <paramref name="database"/> is an empty string, or no database connection profile exists with the given name, or the database connection profile has an invalid property.</exception>
		/// <exception cref="ArgumentNullException">The supplied <paramref name="database"/> is null.</exception>
		/// <exception cref="ArgumentOutOfRangeException">Invalid <see cref="TransactionScopeOption"/>.</exception>
		/// <exception cref="InvalidOperationException">The server name or database name was not specified in the database connection profile, or an invalid attempt was made to use parallel transactions.</exception>
		/// <exception cref="System.Data.SqlClient.SqlException">A connection-level error occurred while opening the connection, or an invalid attempt was made to use parallel transactions when using Multiple Active Result Sets (MARS).</exception>
		public TransactionScope(string database) : this(database, TransactionScopeOption.Required, IsolationLevel.ReadCommitted)
		{
			//
		}

		/// <summary>
		/// Creates a new <see cref="TransactionScope"/> for the given <paramref name="database">database</paramref>.
		/// </summary>
		/// <param name="database">The connection string or database connection profile name.</param>
		/// <param name="option">An instance of the <see cref="TransactionScopeOption"/> enumeration that describes the transaction requirements associated with this transaction scope.</param>
		/// <exception cref="ArgumentException">The supplied <paramref name="database"/> is an empty string, or no database connection profile exists with the given name, or the database connection profile has an invalid property.</exception>
		/// <exception cref="ArgumentNullException">The supplied <paramref name="database"/> is null.</exception>
		/// <exception cref="ArgumentOutOfRangeException">Invalid <see cref="TransactionScopeOption"/>.</exception>
		/// <exception cref="InvalidOperationException">The server name or database name was not specified in the database connection profile, or an invalid attempt was made to use parallel transactions.</exception>
		/// <exception cref="System.Data.SqlClient.SqlException">A connection-level error occurred while opening the connection, or an invalid attempt was made to use parallel transactions when using Multiple Active Result Sets (MARS).</exception>
		public TransactionScope(string database, TransactionScopeOption option) : this(database, option, IsolationLevel.ReadCommitted)
		{
			//
		}

		/// <summary>
		/// Creates a new <see cref="TransactionScope"/> for the given <paramref name="database">database</paramref>.
		/// </summary>
		/// <param name="database">The connection string or database connection profile name.</param>
		/// <param name="option">An instance of the <see cref="TransactionScopeOption"/> enumeration that describes the transaction requirements associated with this transaction scope.</param>
		/// <param name="isolation">Specifies the isolation level for the transaction.</param>
		/// <exception cref="ArgumentException">The supplied <paramref name="database"/> is null or an empty string, or no database connection profile exists with the given name, or the database connection profile has an invalid property.</exception>
		/// <exception cref="ArgumentException">The current TransactionScope has a different IsolationLevel than the value requested for the new TransactionScope.</exception>
		/// <exception cref="ArgumentOutOfRangeException">Invalid <see cref="TransactionScopeOption"/>.</exception>
		/// <exception cref="InvalidOperationException">The server name or database name was not specified in the database connection profile, or an invalid attempt was made to use parallel transactions.</exception>
		/// <exception cref="System.Data.SqlClient.SqlException">A connection-level error occurred while opening the connection, or an invalid attempt was made to use parallel transactions when using Multiple Active Result Sets (MARS).</exception>
		public TransactionScope(string database, TransactionScopeOption option, IsolationLevel isolation)
		{
			ArgumentHelper.AssertNotNullOrEmpty("database", database);

			this.connectionString = DatabaseConnectionProfile.GetConnectionString(database);

			switch(option)
			{
				case TransactionScopeOption.Required:

					TransactionScope current = TransactionScope.GetCurrent(this.connectionString);

					if((current != null) && (current.HasTransaction))
					{
						UseExistingConnection(current, isolation);			// We need to use the existing connection and transaction.
					}
					else
					{
						CreateConnection(true, isolation);					// We need to create a new connection and transaction.
					}

					break;

				case TransactionScopeOption.RequiresNew:

					CreateConnection(true, isolation);						// We need to create a new connection and transaction.

					break;

				case TransactionScopeOption.Suppress:

					CreateConnection(false, IsolationLevel.Unspecified);	// We need to create a new connection without a transaction.

					break;

				default:

					throw new ArgumentOutOfRangeException("option");
			}
		}

		private void UseExistingConnection(TransactionScope existing, IsolationLevel isolation)
		{
			if((isolation != IsolationLevel.Unspecified) && (isolation != existing.currentConnection.IsolationLevel))
			{
				throw new ArgumentException("The current TransactionScope has a different IsolationLevel than the value requested for the new TransactionScope.", "isolation");
			}

			this.createdConnection = null;

			this.currentConnection = existing.currentConnection;

			this.ChainScope();
		}

		private void CreateConnection(bool useTransaction, IsolationLevel isolation)
		{
			try
			{
				this.createdConnection = new Connection(this.connectionString, useTransaction, isolation);

				this.currentConnection = this.createdConnection;

				this.ChainScope();
			}
			catch
			{
				if(this.createdConnection != null)
				{
					this.createdConnection.Dispose();

					this.createdConnection = null;
				}

				throw;
			}
		}

		private bool HasTransaction
		{
			get
			{
				return this.currentConnection.HasTransaction;
			}
		}

		/// <summary>
		/// Marks the transaction for the TransactionScope as complete. It will be committed when the TransactionScope if disposed.
		/// </summary>
		/// <exception cref="ObjectDisposedException">The TransactionScope has already been disposed.</exception>
		/// <exception cref="InvalidOperationException">The current TransactionScope is already complete. You should dispose the TransactionScope.</exception>
		public void Complete()
		{
			if(this.disposed)
			{
				throw new ObjectDisposedException("TransactionScope");
			}

			if(this.complete)
			{
				throw new InvalidOperationException("The current TransactionScope is already complete. You should dispose the TransactionScope.");
			}

			this.complete = true;
		}

		/// <summary>
		/// Commits or rolls back the transaction for the TransactionScope.
		/// </summary>
		/// <exception cref="InvalidOperationException">The TransactionScope was disposed on the wrong thread or out of order, or the database connection is broken, or The transaction for this TransactionScope has already been rolled back by an inner TransactionScope, or the transaction has already been committed, or the transaction cannot be rolled back because it has already been committed.</exception>
		public void Dispose()
		{
			if(!this.disposed)
			{
				this.AssertScope();					// Make sure the scopes are being disposed in the correct order.

				this.disposed = true;

				try
				{
					this.UnchainScope();			// Remove this scope from the bottom of the scope chain.

					if(this.currentConnection.HasTransaction)
					{
						if(this.complete)
						{
							if(this.createdConnection != null)
							{
								// We created the transaction so now we can commit the transaction. This will throw
								// an exception if any inner scopes failed to complete.

								this.createdConnection.Commit();
							}
							else
							{
								// We're using a transaction created by a parent scope. We don't need to do anything
								// because the parent scope needs to be completed before a commit happens.
							}
						}
						else
						{
							// If user did not set complete, then we need to rollback the transaction, even if this scope didn't create the transaction.

							this.currentConnection.Rollback();
						}
					}
				}
				finally
				{
					if(this.createdConnection != null)
					{
						this.createdConnection.Dispose();
					}
				}
			}
		}

		private void AssertScope()
		{
			// Normally you don't want to throw exceptions from Dispose(), but I REALLY want to know if these happen.

			if(this.thread != Thread.CurrentThread)
			{
				throw new InvalidOperationException("Scope disposed on wrong thread.");
			}

			if(this != TransactionScope.current)
			{
				throw new InvalidOperationException("Scope disposed out of order.");
			}
		}

		private void ChainScope()
		{
			Thread.BeginThreadAffinity();

			this.thread = Thread.CurrentThread;

			this.previous = TransactionScope.current;

			TransactionScope.current = this;
		}

		private void UnchainScope()
		{
			TransactionScope.current = this.previous;

			Thread.EndThreadAffinity();
		}

		#endregion

		#region -- Private Classes --

		private class Connection : IDisposable
		{
			private enum TransactionState
			{
				None,
				Active,
				Aborted,
				Committed
			}

			private SqlConnection connection;			// The actual database connection that the transaction is using.

			private SqlTransaction transaction;			// The actual database transaction that the transaction is using. May be null if we're not using a transaction with this connection.

			private TransactionState state;

			public Connection(string connectionString, bool useTransaction, IsolationLevel isolation)
			{
				try
				{
					this.connection = new SqlConnection(connectionString);

					this.connection.Open();

					if(useTransaction)
					{
						this.transaction = this.connection.BeginTransaction(isolation);

						this.state = TransactionState.Active;
					}
					else
					{
						this.transaction = null;

						this.state = TransactionState.None;
					}
				}
				catch
				{
					if(this.transaction != null)
					{
						this.transaction.Dispose();
					}

					if(this.connection != null)

					{
						this.connection.Dispose();
					}

					throw;
				}
			}

			public SqlConnection SqlConnection
			{
				get { return this.connection; }
			}

			public SqlTransaction SqlTransaction
			{
				get { return this.transaction; }
			}

			public bool HasTransaction
			{
				get
				{
					return this.transaction != null;
				}
			}

			public IsolationLevel IsolationLevel
			{
				get
				{
					if(this.transaction != null)
					{
						return this.transaction.IsolationLevel;
					}
					else
					{
						return IsolationLevel.Unspecified;
					}
				}
			}

			public void Commit()
			{
				switch(this.state)
				{
					case TransactionState.Active:

						try
						{
							this.transaction.Commit();
						}
						finally
						{
							this.connection.Close();

							this.state = TransactionState.Committed;
						}

						break;

					case TransactionState.Aborted:

						throw new InvalidOperationException("The transaction for this TransactionScope has already been rolled back by an inner TransactionScope.");

					case TransactionState.Committed:

						// This should only happen if there is a bug in our code.

						throw new InvalidOperationException("The transaction has already been committed.");

					default:

						// We don't have a transaction so do nothing.

						break;
				}
			}

			public void Rollback()
			{
				switch(this.state)
				{
					case TransactionState.Active:

						try
						{
							this.transaction.Rollback();
						}
						finally
						{
							this.connection.Close();

							this.state = TransactionState.Aborted;
						}

						break;

					case TransactionState.Aborted:

						// We've already been rolled back so we just ignore another attempt to rollback.

						break;

					case TransactionState.Committed:

						// This should only happen if there is a bug in our code.

						throw new InvalidOperationException("The transaction cannot be rolled back because it has already been committed.");

					default:

						// We don't have a transaction so do nothing.

						break;
				}
			}

			public void Dispose()
			{
				// Since this class is sealed we don't need a protected Dispose(bool) method. Since we don't have any unmanaged resources we don't need a finalizer.

				if(this.transaction != null)
				{
					this.transaction.Dispose();
				}

				if(this.connection != null)
				{
					this.connection.Dispose();
				}
			}
		}

		#endregion
	}
}
