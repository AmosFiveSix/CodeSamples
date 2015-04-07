using System;
using ABC4Library;
using ABCPdfLibrary;

namespace ABC4Services
{
	public interface IAttachmentConverter
	{
		IPdfDocument ConvertToPdf(IAttachment attachment);

		bool CanConvertToPdf(IAttachment attachment);
	}
}
