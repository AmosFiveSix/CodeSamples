using System;

namespace ABCEnterpriseLibrary
{
	public enum AllowCertificateType
	{
		/// <summary>
		/// Only allows valid certificate from a trusted root.
		/// </summary>
		Valid = 1,

		/// <summary>
		/// Allows valid certificates from a trusted root and alows self-signed certificates.
		/// </summary>
		ValidAndSelfSigned = 2,

		/// <summary>
		/// Allows all certificate, even invalid ones.
		/// </summary>
		All = 3,
	}
}
