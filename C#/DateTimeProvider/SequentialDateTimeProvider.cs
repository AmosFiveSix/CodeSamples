using System;

namespace ABCEnterpriseLibrary
{
	/// <summary>
	/// A thread safe time provider that returns a new DateTime each time it is called, each one a set amount in the future from the last one returned.
	/// </summary>
	public sealed class SequentialDateTimeProvider : IDateTimeProvider
	{
		private readonly object syncLock;

		private readonly TimeSpan increment;

		private DateTime current;

		public SequentialDateTimeProvider() : this(DateTime.Now, new TimeSpan(0, 0, 1))
		{
			//
		}

		public SequentialDateTimeProvider(TimeSpan increment) : this(DateTime.Now, increment)
		{
			//
		}

		public SequentialDateTimeProvider(DateTime start, TimeSpan increment)
		{
			this.syncLock = new object();

			this.current = start;

			this.increment = increment;

			if(start.Kind == DateTimeKind.Local)
			{
				this.current = start;
			}
			else if(start.Kind == DateTimeKind.Utc)
			{
				this.current = start.ToLocalTime();
			}
			else
			{
				throw new ArgumentException("The supplied start must be DateTimeKind.Local or DateTimeKind.Utc.");
			}
		}

		public DateTime Now
		{
			get
			{
				lock(this.syncLock)
				{
					DateTime result = this.current;

					this.current = this.current.Add(this.increment);

					return result;
				}
			}
		}

		public DateTime UtcNow
		{
			get
			{
				return this.Now.ToUniversalTime();
			}
		}
	}
}
