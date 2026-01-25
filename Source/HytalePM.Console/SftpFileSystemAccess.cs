using Renci.SshNet;
using Renci.SshNet.Sftp;

namespace HytalePM.Console;

public class SftpFileSystemAccess : IFileSystemAccess
{
    private readonly SftpClient _sftpClient;
    private bool _disposed;

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

    public Task<List<string>> ListJarFilesAsync(string directory)
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
            .Where(f => f.IsRegularFile && f.Name.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
            .Select(f => f.FullName)
            .ToList();

        return Task.FromResult(files);
    }

    public string GetFileName(string path)
    {
        // Handle both Unix and Windows paths by using the last segment after '/' or '\'
        var lastSlash = Math.Max(path.LastIndexOf('/'), path.LastIndexOf('\\'));
        return lastSlash >= 0 ? path.Substring(lastSlash + 1) : path;
    }

    public void Dispose()
    {
        if (_disposed) return;
        
        if (_sftpClient.IsConnected)
        {
            _sftpClient.Disconnect();
        }
        _sftpClient.Dispose();
        
        _disposed = true;
    }
}
