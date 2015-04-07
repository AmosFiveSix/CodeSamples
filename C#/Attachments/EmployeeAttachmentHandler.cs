using System;
using ABC4Core;
using ABC4Services;
using ABCEnterpriseLibrary;

namespace ABC4Handlers
{
	// TODO:
	// I'm still tightly coupled to Request.Current.
	// I'm still tightly coupled to ServerEventLog.LogError.
	// I'm still tightly coupled to ExceptionHelper.ToXml. This might not be so bad since it can run OK in a unit test.

	public class EmployeeAttachmentHandler : AttachmentHandler
	{
		public EmployeeAttachmentHandler(IAttachmentRepository repository, IAttachmentConverter converter, IAttachmentResponseWriter writer) : base(repository, converter, writer)
		{
			// The base class does everything we need.
		}

		protected override bool HasConfiguration()
		{
			return Request.Current.HasConfiguration(Configuration.HRPayroll);
		}

		protected override bool HasPermission()
		{
			return Request.Current.HasPermission(Permission.Employee.Attachments);
		}

		protected override void LogException(string attachmentId, Exception exception)
		{
			ServerEventLog.LogError(ABCEvent.EmployeeAttachmentHandlerException, "EmployeeAttachmentHandler caught an unexpected exception. {0} EmployeeAttachmentId: {1}; SessionId: {2}; Exception: {3}", exception.Message, attachmentId, Request.Current.SessionId, ExceptionHelper.ToXml(exception));
		}
	}
}
