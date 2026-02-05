using Serilog;

namespace HytalePM.Console;

public class LocalFileSystemAccess : IFileSystemAccess
{
    private readonly HttpClient _httpClient = new();

    public bool IsLocal => true;

    public Task<List<string>> ListModFilesAsync(string directory)
    {
        if (!Directory.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Directory not found: {directory}");
        }

        var modFiles = new List<string>();
        modFiles.AddRange(Directory.GetFiles(directory, "*.jar", SearchOption.TopDirectoryOnly));
        modFiles.AddRange(Directory.GetFiles(directory, "*.zip", SearchOption.TopDirectoryOnly));
        Log.Debug("Local list returned {FileCount} files for {Directory}.", modFiles.Count, directory);
        
        return Task.FromResult(modFiles);
    }

    public string GetFileName(string path)
    {
        return Path.GetFileName(path);
    }

    public Task<string> CreateBackupAsync(string sourceFile, string backupDirectory)
    {
        if (!File.Exists(sourceFile))
        {
            throw new FileNotFoundException($"Source file not found: {sourceFile}");
        }

        // Create backup directory if it doesn't exist
        if (!Directory.Exists(backupDirectory))
        {
            Directory.CreateDirectory(backupDirectory);
        }

        // Create backup filename with timestamp and ensure uniqueness
        var fileName = Path.GetFileName(sourceFile);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupFileName = $"{Path.GetFileNameWithoutExtension(fileName)}_{timestamp}{Path.GetExtension(fileName)}";
        var backupPath = Path.Combine(backupDirectory, backupFileName);
        
        // If file exists, add a counter suffix
        int counter = 1;
        while (File.Exists(backupPath))
        {
            backupFileName = $"{Path.GetFileNameWithoutExtension(fileName)}_{timestamp}_{counter}{Path.GetExtension(fileName)}";
            backupPath = Path.Combine(backupDirectory, backupFileName);
            counter++;
        }

        // Copy file to backup location
        File.Copy(sourceFile, backupPath, overwrite: false);
        Log.Information("Created local backup {BackupPath} from {SourceFile}.", backupPath, sourceFile);

        return Task.FromResult(backupPath);
    }

    public async Task DownloadFileAsync(string url, string destinationPath)
    {
        Log.Information("Downloading {Url} to {DestinationPath}.", url, destinationPath);
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync();
        await using var fileStream = new FileStream(destinationPath, FileMode.Create, FileAccess.Write, FileShare.None);
        await contentStream.CopyToAsync(fileStream);
    }

    public Task DeleteFileAsync(string path)
    {
        if (File.Exists(path))
        {
            File.Delete(path);
            Log.Information("Deleted local file {Path}.", path);
        }

        return Task.CompletedTask;
    }

    public Task MoveFileAsync(string sourcePath, string destinationPath)
    {
        File.Move(sourcePath, destinationPath, overwrite: true);
        Log.Information("Moved local file from {SourcePath} to {DestinationPath}.", sourcePath, destinationPath);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        _httpClient?.Dispose();
    }
}
