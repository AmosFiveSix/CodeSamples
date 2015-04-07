using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Authentication;
using System.Text;

namespace ABCEnterpriseLibrary
{
	public sealed class FtpUpload
	{
		public const int DefaultConnectTimeout = 30000;		// 30 Seconds in milliseconds

		public const int DefaultWriteTimeout = 300000;		// 5 Minutes in milliseconds

		public string Server { get; set; }

		public int Port { get; set; }

		public string Path { get; set; }

		public string Username { get; set; }

		public string Password { get; set; }

		public bool EnableSsl { get; set; }

		public bool UsePassive { get; set; }

		public bool UseBinary { get; set; }

		public int ConnectTimeout { get; set; }

		public int WriteTimeout { get; set; }

		public AllowCertificateType AllowCertificates { get; set; }

		public RemoteCertificateValidationCallback CertificateCallback { get; set; }

		public FtpUploadResponse Response { get; private set; }

		public FtpUpload()
		{
			this.UsePassive = true;

			this.ConnectTimeout = FtpUpload.DefaultConnectTimeout;

			this.WriteTimeout = FtpUpload.DefaultWriteTimeout;

			this.AllowCertificates = AllowCertificateType.Valid;
		}

		public void Upload(string data)
		{
			this.Upload(data, Encoding.UTF8);
		}

		public void Upload(string data, Encoding encoding)
		{
			ArgumentHelper.AssertNotNull("data", data);
			ArgumentHelper.AssertNotNull("encoding", encoding);

			this.Upload(encoding.GetBytes(data));
		}

		public void Upload(byte[] data)
		{
			// Details on how FtpWebRequest and FtpWebRespone work for uploading files:
			// 1. Calling FtpWebRequest.Create() will throw an exception if we don't have network connect permission for the URI.
			// 2. Setting request.KeepAlive to false causes stream.Dispose() to close the connection to the FTP server. If we did not do this the connection might stay open until the process terminates.
			// 3. request.Timeout is the timeout period in milliseconds for the request.GetRequestStream() and request.GetResponse() methods.
			// 4. request.ReadWriteTimeout is the timeout period in milliseconds for the request.GetRequestStream().Write() methods.
			// 5. When you call GetRequestSteam, it sends these commands as of .NET 4.5:
			//    a. For SSL off: USER xxx, PASS xxx, OPTS utf8 on, PWD, TYPE I, EPSV (for passive) or EPRT (for active), STOR ..path.. which responds with 150.
			//    b  For SSL on:  AUTH TLS, USER xxx, PASS xxx, PBSZ 0, PROT P, OPTS utf8 on, PWD, TYPE I, EPRT (for active), STOR ..path.. which responds with 150.
			// 6. When stream.Write() is called the bytes are actually sent over the network to the FTP server.
			// 7. When stream.Dispose() is called the data transfer is finished the connection to the FTP server is closed. The resulst are already stored for when we call request.GetResponse().
			// 8. Calling response.GetResponseStream() always returns an empty stream.

			ArgumentHelper.AssertNotNull("data", data);

			if(data.LongLength > Int32.MaxValue)
			{
				throw new FtpException("The data is " + data.LongLength + " bytes long. Cannot FTP more than " + Int32.MaxValue + " bytes.");
			}

			Stream stream = null;

			var request = (FtpWebRequest)FtpWebRequest.Create(this.GenerateUri());	 // GenerateUri will throw is the URI components are invalid.

			using(var validator = new WebRequestCertificateValidator(request, this.AllowCertificates, this.CertificateCallback))
			{
				request.EnableSsl = this.EnableSsl;

				request.UsePassive = this.UsePassive;

				request.UseBinary = this.UseBinary;

				request.KeepAlive = false;

				request.ReadWriteTimeout = this.WriteTimeout;

				request.Timeout = this.ConnectTimeout;
				
				request.Method = WebRequestMethods.Ftp.UploadFile;

				try
				{
					try
					{
						Tracing.Trace(TraceEventType.Verbose, (int)FtpUploadEvent.ConnectBeginning, "FTP upload connection beginning.");

						stream = request.GetRequestStream();			// Connects to the FTP server

						Tracing.Trace(TraceEventType.Verbose, (int)FtpUploadEvent.ConnectCompleted, "FTP upload connection completed.");
					}
					catch(Exception exception)
					{
						if(this.HandleConnectException(exception) == ExceptionResult.Unhandled)
						{
							throw;	// We didn't do anything special to handle the exception while connecting, so rethrow the original exception.
						}
					}

					try
					{
						Tracing.Trace(TraceEventType.Verbose, (int)FtpUploadEvent.WriteDataBeginning, "FTP upload write data beginning.");

						stream.Write(data, 0, data.Length);			// Writes data to the FTP server

						Tracing.Trace(TraceEventType.Verbose, (int)FtpUploadEvent.WriteDataCompleted, "FTP upload write data completed.");
					}
					catch(Exception exception)
					{
						if(this.HandleWriteException(exception) == ExceptionResult.Unhandled)
						{
							throw;	// We didn't do anything special to handle the exception while writing, so rethrow the original exception.
						}
					}
				}
				finally
				{
					if(stream != null)
					{
						try
						{
							Tracing.Trace(TraceEventType.Verbose, (int)FtpUploadEvent.DisconnectBeginning, "FTP upload disconnection beginning.");

							stream.Dispose();						// Disconnects from the FTP server

							Tracing.Trace(TraceEventType.Verbose, (int)FtpUploadEvent.DisconnectCompleted, "FTP upload disconnection completed.");
						}
						catch(Exception exception)
						{
							if(this.HandleDisconnectException(exception) == ExceptionResult.Unhandled)
							{
								throw;	// We didn't do anything special to handle the exception while disconnecting, so rethrow the original exception.
							}
						}
					}
				}

				this.Response = new FtpUploadResponse();

				using(var response = (FtpWebResponse)request.GetResponse())
				{
					this.Response.StatusCode = response.StatusCode;

					this.Response.Description = response.StatusDescription;

					this.Response.BannerMessage = response.BannerMessage;

					this.Response.WelcomeMessage = response.WelcomeMessage;

					this.Response.ExitMessage = response.ExitMessage;

					response.Close();
				}
			}
		}

