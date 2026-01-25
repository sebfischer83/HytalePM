namespace HytalePM.Console;

public class ModConfig
{
    public string CurseForgeApiKey { get; set; } = string.Empty;
    public List<ModInfo> Mods { get; set; } = new();
    public SshConfig? Ssh { get; set; }
    public string BackupDirectory { get; set; } = "backups";
    public bool AutoUpdate { get; set; } = false;
}

public class ModInfo
{
    public string Name { get; set; } = string.Empty;
    public string CurseForgeUrl { get; set; } = string.Empty;
    public int ProjectId { get; set; }
}

public class SshConfig
{
    public string Host { get; set; } = string.Empty;
    public int Port { get; set; } = 22;
    public string Username { get; set; } = string.Empty;
    public string? Password { get; set; }
    public string? PrivateKeyPath { get; set; }
    public string? PrivateKeyPassphrase { get; set; }
}
