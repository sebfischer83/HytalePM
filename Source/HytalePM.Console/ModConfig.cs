namespace HytalePM.Console;

public class ModConfig
{
    public string CurseForgeApiKey { get; set; } = string.Empty;
    public List<ModInfo> Mods { get; set; } = new();
}

public class ModInfo
{
    public string Name { get; set; } = string.Empty;
    public string CurseForgeUrl { get; set; } = string.Empty;
    public int ProjectId { get; set; }
}
