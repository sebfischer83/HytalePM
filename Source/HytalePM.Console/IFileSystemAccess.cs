namespace HytalePM.Console;

public interface IFileSystemAccess : IDisposable
{
    /// <summary>
    /// Lists all .jar files in the specified directory
    /// </summary>
    Task<List<string>> ListJarFilesAsync(string directory);
    
    /// <summary>
    /// Gets the filename from a full path
    /// </summary>
    string GetFileName(string path);
}
