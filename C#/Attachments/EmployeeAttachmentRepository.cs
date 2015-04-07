using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SqlClient;
using System.IO;
using System.Security;
using ABC4Core;
using ABC4Library;

namespace ABC4Services
{
	// TODO:
	// * I'm still tightly coupled to Entity.
	// * I'm still tightly coupled to Entities.

	/// <summary>
	/// Repository for employee attachments. Added to the January 2015 release by Ron Skufca.
	/// </summary>
	public class EmployeeAttachmentRepository : IAttachmentRepository
	{
		/// <summary>
		/// Determines if an attachment with the supplied id exists in the database.
		/// </summary>
		/// <param name="employeeAttachmentId">The id of the attachment to check for.</param>
		/// <returns>Returns true of the attachment is in the database, otherwise false.</returns>
		/// <exception cref="SqlException">An error occurred when trying to read the data from the database.</exception>
		/// <exception cref="DataException">An error occurred when trying to read the data from the database.</exception>
		public bool Exists(string employeeAttachmentId)
		{
			return Entity.Exists<EmployeeAttachment>(employeeAttachmentId);
		}

		/// <summary>
		/// Returns an attachment based on its Id.
		/// </summary>
		/// <param name="employeeAttachmentId">The id of the attachment to load.</param>
		/// <returns>A loaded instance of the attachment.</returns>
		/// <exception cref="EntityNotFoundException">Thrown if no attachment with the supplied Id is found in the database.</exception>
		/// <exception cref="SqlException">An error occurred when trying to read the data from the database.</exception>
		/// <exception cref="DataException">An error occurred when trying to read the data from the database.</exception>
		public IAttachment GetAttachment(string employeeAttachmentId)
		{
			return Entity.Restore<EmployeeAttachment>(employeeAttachmentId);
		}

		/// <summary>
		/// Returns a collection of attachments what all have the same bundle identifier, sorted by their bundle sequence.
		///</summary>
		/// <param name="bundleIdentifier">The identifier of the bundle to get.</param>
		/// <returns>A collection of attachments with the same bundle identifier sorted by bundle sequence.</returns>
		/// <exception cref="SqlException">An error occurred when trying to read the data from the database.</exception>
		/// <exception cref="DataException">An error occurred when trying to read the data from the database.</exception>
		public IEnumerable<IAttachment> GetAttachments(string bundleIdentifier)
		{
			return Entities.Find<EmployeeAttachment>("BundleIdentifier", bundleIdentifier, "BundleSequence").ToList<EmployeeAttachment>();
		}

		/// <summary>
		/// Returns the actual data for an attachment as an array of bytes in memory.
		/// </summary>
		/// <param name="employeeAttachmentId">The id of the attachment to load.</param>
		/// <returns>An in-memory array of bytes containing the raw attachment data.</returns>
		/// <exception cref="FileNotFoundException">Thrown if the data is on the file system and the file does not exist.</exception>
		/// <exception cref="IOException">Thrown for various I/O related problems when the data is on the file system. Includes DirectoryNotFoundException, PathTooLongException.</exception>
		/// <exception cref="SecurityException">The caller does not have the required permission.</exception>
		/// <exception cref="UnauthorizedAccessException">The access requested is not permitted by the operating system for the specified path, such as when access is Write or ReadWrite and the file or directory is set for read-only access.</exception>
		/// <exception cref="EntityNotFoundException">Thrown if no attachment with the supplied Id is found in the database.</exception>
		/// <exception cref="SqlException">An error occurred when trying to read the data from the database.</exception>
		/// <exception cref="DataException">An error occurred when trying to read the data from the database.</exception>
		public byte[] GetAttachmentData(string employeeAttachmentId)
		{
			IAttachment attachment = this.GetAttachment(employeeAttachmentId);

			if(String.IsNullOrEmpty(attachment.FileLocation))
			{
				return Entity.Find<EmployeeAttachmentData>("EmployeeAttachmentId", employeeAttachmentId).Data;
			}
			else
			{
				return File.ReadAllBytes(attachment.FileLocation);
			}
		}

		/// <summary>
		/// Creates a Stream representing the data for the attachment. Make sure to dispose of the Stream when you are done with it.
		/// </summary>
		/// <param name="attachment">The attachment whose data should be read.</param>
		/// <returns>A Stream over the attachment's data. Make sure to dispose of the Stream when you are done with it.</returns>
		/// <exception cref="FileNotFoundException">Thrown if the data is on the file system and the file does not exist.</exception>
		/// <exception cref="IOException">Thrown for various I/O related problems when the data is on the file system. Includes DirectoryNotFoundException, PathTooLongException.</exception>
		/// <exception cref="SecurityException">The caller does not have the required permission.</exception>
		/// <exception cref="UnauthorizedAccessException">The access requested is not permitted by the operating system for the specified path, such as when access is Write or ReadWrite and the file or directory is set for read-only access.</exception>
		/// <exception cref="EntityNotFoundException">Thrown if no attachment data record with the supplied attachment Id is found in the database.</exception>
		/// <exception cref="SqlException">An error occurred when trying to read the data from the database.</exception>
		/// <exception cref="DataException">An error occurred when trying to read the data from the database.</exception>
		public Stream OpenDataStream(IAttachment attachment)
		{
			if(String.IsNullOrEmpty(attachment.FileLocation))
			{
				var attachmentData = Entity.Find<EmployeeAttachmentData>("EmployeeAttachmentId", attachment.Id);

				return new MemoryStream(attachmentData.Data);
			}
			else
			{
				return new FileStream(attachment.FileLocation, FileMode.Open, FileAccess.Read, FileShare.Read);
			}
		}
	}
}
