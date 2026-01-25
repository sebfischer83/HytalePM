using CurseForge.APIClient;
using CurseForge.APIClient.Models.Mods;

namespace HytalePM.Console;

public class ModVersionChecker
{
    private readonly ApiClient _curseForgeClient;
    private readonly ModConfig _config;

    public ModVersionChecker(string apiKey, ModConfig config)
    {
        _curseForgeClient = new ApiClient(apiKey);
        _config = config;
    }

    public async Task<List<ModCheckResult>> CheckModsInDirectory(string modsDirectory, IFileSystemAccess fileSystem)
    {
        var results = new List<ModCheckResult>();

        var jarFiles = await fileSystem.ListJarFilesAsync(modsDirectory);
        System.Console.WriteLine($"Found {jarFiles.Count} jar files in {modsDirectory}");

        foreach (var mod in _config.Mods)
        {
            System.Console.WriteLine($"\nChecking mod: {mod.Name} (Project ID: {mod.ProjectId})");
            
            try
            {
                var modInfo = await _curseForgeClient.GetModAsync(mod.ProjectId);
                
                if (modInfo?.Data == null)
                {
                    results.Add(new ModCheckResult
                    {
                        ModName = mod.Name,
                        ProjectId = mod.ProjectId,
                        Status = "Error: Could not fetch mod information from CurseForge"
                    });
                    continue;
                }

                var latestFile = modInfo.Data.LatestFiles?.OrderByDescending(f => f.FileDate).FirstOrDefault();
                
                if (latestFile == null)
                {
                    results.Add(new ModCheckResult
                    {
                        ModName = mod.Name,
                        ProjectId = mod.ProjectId,
                        Status = "Error: No files found for this mod"
                    });
                    continue;
                }

                var localModFile = jarFiles.FirstOrDefault(f => 
                    fileSystem.GetFileName(f).Contains(mod.Name, StringComparison.OrdinalIgnoreCase));

                var result = new ModCheckResult
                {
                    ModName = mod.Name,
                    ProjectId = mod.ProjectId,
                    LatestVersion = latestFile.DisplayName ?? latestFile.FileName,
                    LatestFileDate = latestFile.FileDate,
                    DownloadUrl = latestFile.DownloadUrl,
                    LocalFile = localModFile != null ? fileSystem.GetFileName(localModFile) : null
                };

                if (localModFile != null)
                {
                    var localFileName = Path.GetFileNameWithoutExtension(localModFile);
                    var latestFileName = Path.GetFileNameWithoutExtension(latestFile.FileName);
                    
                    if (localFileName.Equals(latestFileName, StringComparison.OrdinalIgnoreCase))
                    {
                        result.Status = "Up to date";
                    }
                    else
                    {
                        result.Status = "Update available";
                    }
                }
                else
                {
                    result.Status = "Not installed";
                }

                results.Add(result);
                
                System.Console.WriteLine($"  Status: {result.Status}");
                System.Console.WriteLine($"  Latest: {result.LatestVersion}");
                if (result.LocalFile != null)
                {
                    System.Console.WriteLine($"  Local:  {result.LocalFile}");
                }
            }
            catch (Exception ex)
            {
                System.Console.WriteLine($"  Error: {ex.Message}");
                results.Add(new ModCheckResult
                {
                    ModName = mod.Name,
                    ProjectId = mod.ProjectId,
                    Status = $"Error: {ex.Message}"
                });
            }
        }

        return results;
    }
}

public class ModCheckResult
{
    public string ModName { get; set; } = string.Empty;
    public int ProjectId { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? LatestVersion { get; set; }
    public DateTimeOffset? LatestFileDate { get; set; }
    public string? DownloadUrl { get; set; }
    public string? LocalFile { get; set; }
}
