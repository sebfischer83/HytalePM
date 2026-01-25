namespace HytalePM.Console;

public class LocalFileSystemAccess : IFileSystemAccess
{
    public Task<List<string>> ListJarFilesAsync(string directory)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Directory not found: {directory}");
        }

        var jarFiles = Directory.GetFiles(directory, "*.jar", SearchOption.TopDirectoryOnly);
        return Task.FromResult(jarFiles.ToList());
    }

    public string GetFileName(string path)
    {
        return Path.GetFileName(path);
    }

    public void Dispose()
    {
        // Nothing to dispose for local file system
    }
}
