using System;

namespace ABCEnterpriseLibrary
{
	public interface IDateTimeProvider
	{
		DateTime Now { get;  }

		DateTime UtcNow { get;  }
	}
}
