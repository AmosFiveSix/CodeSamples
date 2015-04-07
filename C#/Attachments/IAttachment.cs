using System;
using System.ComponentModel;
using ABCEnterpriseLibrary;

namespace ABC4Library
{
	/// <summary>
	/// Defines fields common to specific entity attachment classes. i.e. EmployeeAttachment.
	/// </summary>
	[Description("Defines fields common to specific entity attachment classes. i.e. EmployeeAttachment.")]
	[Release(2015, Month.January)]
	[Wiki("http://wiki/display/ABC/Generic+Attachments")]
	public interface IAttachment
	{
		/// <summary>
		/// The unique Id of the attachment. Usually DatabaseEntity.Id.
		/// </summary>
		string Id { get; set; }

		/// <summary>
		/// Load this with a unique name or description for the specific record. This is roughly equivalent to the Description field in the AccountTransacription table.
		/// </summary>
		[Description("Load this with a unique name or description for the specific record. This is roughly equivalent to the Description field in the AccountTransacription table.")]
		[Required]
		string Name { get; set; }

		/// <summary>
		/// The file date not the date it was loaded.
		/// </summary>
		[Description("The file date not the date it was loaded.")]
		[Optional]
		DateTime FileDate { get; set; }

		/// <summary>
		/// ABC will use this field to determine where the file is stored so;
		/// If attachments are to be stored in the database then, this MUST be a blank string.
		/// If the client decides to put attachments on the file system then, this MUST be 
		/// filled in with the path (i.e. \\SERVERNAME\SHARENAME\Attachments\MyAttachment.pdf) to the file.
		/// </summary>
		[Description("ABC will use this field to determine where the file is stored so; If attachments are to be stored in the database then, this MUST be a blank string. If the client decides to put attachments on the file system then, this MUST be filled in with the path (i.e. \\SERVERNAME\\SHARENAME\\Attachments\\MyAttachment.pdf) to the file.")]
		[DatabaseEntity(DatabaseColumnDataType.VarChar, 128)]
		[Optional]
		string FileLocation { get; set; }

		/// <summary>
		/// Attachments support the following Mime Types "wiki link goes here"
		/// </summary>
		[Required]
		[DatabaseEntity(DatabaseColumnDataType.VarChar, 128)]
		[Description("You MUST load this with a value. For details on loading the MIME type see: http://wiki/display/ABC/Generic+Attachments")]
		[Wiki("http://wiki/display/ABC/Generic+Attachments")]
		string MimeType { get; set; }

		/// <summary>
		/// If the MimeType is set to 'text/html', this field is used to set the charset portion of the MIME type. IE appears to default HTML to 7-bit US-ASCII if no encoding/charset is specified. This can make some characters, such as the non-breaking space (0xA0), appear as question marks in the browser. If you don't know the encoding for the HTML and it is not displaying correctly, try using 'Windows-1252' or 'ISO-8859-1' here. See http://tools.ietf.org/html/rfc2854#section-6 and http://htmlpurifier.org/docs/enduser-utf8.html.
		/// </summary>
		[Description("If the MimeType is set to 'text/html', this field is used to set the charset portion of the MIME type. IE appears to default HTML to 7-bit US-ASCII if no encoding/charset is specified. This can make some characters, such as the non-breaking space (0xA0), appear as question marks in the browser. If you don't know the encoding for the HTML and it is not displaying correctly, try using 'Windows-1252' or 'ISO-8859-1' here. See http://tools.ietf.org/html/rfc2854#section-6 and http://htmlpurifier.org/docs/enduser-utf8.html.")]
		[Optional]
		string Encoding { get; set; }

		/// <summary>
		/// A bundle: Is one or more Attachment records; Always appears as a single record when displaying in a grid. Always views and prints as a PDF. If you load one or more records with a non-blank BundleIdentifier, all of the records with the same BundleIdentifier will only appear once in the various grids, and when you view it will show all of the separate records merged into one PDF.
		/// </summary>
		[Description("A bundle: Is one or more Attachment records; Always appears as a single record when displaying in a grid. Always views and prints as a PDF. If you load one or more records with a non-blank BundleIdentifier, all of the records with the same BundleIdentifier will only appear once in the various grids, and when you view it will show all of the separate records merged into one PDF.")]
		[Optional]
		string BundleIdentifier { get; set; }

		/// <summary>
		/// Indicates the order in which the files should be merged together.
		/// Lower sequences come first. If this record is not part of a bundle, this should be zero.
		/// If it is part of a bundle, it should be one or greater.
		/// The record with a sequence of one will be the one that appears in display grids with other attachments.
		/// </summary>
		[Description("Indicates the order in which the files should be merged together. Lower sequences come first. If this record is not part of a bundle, this should be zero. If it is part of a bundle, it should be one or greater. The record with a sequence of one will be the one that appears in display grids with other attachments.")]
		[Optional]
		int BundleSequence { get; set; }
	}
}
