using System;
using System.IO;
using System.Web;
using ABC4Library;
using ABCEnterpriseLibrary;

namespace ABC4Handlers
{
	// TODO:
	// * I'm still tightly coupled to HttpResponse.
	// * I'm still tightly coupled to ArgumentHelper.

	/// <summary>
	/// Writes HTTP responses for binary data based on the data's MIME type and encoding.
	/// </summary>
	public class AttachmentResponseWriter : IAttachmentResponseWriter
	{
		/// <summary>
		/// Writes the appropriate HTTP response headers and body for the supplied binary 'attachment' data.
		/// </summary>
		/// <param name="response">The response to write to.</param>
		/// <param name="stream">The stream of data to write. The stream should be positioned at the start.</param>
		/// <param name="mimeType">The MIME type of the data. If null or String.Empty the data will be sent as application/octet-stream" meaning the browser will have to figure it out.</param>
		/// <param name="encoding">The text encoding of the data if any. Only applied to HTML data.</param>
		public void WriteResponse(HttpResponse response, Stream stream, string mimeType = null, string encoding = null)
		{
			ArgumentHelper.AssertNotNull("response", response);
			ArgumentHelper.AssertNotNull("stream", stream);

			StartResponse(response, mimeType, encoding);

			response.AddHeader("Content-Length", stream.Length.ToString());

			CopyStreamToResponse(stream, response);
		}

		private void StartResponse(HttpResponse response, string mimeType, string encoding)
		{
			string contentType = this.GetContentType(mimeType, encoding);

			response.BufferOutput = true;

			response.Cache.SetCacheability(HttpCacheability.NoCache);

			response.AddHeader("Content-Type", contentType);
			response.AddHeader("Accept-Ranges", "none");

			if(contentType == MimeTypes.Rtf)
			{
				// IE seems to think it can display RTF in-line, but actually displays garbage.
				// To prevent IE from trying (and failing) we have to force it to prompt the
				// user to save the file. A Content-Disposition of attachment does that. We just
				// make up a generic default file name.

				response.AddHeader("Content-Disposition", "attachment; filename=attachment.rtf");
			}
		}

		private string GetContentType(string mimeType, string encoding)
		{
			mimeType = mimeType.ToLowerInvariant();

			if(String.IsNullOrWhiteSpace(mimeType))
			{
				return MimeTypes.OctetStream;
			}

			if(mimeType == MimeTypes.WebSite)
			{
				return MimeTypes.PlainText;
			}

			if((mimeType == MimeTypes.Html) && !String.IsNullOrWhiteSpace(encoding))
			{
				// IE appears to default HTML to 7-bit US-ASCII if no encoding/charset is specified.
				// This can make some characters, such as the non-breaking space (0xA0), appear as
				// question marks in the browser. If you don't know the encoding for the HTML and it
				// is not displaying correctly, try using 'Windows-1252' or 'ISO-8859-1' here. See:
				// * http://tools.ietf.org/html/rfc2854#section-6
				// * http://htmlpurifier.org/docs/enduser-utf8.html.

				return mimeType + "; charset=" + encoding;
			}

			return mimeType;
		}

		private void CopyStreamToResponse(Stream stream, HttpResponse response)
		{
			// We intentionally don't use Response.TransmitFile or Response.WriteFile.
			// From http://stackoverflow.com/q/2187252/114267:
			// * Response.WriteFile is synchronous, but it buffers the file in memory before sending it to the user.
			//   Since I'm dealing with very large files, this could cause problems.
			// * Response.TransmitFile doesn't buffer locally so it does work for large files, but it is asynchronous,
			//   so I can't delete the file after calling TransmitFile. Apparently flushing the file doesn't guarantee
			//   that I can delete it either? [Note these comments don't actually apply here in ABC ;-)]

			byte[] buffer;
			long bytesRemaining;
			int bytesRead;

			buffer = new byte[10000];

			bytesRemaining = stream.Length;

			while(bytesRemaining > 0)
			{
				if(response.IsClientConnected)
				{
					bytesRead = stream.Read(buffer, 0, buffer.Length);

					response.OutputStream.Write(buffer, 0, bytesRead);

					response.SafeFlush(); // This ignores connection aborted exceptions.

					bytesRemaining -= bytesRead;
				}
				else
				{
					bytesRemaining = 0;
				}
			}
		}
	}
}
