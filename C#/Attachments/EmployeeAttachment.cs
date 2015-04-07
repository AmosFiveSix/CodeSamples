using System;
using System.ComponentModel;
using ABCEnterpriseLibrary;

namespace ABC4Library
{
	/// <summary>
	/// Contains employee specific attachment meta data.
	/// </summary>
	[Description("Contains employee specific attachment meta data.")]
	[Release(2015, Month.January)]
	[Wiki("http://wiki/display/ABC/Generic+Attachments")]
	[Module(Module.HRPayroll)]
	public class EmployeeAttachment : ABCEntity, IAttachment
	{
		private string employeeId = String.Empty;
		private string name = String.Empty;
		private DateTime fileDate = Constants.MinimumDate;
		private string fileLocation = String.Empty;
		private string mimeType = String.Empty;
		private string encoding = String.Empty;
		private string bundleIdentifier = String.Empty;
		private int bundleSequence = 0;

		/// <summary>
		/// Links back to an Employee record.
		/// </summary>
		[Description("Links back to an Employee record.")]
		[References(typeof(Employee), "Id")]
		[Required]
		public string EmployeeId
		{
			get { return employeeId; }
			set { employeeId = value; }
		}

		/// <summary>
		/// Load this with a unique name or description for the specific record. This is roughly equivalent to the Description field in the AccountTransacription table.
		/// </summary>
		[Description("Load this with a unique name or description for the specific record. This is roughly equivalent to the Description field in the AccountTransacription table.")]
		[Required]
		public string Name
		{
			get { return name; }
			set { name = value; }
		}

		[Optional]
		public DateTime FileDate
		{
			get { return fileDate; }
			set { fileDate = value; }
		}

		/// <summary>
		/// ABC will use this field to determine where the file is stored so;
		/// If attachments are to be stored in the database then, this MUST be a blank string.
		/// If the client decides to put attachments on the file system then, this MUST be 
		/// filled in with the path (i.e. \\SERVERNAME\SHARENAME\Attachments\MyAttachment.pdf) to the file.
		/// </summary>
		[Description("ABC will use this field to determine where the file is stored so; If attachments are to be stored in the database then, this MUST be a blank string. If the client decides to put attachments on the file system then, this MUST be filled in with the path (i.e. \\SERVERNAME\\SHARENAME\\Attachments\\MyAttachment.pdf) to the file.")]
		[DatabaseEntity(DatabaseColumnDataType.VarChar, 128)]
		[Optional]
		public string FileLocation
		{
			get { return fileLocation; }
			set { fileLocation = value; }
		}

		/// <summary>
		/// You MUST load this with a value. For details on loading the MIME type see: http://wiki/display/ABC/Generic+Attachments
		/// </summary>
		[Description("You MUST load this with a value. For details on loading the MIME type see: http://wiki/display/ABC/Generic+Attachments")]
		[Wiki("http://wiki/display/ABC/Generic+Attachments")]
		[Required]
		[DatabaseEntity(DatabaseColumnDataType.VarChar, 128)]
		public string MimeType
		{
			get { return mimeType; }
			set { mimeType = value; }
		}

		/// <summary>
		/// If the MimeType is set to 'text/html', this field is used to set the charset portion of the MIME type. IE appears to default HTML to 7-bit US-ASCII if no encoding/charset is specified. This can make some characters, such as the non-breaking space (0xA0), appear as question marks in the browser. If you don't know the encoding for the HTML and it is not displaying correctly, try using 'Windows-1252' or 'ISO-8859-1' here. See http://tools.ietf.org/html/rfc2854#section-6 and http://htmlpurifier.org/docs/enduser-utf8.html.
		/// </summary>
		[Description("If the MimeType is set to 'text/html', this field is used to set the charset portion of the MIME type. IE appears to default HTML to 7-bit US-ASCII if no encoding/charset is specified. This can make some characters, such as the non-breaking space (0xA0), appear as question marks in the browser. If you don't know the encoding for the HTML and it is not displaying correctly, try using 'Windows-1252' or 'ISO-8859-1' here. See http://tools.ietf.org/html/rfc2854#section-6 and http://htmlpurifier.org/docs/enduser-utf8.html.")]
		[Optional]
		public string Encoding
		{
			get { return encoding; }
			set { encoding = value; }
		}

		/// <summary>
		/// A bundle: Is one or more Attachment records; Always appears as a single record when displaying in a grid. Always views and prints as a PDF. If you load one or more records with a non-blank BundleIdentifier, all of the records with the same BundleIdentifier will only appear once in the various grids, and when you view it will show all of the separate records merged into one PDF.
		/// </summary>
		[Description("A bundle: Is one or more Attachment records; Always appears as a single record when displaying in a grid. Always views and prints as a PDF. If you load one or more records with a non-blank BundleIdentifier, all of the records with the same BundleIdentifier will only appear once in the various grids, and when you view it will show all of the separate records merged into one PDF.")]
		[Optional]
		public string BundleIdentifier
		{
			get { return bundleIdentifier; }
			set { bundleIdentifier = value; }
		}

		/// <summary>
		/// Indicates the order in which the files should be merged together.
		/// Lower sequences come first. If this record is not part of a bundle, this should be zero.
		/// If it is part of a bundle, it should be one or greater.
		/// The record with a sequence of one will be the one that appears in display grids with other attachments.
		/// </summary>
		[Description("Indicates the order in which the files should be merged together. Lower sequences come first. If this record is not part of a bundle, this should be zero. If it is part of a bundle, it should be one or greater. The record with a sequence of one will be the one that appears in display grids with other attachments.")]
		[Optional]
		public int BundleSequence
		{
			get { return bundleSequence; }
			set { bundleSequence = value; }
		}
	}
}
