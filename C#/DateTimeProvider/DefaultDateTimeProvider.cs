using System;

namespace ABCEnterpriseLibrary
{
	/// <summary>
	/// A time provider that returns the real current date and time.
	/// </summary>
	public sealed class DefaultDateTimeProvider : IDateTimeProvider
	{
		public DateTime Now
		{
			get
			{
				return DateTime.Now;
			}
		}

		public DateTime UtcNow
		{
			get
			{
				return DateTime.UtcNow;
			}
		}
	}
}
