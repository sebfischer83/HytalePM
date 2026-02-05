namespace HytalePM.Console;

public interface IFileSystemAccess : IDisposable
{
    /// <summary>
    /// Lists all mod files (.jar and .zip) in the specified directory
    /// </summary>
    Task<List<string>> ListModFilesAsync(string directory);
    
    /// <summary>
    /// Gets the filename from a full path
    /// </summary>
    string GetFileName(string path);
    
    /// <summary>
    /// Creates a backup of the specified file
    /// </summary>
    Task<string> CreateBackupAsync(string sourceFile, string backupDirectory);
    
    /// <summary>
    /// Downloads a file from a URL to the specified path
    /// </summary>
    Task DownloadFileAsync(string url, string destinationPath);

    /// <summary>
    /// Deletes a file at the specified path
    /// </summary>
    Task DeleteFileAsync(string path);

    /// <summary>
    /// Moves (or renames) a file to the specified path
    /// </summary>
    Task MoveFileAsync(string sourcePath, string destinationPath);
    
    /// <summary>
    /// Checks if the file system is local (supports backups and downloads)
    /// </summary>
    bool IsLocal { get; }
}
