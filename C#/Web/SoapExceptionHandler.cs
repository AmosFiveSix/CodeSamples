using System;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Web.Services.Protocols;
using System.Xml;

namespace ABCEnterpriseLibrary
{
	public class SoapExceptionHandler : SoapExtension
	{
		// This SOAP Extension catches any exceptions throw from ASMX web service methods and packs up the exception information into the SOAP Fault response.
		// We have to go through some rigmarole to inject ourself into the process at the right point.

		// Reference:
		// * SoapExtension Class
		//   http://msdn.microsoft.com/en-us/library/system.web.services.protocols.soapextension.aspx
		// * Using SOAP Extensions in ASP.NET
		//   http://msdn.microsoft.com/en-us/magazine/cc164007.aspx
		// * Exception Injection Using a Custom SOAP Extension
		//   http://haacked.com/archive/2005/06/29/ExceptionInjectionUsingCustomSoapExtension.aspx
		// * ASMX SoapExtension to Strip out Whitespace and New Lines
		//   http://www.hanselman.com/blog/ASMXSoapExtensionToStripOutWhitespaceAndNewLines.aspx

		// Sequence of events:
		//   Constructor
		//   Initialize
		//   ChainStream
		//   BeforeDeserialize
		//   AfterDeserialize
		//   -Method is called-
		//   ChainStream
		//   BeforeSerialize
		//   AfterSerialize

		private static readonly TraceSource mTrace = new TraceSource("SoapExceptionHandler");

		private Stream mTheirStream;
		private Stream mOurStream;

		public override object GetInitializer(Type serviceType)
		{
			return null;
		}

		public override object GetInitializer(LogicalMethodInfo methodInfo, SoapExtensionAttribute attribute)
		{
			throw new NotImplementedException("The method is not implemented.");
		}

		public override void Initialize(object initializer)
		{
			// Do nothing
		}

		public override Stream ChainStream(Stream stream)
		{
			// ChainStream is called normally twice, once before the request is processed and once before the response is processed.
			// Note that we cannot expect to always be called for the request. We may only be called once.
			// Since we may want to modify the response, we need to insert ourselves into the stream chain here.

			mTheirStream = stream;

			mOurStream = new MemoryStream();

			return mOurStream;
		}

		public override void ProcessMessage(SoapMessage message)
		{
			if(IsTracing)
			{
				Trace(TraceEventType.Information, String.Format(CultureInfo.InvariantCulture, "ProcessMessage({0}) for {1}", message.Stage.ToString(), message.Action));
			}

			switch(message.Stage)
			{
				case SoapMessageStage.BeforeDeserialize:

					// The incoming response flows like this: original -> extension -> this extension -> extension -> used for calling the method

			        // We have to copy the data from the previous extension's stream (which starts out with the actual incoming data)
			        // into the stream that we've passed on to the next extension so that extension will get the data too.

					this.CopyStream(mTheirStream, mOurStream);

					mOurStream.Seek(0, SeekOrigin.Begin);

					break;

				case SoapMessageStage.AfterSerialize:

			        // The outgoing response flows like this: original -> extension -> this extension -> extension -> returned to the client.

			        // The previous extension will have copied their version of the response into our stream. We need to modify that if needed, 
			        // then copy that into the stream used by the next extension.

					mOurStream.Seek(0, SeekOrigin.Begin);

					if(message.Exception != null)
					{
						this.ModifyStream(mOurStream, mTheirStream, message.Exception);
					}
					else
					{
						this.CopyStream(mOurStream, mTheirStream);
					}

					break;

				default:

					break;
			}
		}

		protected virtual void ModifyStream(Stream source, Stream destination, Exception exception)
		{
			// Here we read the prepared XML response from the source stream, check and modify it if needed, then write it out to the destination stream.

			try
			{
				XmlDocument document;
				XmlNode node;

				document = new XmlDocument();

				document.Load(source);

				node = document.SelectSingleNode("/*/*/*/detail");

				// If the details node already has content, then we're most likely dealing with a SoapExceptionEx which will have already taken care of all this.

				if((node != null) && !node.HasChildNodes)
				{
					if(exception.InnerException != null)
					{
						// This is what's normal when an exception is thrown from a web method. It gets turned into a SoapException where the InnerException is the original exception.

						node.InnerXml = ExceptionHelper.ToXml(exception.InnerException);
					}
					else
					{
						// There was most likely an error in a SOAP extension or the .Net framework's SOAP processing.

						node.InnerXml = ExceptionHelper.ToXml(exception);
					}
				}

				document.Save(destination);

				if(IsTracing)
				{
					Trace(TraceEventType.Error, String.Format(CultureInfo.InvariantCulture, "Web Service Method Threw Exception : {0}", document.OuterXml.Replace("\r", "").Replace("\n", "").Replace("\t", "")));
				}
			}
			catch
			{
				// TODO: Catching all exceptions is bad.

				this.CopyStream(source, destination);
			}
		}

		private void CopyStream(Stream source, Stream destination)
		{
			StreamReader reader = new StreamReader(source);

			StreamWriter writer = new StreamWriter(destination);

			if(IsTracing)
			{
				string xml = reader.ReadToEnd().Replace("\r", "").Replace("\n", "").Replace("\t", "");

				Trace(TraceEventType.Verbose, String.Format(CultureInfo.InvariantCulture, "CopyStream() : {0}", xml));

				source.Seek(0, SeekOrigin.Begin);
			}

			writer.WriteLine(reader.ReadToEnd());

			writer.Flush();
		}

		private static void Trace(TraceEventType type, string message)
		{
			if(mTrace.Switch.ShouldTrace(type))
			{
				mTrace.TraceEvent(type, 0, "{0} : {1}", DateTimeProvider.Current.Now.ToString("yyyy-MM-dd HH:mm:ss:fff", CultureInfo.InvariantCulture), message);
			}
		}

		private static bool IsTracing
		{
			get
			{
				return mTrace.Switch.ShouldTrace(TraceEventType.Critical);
			}
		}
	}
}
