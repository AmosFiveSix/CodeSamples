using System;
using System.ComponentModel;
using ABCEnterpriseLibrary;

namespace ABC4Library
{
	/// <summary>
	/// Contains the actual binary attachment data.
	/// </summary>
	[Description("Contains the actual binary attachment data.")]
	[Module(Module.HRPayroll)]
	[Release(2015, Month.January)]
	[Wiki("http://wiki/display/ABC/Generic+Attachments")]
	public class EmployeeAttachmentData : ABCEntity, IAttachmentData
	{
		string employeeAttachmentId = String.Empty;

		/// <summary>
		/// Links back to an EmployeeAttachment record.
		/// </summary>
		[Description("Links back to an EmployeeAttachment record.")]
		[References(typeof(EmployeeAttachment), "Id")]
		[Required]
		public string EmployeeAttachmentId
		{
			get { return employeeAttachmentId; }
			set { employeeAttachmentId = value; }
		}

		/// <summary>
		/// If the attachment's data is stored in the database then, this is where the actual bytes go.
		/// </summary>
		[Description("If the attachment's data is stored in the database then, this is where the actual bytes go.")]
		[DatabaseEntity(DatabaseColumnDataType.VarBinaryMax)]
		[Required]
		public byte[] Data { get; set; }
	}
}
