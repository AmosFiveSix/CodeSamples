using System;
using System.IO;
using System.Web;

namespace ABC4Handlers
{
	public interface IAttachmentResponseWriter
	{
		void WriteResponse(HttpResponse response, Stream stream, string mimeType = null, string encoding = null);
	}
}