		private Uri GenerateUri()
		{
			try
			{
				var builder = new UriBuilder(Uri.UriSchemeFtp, this.Server, this.Port, this.Path);

				builder.UserName = this.Username;
				builder.Password = this.Password;

				return builder.Uri;
			}
			catch(UriFormatException exception)
			{
				throw new FtpException("Unable to generate a properly formatted URI. The server, port, path, username or password may contain invalid characters or be formatted improperly.", exception);
			}
		}

		private enum ExceptionResult
		{
			Handled,
			Unhandled
		}

		private ExceptionResult HandleConnectException(Exception original)
		{
			Exception replacement;

			WebException webException = original as WebException;

			if(webException != null)
			{
				if(webException.Status == WebExceptionStatus.NameResolutionFailure)
				{
					replacement = new FtpException("Unable to connect to the FTP server. The server address or port may be incorrect or the server may be offline.", original);
				}
				else if(webException.Status == WebExceptionStatus.ProtocolError)
				{
					var response = webException.Response as FtpWebResponse;

					if(response != null)
					{
						if(response.StatusCode == FtpStatusCode.NotLoggedIn)
						{
							if(this.EnableSsl == false)
							{
								replacement = new FtpException("An error occurred while logging into the FTP server. The username or password may be incorrect, or the server may require explicit SSL. The FTP server's response was: " + response.StatusDescription, original);
							}
							else
							{
								replacement = new FtpException("An error occurred while logging into the FTP server. The username or password may be incorrect. The FTP server's response was: " + response.StatusDescription, original);
							}
						}
						else if(response.StatusCode == FtpStatusCode.NeedLoginAccount)
						{
							if(this.EnableSsl == false)
							{
								replacement = new FtpException("An error occurred while logging into the FTP server. It may require explicit SSL. The FTP server's response was: " + response.StatusDescription, original);
							}
							else
							{
								replacement = new FtpException("An error occurred while logging into the FTP server. The username or password may be incorrect. The FTP server's response was: " + response.StatusDescription, original);
							}
						}
						else if((response.StatusCode == FtpStatusCode.CommandNotImplemented) && (this.EnableSsl == true))
						{
							replacement = new FtpException("An error occurred while logging into the FTP server. Explicit SSL was requested but the server may not support it. The FTP server's response was: " + response.StatusDescription, original);
						}
						else if(response.StatusCode == FtpStatusCode.ActionNotTakenFileUnavailable)
						{
							replacement = new FtpException("An error occurred while preparing to send the file to the server. The folder requested may not exist or the user account may not have write permission to it. The FTP server's response was: " + response.StatusDescription, original);
						}
						else
						{
							// There is a response but it's not one we normally expect to see.

							replacement = new FtpException("An unexpected error occurred while connecting to or communicating with the FTP server. The FTP server's response was: " + response.StatusDescription, original);
						}
					}
					else
					{
						// There was no response. I'm not sure this can actually happen with a protocol error since that usually means the remote server is telling us we did something wrong. 

						replacement = new FtpException("An unexpected error occurred while connecting to or communicating with the FTP server. The FTP server reported that we made a protocol violation but did not provide any details. " + original.Message, original);
					}
				}
				else if(webException.Status == WebExceptionStatus.Timeout)
				{
					replacement = new FtpException("Connecting to the FTP server timed out after " + this.ConnectTimeout + " milliseconds.", original);
				}
				else if(webException.Status == WebExceptionStatus.UnknownError)
				{
					replacement = new FtpException("Unable to connect to the FTP server. This type of error has happened when trying to connect using explicit SSL or no SSL to an FTP server that only supports implicit SSL.", original);
				}
				else
				{
					// It's a WebException but it's not one we normaly expect to see.

					replacement = new FtpException("An unexpected error occurred while connecting to or communicating with the FTP server. " + original.Message, original);
				}
			}
			else
			{
				AuthenticationException authenticationException = original as AuthenticationException;

				if(authenticationException != null)
				{
					replacement = new FtpException("An authentication exception occurred while trying to connect to the FTP server. The SSL certificate provided by the FTP server may be invalid.", original);
				}
				else
				{
					// At this point it's not a WebException or AuthenticationException. We don't know what to do with it so we tell our caller that. Our caller normally rethrows the original exception.

					replacement = null;
				}
			}

			if(replacement != null)
			{
				// We want our FtpException to only be logged as a warning. Our caller can decide if they want to log it as an error, which may alert support staff.

				Tracing.Trace(TraceEventType.Warning, (int)FtpUploadEvent.ConnectException, replacement.Message + " " + ExceptionHelper.ToXml(replacement));

				throw replacement;
			}
			else
			{
				Tracing.Trace(TraceEventType.Error, (int)FtpUploadEvent.ConnectUnexpectedException, original.Message + " " + ExceptionHelper.ToXml(original));

				return ExceptionResult.Unhandled;
			}
		}

