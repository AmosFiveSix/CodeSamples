using System;
using System.Security.Cryptography.X509Certificates;

namespace ABCEnterpriseLibrary
{
	public static class NetHelper
	{
		public static bool IsSelfSignedCertificate(X509Certificate certificate, X509Chain chain)
		{
			// Based on http://msdn.microsoft.com/en-us/library/dd633677%28v=exchg.80%29.aspx

			if(chain != null && chain.ChainStatus != null)
			{
				foreach(X509ChainStatus status in chain.ChainStatus)
				{
					if((certificate.Subject == certificate.Issuer) && (status.Status == X509ChainStatusFlags.UntrustedRoot))
					{
						// Self-signed certificates with an untrusted root are valid. 

						continue;
					}
					else
					{
						if(status.Status != X509ChainStatusFlags.NoError)
						{
							// If there are any other errors in the certificate chain, the certificate is invalid, so the method returns false.

							return false;
						}
					}
				}
			}

			// When processing reaches this line, the only errors in the certificate chain are untrusted root errors for self-signed certificates.

			return true;
		}
	}
}
