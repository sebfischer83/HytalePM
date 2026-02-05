using Renci.SshNet;
using Renci.SshNet.Sftp;
using Serilog;

namespace HytalePM.Console;

public class SftpFileSystemAccess : IFileSystemAccess
{
    private readonly SftpClient _sftpClient;
    private readonly HttpClient _httpClient = new();
    private bool _disposed;

    public bool IsLocal => false;

    public SftpFileSystemAccess(string host, int port, string username, string password)
    {
        _sftpClient = new SftpClient(host, port, username, password);
        _sftpClient.Connect();
    }

    public SftpFileSystemAccess(string host, int port, string username, string privateKeyPath, string? passphrase = null)
    {
        var keyFile = string.IsNullOrEmpty(passphrase) 
            ? new PrivateKeyFile(privateKeyPath) 
            : new PrivateKeyFile(privateKeyPath, passphrase);
        
        _sftpClient = new SftpClient(host, port, username, keyFile);
        _sftpClient.Connect();
    }

    public Task<List<string>> ListModFilesAsync(string directory)
    {
        if (!_sftpClient.IsConnected)
        {
            throw new InvalidOperationException("SFTP client is not connected");
        }

        if (!_sftpClient.Exists(directory))
        {
            throw new DirectoryNotFoundException($"Remote directory not found: {directory}");
        }

        var files = _sftpClient.ListDirectory(directory)
            .Where(f => f.IsRegularFile && 
                       (f.Name.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) ||
                        f.Name.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)))
            .Select(f => f.FullName)
            .ToList();
        Log.Debug("SFTP list returned {FileCount} files for {Directory}.", files.Count, directory);

        return Task.FromResult(files);
    }

    public string GetFileName(string path)
    {
        // Handle both Unix and Windows paths by using the last segment after '/' or '\'
        var lastSlash = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
        return lastSlash >= 0 ? path.Substring(lastSlash + 1) : path;
    }

    public Task<string> CreateBackupAsync(string sourceFile, string backupDirectory)
    {
        EnsureConnected();

        sourceFile = NormalizeRemotePath(sourceFile);
        backupDirectory = NormalizeRemotePath(backupDirectory);

        if (!_sftpClient.Exists(sourceFile))
        {
            throw new FileNotFoundException($"Remote source file not found: {sourceFile}");
        }

        if (!_sftpClient.Exists(backupDirectory))
        {
            _sftpClient.CreateDirectory(backupDirectory);
        }

        var fileName = GetFileName(sourceFile);
        var timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");
        var backupFileName = $"{Path.GetFileNameWithoutExtension(fileName)}_{timestamp}{Path.GetExtension(fileName)}";
        var backupPath = CombineRemotePath(backupDirectory, backupFileName);

        int counter = 1;
        while (_sftpClient.Exists(backupPath))
        {
            backupFileName = $"{Path.GetFileNameWithoutExtension(fileName)}_{timestamp}_{counter}{Path.GetExtension(fileName)}";
            backupPath = CombineRemotePath(backupDirectory, backupFileName);
            counter++;
        }

        using var sourceStream = _sftpClient.OpenRead(sourceFile);
        _sftpClient.UploadFile(sourceStream, backupPath);
        Log.Information("Created SFTP backup {BackupPath} from {SourceFile}.", backupPath, sourceFile);

        return Task.FromResult(backupPath);
    }

    public async Task DownloadFileAsync(string url, string destinationPath)
    {
        EnsureConnected();

        destinationPath = NormalizeRemotePath(destinationPath);

        Log.Information("Downloading {Url} to remote path {DestinationPath}.", url, destinationPath);
        using var response = await _httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
        response.EnsureSuccessStatusCode();

        await using var contentStream = await response.Content.ReadAsStreamAsync();
        _sftpClient.UploadFile(contentStream, destinationPath);
    }

    public Task DeleteFileAsync(string path)
    {
        EnsureConnected();
        path = NormalizeRemotePath(path);

        if (_sftpClient.Exists(path))
        {
            _sftpClient.DeleteFile(path);
            Log.Information("Deleted SFTP file {Path}.", path);
        }

        return Task.CompletedTask;
    }

    public Task MoveFileAsync(string sourcePath, string destinationPath)
    {
        EnsureConnected();
        sourcePath = NormalizeRemotePath(sourcePath);
        destinationPath = NormalizeRemotePath(destinationPath);

        if (_sftpClient.Exists(destinationPath))
        {
            _sftpClient.DeleteFile(destinationPath);
            Log.Information("Removed existing SFTP destination {DestinationPath}.", destinationPath);
        }

        _sftpClient.RenameFile(sourcePath, destinationPath);
        Log.Information("Moved SFTP file from {SourcePath} to {DestinationPath}.", sourcePath, destinationPath);
        return Task.CompletedTask;
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        if (_sftpClient.IsConnected)
        {
            _sftpClient.Disconnect();
        }
        _sftpClient.Dispose();
        _httpClient?.Dispose();
        
        _disposed = true;
    }

    private void EnsureConnected()
    {
        if (!_sftpClient.IsConnected)
        {
            throw new InvalidOperationException("SFTP client is not connected");
        }
    }

    private static string NormalizeRemotePath(string path)
    {
        return path.Replace('\\', '/');
    }

    private static string CombineRemotePath(string directory, string fileName)
    {
        var left = NormalizeRemotePath(directory).TrimEnd('/');
        var right = NormalizeRemotePath(fileName).TrimStart('/');
        return $"{left}/{right}";
    }
}
