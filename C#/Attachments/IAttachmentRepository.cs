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
	/// <summary>
	/// Abstraction for repository of attachments. 
	/// </summary>
	public interface IAttachmentRepository
	{
		/// <summary>
		/// Determines if an attachment with the supplied id exists in the database.
		/// </summary>
		/// <param name="attachmentId">The id of the attachment to check for.</param>
		/// <returns>Returns true of the attachment is in the database, otherwise false.</returns>
		/// <exception cref="SqlException">An error occurred when trying to read the data from the database.</exception>
		/// <exception cref="DataException">An error occurred when trying to read the data from the database.</exception>
		bool Exists(string attachmentId);

		/// <summary>
		/// Returns an attachment based on its Id.
		/// </summary>
		/// <param name="attachmentId">The id of the attachment to load.</param>
		/// <returns>A loaded instance of the attachment.</returns>
		/// <exception cref="EntityNotFoundException">Thrown if no attachment with the supplied Id is found in the database.</exception>
		/// <exception cref="SqlException">An error occurred when trying to read the data from the database.</exception>
		/// <exception cref="DataException">An error occurred when trying to read the data from the database.</exception>
		IAttachment GetAttachment(string attachmentId);

		/// <summary>
		/// Returns a collection of attachments what all have the same bundle identifier, sorted by their bundle sequence.
		/// </summary>
		/// <param name="bundleIdentifier">The identifier of the bundle to get.</param>
		/// <returns>A collection of attachments with the same bundle identifier sorted by bundle sequence.</returns>
		/// <exception cref="SqlException">An error occurred when trying to read the data from the database.</exception>
		/// <exception cref="DataException">An error occurred when trying to read the data from the database.</exception>
		IEnumerable<IAttachment> GetAttachments(string bundleIdentifier);

		/// <summary>
		/// Returns the actual data for an attachment as an array of bytes in memory.
		/// </summary>
		/// <param name="attachmentId">The id of the attachment to load.</param>
		/// <returns>An in-memory array of bytes containing the raw attachment data.</returns>
		/// <exception cref="FileNotFoundException">Thrown if the data is on the file system and the file does not exist.</exception>
		/// <exception cref="IOException">Thrown for various I/O related problems when the data is on the file system. Includes DirectoryNotFoundException, PathTooLongException.</exception>
		/// <exception cref="SecurityException">The caller does not have the required permission.</exception>
		/// <exception cref="UnauthorizedAccessException">The access requested is not permitted by the operating system for the specified path, such as when access is Write or ReadWrite and the file or directory is set for read-only access.</exception>
		/// <exception cref="EntityNotFoundException">Thrown if no attachment with the supplied Id is found in the database.</exception>
		/// <exception cref="SqlException">An error occurred when trying to read the data from the database.</exception>
		/// <exception cref="DataException">An error occurred when trying to read the data from the database.</exception>
		byte[] GetAttachmentData(string attachmentId);

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
		Stream OpenDataStream(IAttachment attachment);
	}
}
