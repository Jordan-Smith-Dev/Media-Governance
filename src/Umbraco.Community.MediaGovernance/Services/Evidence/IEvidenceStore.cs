namespace Umbraco.Community.MediaGovernance.Services.Evidence;

/// <summary>Stores consent evidence (a signed form or email) for people.</summary>
public interface IEvidenceStore
{
	/// <summary>Checks and stores a file for a person and returns its relative path. The client filename is used only for its extension.</summary>
	Task<string> SaveAsync(Guid personId, string fileName, Stream data);

	/// <summary>Opens a stored file for reading, or returns null if it is missing or the path is not one this store made.</summary>
	Task<Stream?> OpenReadAsync(string path);

	/// <summary>Size in bytes of a stored file, or null if it is missing or the path is not one this store made.</summary>
	Task<long?> GetSizeAsync(string path);

	/// <summary>Deletes a stored file. Does nothing for a path this store did not make.</summary>
	Task DeleteAsync(string path);

	/// <summary>Deletes the whole folder holding a person's evidence (also catches orphaned files).</summary>
	Task DeleteForPersonAsync(Guid personId);
}
