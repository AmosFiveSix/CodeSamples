using System;
using System.Net;

namespace ABCEnterpriseLibrary
{
	public class FtpUploadResponse
	{
		public FtpStatusCode StatusCode { get; set; }

		public string Description { get; set; }

		public string BannerMessage { get; set; }

		public string WelcomeMessage { get; set; }

		public string ExitMessage { get; set; }
	}
}
