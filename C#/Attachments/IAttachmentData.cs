using System;
using System.ComponentModel;
using ABCEnterpriseLibrary;

namespace ABC4Library
{
	/// <summary>
	/// Defines fields common to specific entity attachment data classes i.e. EmployeeAttachmentData.
	/// </summary>
	[Description("Defines fields common to specific entity attachment data classes i.e. EmployeeAttachmentData.")]
	[Release(2015, Month.January)]
	[Wiki("http://wiki/display/ABC/Generic+Attachments")]
	public interface IAttachmentData
	{
		/// <summary>
		/// If the attachment's data is stored in the database then, this is where the actual bytes go.
		/// </summary>
		[Description("If the attachment's data is stored in the database then, this is where the actual bytes go.")]
		[DatabaseEntity(DatabaseColumnDataType.VarBinaryMax)]
		[Required]
		byte[] Data { get; set; }
	}
}