		private ExceptionResult HandleWriteException(Exception original)
		{
			Exception replacement;

			WebException webException = original as WebException;

			if(webException != null)
			{
				if(webException.Status == WebExceptionStatus.ProtocolError)
				{
					var response = webException.Response as FtpWebResponse;

					if(response != null)
					{
						replacement = new FtpException("An unexpected error occurred while sending data to the FTP server. The FTP server's response was: " + response.StatusDescription, original);
					}
					else
					{
						// There was no response. I'm not sure this can actually happen with a protocol error since that usually means the remote server is telling us we did something wrong.

						replacement = new FtpException("An unexpected error occurred while sending data to the FTP server. The FTP server reported that we made a protocol violation but did not provide any details. " + original.Message, original);
					}
				}
				else if(webException.Status == WebExceptionStatus.Timeout)
				{
					replacement = new FtpException("Sending data to the FTP server timed out after " + this.ConnectTimeout + " milliseconds.", original);
				}
				else
				{
					// It's a WebException but it's not one we normaly expect to see.

					replacement = new FtpException("An unexpected error occurred while sending data to the FTP server. " + original.Message, original);
				}
			}
			else
			{
				// It's not a WebException. We don't know what to do with it so we tell our caller that. Our caller normally rethrows the original exception.

				replacement = null;
			}

			if(replacement != null)
			{
				// We want our FtpException to only be logged as a warning. Our caller can decide if they want to log it as an error, which may alert support staff.

				Tracing.Trace(TraceEventType.Warning, (int)FtpUploadEvent.WriteDataException, replacement.Message + " " + ExceptionHelper.ToXml(replacement));

				throw replacement;
			}
			else
			{
				Tracing.Trace(TraceEventType.Error, (int)FtpUploadEvent.WriteDataUnexpectedException, original.Message + " " + ExceptionHelper.ToXml(original));

				return ExceptionResult.Unhandled;
			}
		}

		private ExceptionResult HandleDisconnectException(Exception original)
		{
			Exception replacement;

			WebException webException = original as WebException;

			if(webException != null)
			{
				var response = webException.Response as FtpWebResponse;

				if(response != null)
				{
					replacement = new FtpException("An unexpected error occurred while disconnecting from the FTP server. The FTP server's response was: " + response.StatusDescription, original);
				}
				else
				{
					replacement = new FtpException("An unexpected error occurred while disconnecting from the FTP server. " + original.Message, original);
				}
			}
			else
			{
				// It's not a WebException. We don't know what to do with it so we tell our caller that. Our caller normally rethrows the original exception.

				replacement = null;
			}

			if(replacement != null)
			{
				// We want our FtpException to only be logged as a warning. Our caller can decide if they want to log it as an error, which may alert support staff.

				Tracing.Trace(TraceEventType.Warning, (int)FtpUploadEvent.DisconnectException, replacement.Message + " " + ExceptionHelper.ToXml(replacement));

				throw replacement;
			}
			else
			{
				Tracing.Trace(TraceEventType.Error, (int)FtpUploadEvent.DisconnectUnexpectedException, original.Message + " " + ExceptionHelper.ToXml(original));

				return ExceptionResult.Unhandled;
			}
		}

		#region -- Trace Logging --

		public static readonly string TraceSourceName = typeof(FtpUpload).FullName;

		private static class Tracing
		{
			// This is an internal class to make initializing and use tracing a bit easier.

			private static readonly TraceSource traceSource = new TraceSource(FtpUpload.TraceSourceName);

			public static void Trace(TraceEventType eventType, int id, string message)
			{
				traceSource.TraceEvent(eventType, id, message);
			}

			public static void Flush()
			{
				traceSource.Flush();
			}
		}

		#endregion
	}
}
