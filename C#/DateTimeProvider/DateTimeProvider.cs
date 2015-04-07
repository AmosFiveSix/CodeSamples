using System;

namespace ABCEnterpriseLibrary
{
	/// <summary>
	/// Provides an ambient context for getting the current date/time. Allows you to change how the current date/time is calculated for testing purposes.
	/// </summary>
	public static class DateTimeProvider
	{
		private static IDateTimeProvider current;

		static DateTimeProvider()
		{
			DateTimeProvider.current = new DefaultDateTimeProvider();
		}

		/// <summary>
		/// Sets or returns the current time provider. Note that that setting the time provider is not thread safe and should be done before any threads are started. You would not normally set the current time provider in production code.
		/// </summary>
		public static IDateTimeProvider Current
		{
			get
			{
				return DateTimeProvider.current;
			}

			set
			{
				ArgumentHelper.AssertNotNull("value", value);

				DateTimeProvider.current = value;
			}
		}

		/// <summary>
		/// Resets the time provider to its default behavior. Note that this method is not thread safe and should only be called when no other threads are running. You would not normally call this method is production code.
		/// </summary>
		public static void Reset()
		{
			DateTimeProvider.current = new DefaultDateTimeProvider();
		}
	}
}
