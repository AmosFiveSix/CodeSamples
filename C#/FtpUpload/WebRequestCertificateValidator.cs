using System;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;

// See http://stackoverflow.com/questions/4156365/how-do-i-set-the-servercertificatevalidationcallback-property-back-to-its-defa
// See http://stackoverflow.com/questions/5225373/asking-sslstream-to-accept-only-a-certificate-signed-by-a-particular-public-key/5225530#5225530
// See http://stackoverflow.com/questions/9058096/how-to-call-the-default-certificate-check-when-overriding-servicepointmanager-se
// See http://msdn.microsoft.com/en-us/library/system.net.security.remotecertificatevalidationcallback.aspx
// See http://msdn.microsoft.com/en-us/library/dd633677%28v=exchg.80%29.aspx

namespace ABCEnterpriseLibrary
{
	public sealed class WebRequestCertificateValidator : IDisposable
	{
		private bool disposed;

		private WebRequest request;

		private AllowCertificateType allow;

		private RemoteCertificateValidationCallback callback;

		/// <summary>
		/// Creates a certificate validator that allows all certificates for the supplied web request.
		/// </summary>
		/// <param name="request">The WebRequest to validate for.</param>
		public WebRequestCertificateValidator(WebRequest request) : this(request, AllowCertificateType.All, null)
		{
			//
		}

		/// <summary>
		/// Creates a certificate validator that allows the specified type of certificates for the supplied web request.
		/// </summary>
		/// <param name="request">The WebRequest to validate for.</param>
		/// <param name="allow">The types of certificates you want to allow.</param>
		public WebRequestCertificateValidator(WebRequest request, AllowCertificateType allow) : this(request, allow, null)
		{
			//
		}

		/// <summary>
		/// Creates a certificate validator that only allows certificates for the supplied web request if the callback returns true.
		/// </summary>
		/// <param name="request">The WebRequest to validate for.</param>
		/// <param name="callback">The delegate that will be called to validate certificates for the WebRequest.</param>
		public WebRequestCertificateValidator(WebRequest request, RemoteCertificateValidationCallback callback) : this(request, AllowCertificateType.All, callback)
		{
			//
		}

		/// <summary>
		/// Creates a certificate validator that only allows certificates for the supplied web request if they are of the supplied type and if the callback returns true.
		/// </summary>
		/// <param name="request">The WebRequest to validate for.</param>
		/// <param name="allow">The types of certificates you want to allow.</param>
		/// <param name="callback">The delegate that will be called to validate certificates for the WebRequest.</param>
		public WebRequestCertificateValidator(WebRequest request, AllowCertificateType allow, RemoteCertificateValidationCallback callback)
		{
			ArgumentHelper.AssertNotNull("request", request);
			ArgumentHelper.AssertEnum("allow", (int)allow, typeof(AllowCertificateType));

			this.disposed = false;

			this.request = request;

			this.allow = allow;

			this.callback = callback;

			ServicePointManager.ServerCertificateValidationCallback += this.InternalCallback;
		}

		private bool InternalCallback(object sender, X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
		{
			WebRequest request = sender as WebRequest;

			if(request != null)
			{
				if(request == this.request)
				{
					if(!this.IsCertificateAllowed(certificate, chain, sslPolicyErrors))
					{
						return false;
					}

					if(this.callback != null)
					{
						return this.callback(sender, certificate, chain, sslPolicyErrors);
					}
				}
			}

			return true;
		}

		private bool IsCertificateAllowed(X509Certificate certificate, X509Chain chain, SslPolicyErrors sslPolicyErrors)
		{
			// See also: http://stackoverflow.com/a/5225530/114267
			// SslPolicyErrors.RemoteCertificateNameMismatch means that your client is connecting to a hostname that doesn't match what's on the certificate. 
			// If you are connecting to "www.myapp.com" then the name on the certificate should match that as well. The untrustedRoot error you get because 
			// the certificate is not installed as a Trusted Root CA in the certificate store of the client computer. You can either install that CA in the 
			// store or ignore that error as you have another explicit check for the public key

			switch(this.allow)
			{
				case AllowCertificateType.Valid:

					return sslPolicyErrors == SslPolicyErrors.None;

				case AllowCertificateType.ValidAndSelfSigned:

					if(sslPolicyErrors == SslPolicyErrors.None)
					{
						return true;
					}
					else if(sslPolicyErrors.HasFlag(SslPolicyErrors.RemoteCertificateChainErrors))
					{
						return NetHelper.IsSelfSignedCertificate(certificate, chain);
					}
					else
					{
						return false;
					}

				case AllowCertificateType.All:

					return true;

				default:

					throw new ArgumentException("Invalid AllowCertificateType.");
			}
		}

		public void Dispose()
		{
			if(!this.disposed)
			{
				ServicePointManager.ServerCertificateValidationCallback -= this.InternalCallback;

				this.callback = null;

				this.request = null;

				this.disposed = true;
			}
		}
	}
}
