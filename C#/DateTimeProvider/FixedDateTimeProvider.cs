using System;

namespace ABCEnterpriseLibrary
{
	/// <summary>
	/// A Date/Time provider that always returns the same date/time. Useful for testing purposes.
	/// </summary>
	public sealed class FixedDateTimeProvider : IDateTimeProvider
	{
		private readonly DateTime localDateTime;

		private readonly DateTime utcDateTime;

		public FixedDateTimeProvider(DateTime dateTime)
		{
			if(dateTime.Kind == DateTimeKind.Utc)
			{
				this.utcDateTime = dateTime;

				this.localDateTime = this.utcDateTime.ToLocalTime();
			}
			else if(dateTime.Kind == DateTimeKind.Local)
			{
				this.localDateTime = dateTime;

				this.utcDateTime = this.localDateTime.ToUniversalTime();
			}
			else
			{
				throw new ArgumentException("The supplied dateTime must be DateTimeKind.Local or DateTimeKind.Utc.");
			}
		}

		public DateTime Now
		{
			get
			{
				return this.localDateTime;
			}
		}

		public DateTime UtcNow
		{
			get
			{
				return this.utcDateTime;
			}
		}
	}
}
