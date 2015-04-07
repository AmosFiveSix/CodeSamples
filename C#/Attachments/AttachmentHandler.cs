using System;
using System.IO;
using System.Web;
using ABC4Core;
using ABC4Library;
using ABC4Services;
using ABCEnterpriseLibrary;

namespace ABC4Handlers
{
	// TODO:
	// * I'm still tightly coupled to HttpContext. - http://www.splinter.com.au/httpcontext-vs-httpcontextbase-vs-httpcontext/ * http://www.hurryupandwait.io/blog/unit-testing-asp-net-http-handlers-and-a-discussion-of-auto-mocking-and-the-testable-pattern
	// * I'm still tightly coupled to ExceptionHelper.IsFatal(). This might not be so bad since it can run OK in a unit test.
	// * I'm still tightly coupled to Request.Current.
	// * I'm still tightly coupled to RedirectTo.

	/// <summary>
	/// Handles requests for viewing attachments. This is an abstract class that concrete handlers can
	/// derive from in order to handle specific types of attachments, such as employee attachments.
	/// </summary>
	public abstract class AttachmentHandler : IHttpHandler
	{
		private readonly IAttachmentRepository repository;

		private readonly IAttachmentConverter converter;

		private readonly IAttachmentResponseWriter writer;

		protected AttachmentHandler(IAttachmentRepository repository, IAttachmentConverter converter, IAttachmentResponseWriter writer)
		{
			this.repository = repository;

			this.converter = converter;

			this.writer = writer;
		}

		#region -- Core Handling --

		/// <summary>
		/// The main entry point that ASP.NET calls so we can handle any requests.
		/// </summary>
		/// <param name="context"></param>
		public void ProcessRequest(HttpContext context)
		{
			string attachmentId = context.Request.QueryString.GetString("id", null);

			try
			{
				this.HandleRequest(context, attachmentId);
			}
			catch(Exception exception)
			{
				// First we do our basic exception handling, which includes logging and then redirecting the user to an appropriate error page.
				// Now if we just swallow the exception here the user will get the redirect and nothing more will happen. But if this is a bad
				// exception, like a thread abort, we don't want to swallow those. If we do throw an exception from there, ASP.NET will throw
				// out our redirect and replace it with the yellow ASP.NET error page. What constitutes a "bad" exception is hard to say. So we
				// just do with our general idea of "fatal" exceptions as being something we should not swallow.

				this.HandleException(context, attachmentId, exception);

				if(ExceptionHelper.IsFatal(exception))
				{
					throw;
				}
			}
		}

		private void HandleRequest(HttpContext context, string attachmentId)
		{
			if(!Request.Current.HasSession)
			{
				RedirectTo.NoSession(context);
			}
			else if(String.IsNullOrWhiteSpace(attachmentId))
			{
				RedirectTo.NoSuchResource(context);
			}
			else if(!this.HasConfiguration() || !this.HasPermission())
			{
				RedirectTo.NoSession(context);
			}
			else if(!this.repository.Exists(attachmentId))
			{
				RedirectTo.NoSuchResource(context);
			}
			else
			{
				this.SendResponse(context, attachmentId);
			}
		}

		private void HandleException(HttpContext context, string attachmentId, Exception exception)
		{
			// First we let our derived class log the exception. That usually means writing something to the server's event log using their specific event Id.
			// Then we check if this is a debug build, meaning we're on a developer's machine of if the user is sitting at the server. If so we send down the
			// the full exception information as XML using RedirectTo.Exception(). After that we check for some specific exception types that get specific
			// error message pages. If it's not a specific one we know about we redirect to the generic error page. Note that our thread keeps on running after
			// any of the redirects, so we can keep on doing things.

			this.LogException(attachmentId, exception);

			if(context.IsDebuggingEnabled || context.Request.IsLocal)
			{
				RedirectTo.Exception(context, exception);
			}
			else if(exception is FileNotFoundException)
			{
				RedirectTo.NoSuchResource(context);
			}
			else if(exception is UnauthorizedAccessException)
			{
				RedirectTo.NoSuchResource(context);
			}
			else
			{
				RedirectTo.Error(context);
			}
		}

		#endregion

		#region -- Sending the Attachment as the Response --

		private void SendResponse(HttpContext context, string attachmentId)
		{
			var attachment = this.repository.GetAttachment(attachmentId);

			if(this.IsBundled(attachment) || this.IsConvertibleToPdf(attachment))
			{
				this.SendConvertedPdf(context, attachment);
			}
			else
			{
				this.SendRawAttachment(context, attachment);
			}
		}

		private void SendConvertedPdf(HttpContext context, IAttachment attachment)
		{
			// The attachment is part of a bundle (which are always sent as PDF) or is convertible to a PDF.

			using(var document = this.converter.ConvertToPdf(attachment))
			{
				using(var stream = document.AsStream())
				{
					this.writer.WriteResponse(context.Response, stream, MimeTypes.Pdf);
				}
			}
		}

		private void SendRawAttachment(HttpContext context, IAttachment attachment)
		{
			// The attachment is not part of a bundle and it cannot be converted to a PDF.

			using(var stream = this.repository.OpenDataStream(attachment))
			{
				this.writer.WriteResponse(context.Response, stream, attachment.MimeType, attachment.Encoding);
			}
		}

		private bool IsBundled(IAttachment attachment)
		{
			if(String.IsNullOrEmpty(attachment.BundleIdentifier))
			{
				return false;
			}
			else
			{
				return true;
			}
		}

		private bool IsConvertibleToPdf(IAttachment attachment)
		{
			return converter.CanConvertToPdf(attachment);
		}

		#endregion

		#region -- Protected Properties and Methods that Concrete Classed Must Override --

		/// <summary>
		/// Derived classes can use this method to check if the user has permission to view the attachment.
		/// </summary>
		/// <returns>Returns true of the user has permission to view the attachment, otherwise false.</returns>
		protected virtual bool HasPermission()
		{
			return true;
		}

		/// <summary>
		/// Derived classes can use this method to check if ABC is configured for the correct type of attachments.
		/// </summary>
		/// <returns>Returns true of the configuration is correct, otherwise false.</returns>
		protected virtual bool HasConfiguration()
		{
			return true;
		}

		/// <summary>
		/// Gives derived classes a chance to log any exceptions that occur during processing, for example by writing to the server event log.
		/// </summary>
		/// <param name="attachmentId">The Id of the attachment the user requested.</param>
		/// <param name="exception">The exception that occurred.</param>
		protected abstract void LogException(string attachmentId, Exception exception);

		#endregion

		#region -- IHttpHandler Stuff --

		/// <summary>
		/// Indicates whether ASP.NET can reuse the same instance of the handler for multiple requests.
		/// </summary>
		public bool IsReusable
		{
			get { return true; }
		}

		#endregion
	}
}
