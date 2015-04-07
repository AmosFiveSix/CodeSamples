using System;
using System.Collections.Generic;
using System.IO;
using ABCLibrary;
using ABCEnterpriseLibrary;
using ABCPdfLibrary;

namespace ABCServices
{
	// TODO:
	// * I'm still tightly coupled to PdfHelper.
	// * I'm still tightly coupled to ArgumentHelper.

	/// <summary>
	/// Converts attachments to PDF files.
	/// </summary>
	public class AttachmentConverter : IAttachmentConverter
	{
		private readonly IAttachmentRepository repository;

		#region --- Public Constructors and Methods ---

		/// <summary>
		/// Creates a new converter using the supplied repository to get attachment data.
		/// </summary>
		/// <param name="repository">The repository to use to obtain attachment data.</param>
		public AttachmentConverter(IAttachmentRepository repository)
		{
			this.repository = repository;
		}

		/// <summary>
		/// Determines if the attachment's mime type can be converted to a PDF.
		/// </summary>
		public bool CanConvertToPdf(IAttachment attachment)
		{
			// IMPORTANT: If you add more supported types here, please update the "MIME Types That ABC Can Convert to PDF"
			// section here: we will need to create a new wiki for AttachmentHandler support
			// similar to http://wiki/display/ABC/Generic+Attachments

			ArgumentHelper.AssertNotNull("attachment", attachment);

			switch(attachment.MimeType.ToLowerInvariant())
			{
				case MimeTypes.Pdf:

					return true;

				default:

					return false;
			}
		}

		/// <summary>
		/// Converts the supplied attachment to a PDF file. If the attachment is part of a bundle, all the attachments in the bundle are converted and included in the returned PDF file.
		/// </summary>
		/// <param name="attachment">The attachment to convert. If the attachment is part of a bundle, all the attachments in the bundle are converted and included in the returned PDF file.</param>
		/// <returns>A PDF document containing the converted attachment. You should dispose of the returned document when done with it.</returns>
		public IPdfDocument ConvertToPdf(IAttachment attachment)
		{
			ArgumentHelper.AssertNotNull("attachment", attachment);

			IEnumerable<IAttachment> attachments;

			if(String.IsNullOrEmpty(attachment.BundleIdentifier))
			{
				attachments = new List<IAttachment> { attachment };
			}
			else
			{
				attachments = this.repository.GetAttachments(attachment.BundleIdentifier);
			}

			return this.ConvertAttachments(attachments);
		}

		#endregion

		#region --- Converting Multiple Attachments ---

		/// <summary>
		/// Converts a collection of attachments into a single PDF file.
		/// </summary>
		/// <param name="attachments">A collection of attachments to convert.</param>
		/// <returns>The converted PDF document.</returns>
		private IPdfDocument ConvertAttachments(IEnumerable<IAttachment> attachments)
		{
			IPdfDocument document = PdfHelper.Create();

			foreach(var attachment in attachments)
			{
				using(var stream = this.ConvertAttachment(attachment))
				{
					document.Append(stream);
				}
			}

			return document;
		}

		#endregion

		#region --- Converting Individual Attachments --

		/// <summary>
		/// Converts the individual attachment into a stream of PDF file bytes.
		/// </summary>
		/// <param name="attachment">The attachment to convert. Only the single attachment is converted. Bundling is ignored.</param>
		/// <returns>A stream containing the PDF file's bytes.</returns>
		private Stream ConvertAttachment(IAttachment attachment)
		{
			// If you add more MIME types here, please update IsConvertibleToPdf().

			switch(attachment.MimeType.ToLowerInvariant())
			{
				case MimeTypes.Pdf:

					return this.ConvertPdf(attachment);

				default:

					return this.ConvertUnknownFileType(attachment);
			}
		}

		#region -- PDF --

		private Stream ConvertPdf(IAttachment attachment)
		{
			return this.repository.OpenDataStream(attachment);
		}

		#endregion

		#region -- Unknown File Type --

		private Stream ConvertUnknownFileType(IAttachment attachment)
		{
			var document = PdfHelper.Create();

			document.AddPage();

			document.DrawPageHeader("The attachment", "Arial", 12, 72, 18, 0);
			document.DrawPageHeader("\"" + attachment.Name + "\"", "Arial", 12, 72 + 18, 18, 0);
			document.DrawPageHeader("dated " + attachment.FileDate.ToString("G"), "Arial", 12, 72 + 18 + 18, 18, 0);
			document.DrawPageHeader("cannot be converted into a PDF file", "Arial", 12, 72 + 18 + 18 + 18, 18, 0);

			return document.AsStream();
		}

		#endregion

		#endregion
	}
}
