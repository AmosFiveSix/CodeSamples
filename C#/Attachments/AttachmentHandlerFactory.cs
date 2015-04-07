using System;
using System.Web;
using ABC4Services;

namespace ABC4Handlers
{
	/// <summary>
	/// This HttpHandler factory lets us control how the various attachment handlers are instantiated. With normal .ashx handlers,
	/// the handler must have a parameterless constructor. Since we want to pass parameters to the constructor so we can inject
	/// dependencies we need to use an IHttpHandlerFactory. Those don't work with regular .ashx files. So we have to register
	/// our handler factory in web.config like this:
	/// <system.webServer><handlers><add name="EmployeeAttachmentHandler" verb="*" path="Handlers/EmployeeAttachment.ashx" type="ABC4Handlers.AttachmentHandlerFactory, ABC4Handlers" resourceType="Unspecified" /></handlers></system.webServer>
	/// </summary>
	public class AttachmentHandlerFactory : IHttpHandlerFactory
	{
		public IHttpHandler GetHandler(HttpContext context, string requestType, string url, string pathTranslated)
		{
			if(url.EndsWith("EmployeeAttachment.ashx", StringComparison.OrdinalIgnoreCase))
			{
				var repository = new EmployeeAttachmentRepository();

				var converter = new AttachmentConverter(repository);

				var writer = new AttachmentResponseWriter();

				var handler = new EmployeeAttachmentHandler(repository, converter, writer);

				return handler;
			}

			throw new ArgumentException("Unknown handler URL.");
		}

		public void ReleaseHandler(IHttpHandler handler)
		{
			// We don't need to do anything here since our handler has no resource to dispose or release manually.
		}
	}
}
