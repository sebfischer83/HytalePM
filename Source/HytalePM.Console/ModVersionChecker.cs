using CurseForge.APIClient;
using CurseForge.APIClient.Models.Mods;
using Spectre.Console;

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

        var modFiles = await AnsiConsole.Status()
            .Spinner(Spinner.Known.Dots)
            .StartAsync($"[yellow]Scanning directory for mod files (.jar, .zip)...[/]", async ctx =>
            {
                var files = await fileSystem.ListModFilesAsync(modsDirectory);
                ctx.Status($"[green]Found {files.Count} mod files in {modsDirectory}[/]");
                await Task.Delay(300);
                return files;
            });

        await AnsiConsole.Progress()
            .AutoClear(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[cyan]Checking mods against CurseForge[/]", maxValue: _config.Mods.Count);

                foreach (var mod in _config.Mods)
                {
                    task.Description = $"[cyan]Checking:[/] {mod.Name}";
                    
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
                            task.Increment(1);
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
                            task.Increment(1);
                            continue;
                        }

                        var localModFile = modFiles.FirstOrDefault(f => 
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
                    }
                    catch (Exception ex)
                    {
                        results.Add(new ModCheckResult
                        {
                            ModName = mod.Name,
                            ProjectId = mod.ProjectId,
                            Status = $"Error: {ex.Message}"
                        });
                    }
                    
                    task.Increment(1);
                }
            });

        return results;
    }

    public async Task<List<ModUpdateResult>> UpdateModsAsync(
        string modsDirectory, 
        IFileSystemAccess fileSystem, 
        List<ModCheckResult> modsToUpdate)
    {
        var updateResults = new List<ModUpdateResult>();

        if (!fileSystem.IsLocal)
        {
            AnsiConsole.MarkupLine("[yellow]Warning:[/] Automatic updates are only supported for local file systems.");
            return updateResults;
        }

        var backupDir = Path.Combine(modsDirectory, _config.BackupDirectory);

        await AnsiConsole.Progress()
            .AutoClear(false)
            .Columns(
                new TaskDescriptionColumn(),
                new ProgressBarColumn(),
                new PercentageColumn(),
                new SpinnerColumn())
            .StartAsync(async ctx =>
            {
                var task = ctx.AddTask("[cyan]Updating mods[/]", maxValue: modsToUpdate.Count);

                foreach (var mod in modsToUpdate)
                {
                    task.Description = $"[cyan]Updating:[/] {mod.ModName}";
                    var result = new ModUpdateResult { ModName = mod.ModName };

                    try
                    {
                        // Find the local file
                        var modFiles = await fileSystem.ListModFilesAsync(modsDirectory);
                        var localFile = modFiles.FirstOrDefault(f => 
                            fileSystem.GetFileName(f).Contains(mod.ModName, StringComparison.OrdinalIgnoreCase));

                        // Download new file first
                        if (!string.IsNullOrEmpty(mod.DownloadUrl))
                        {
                            var newFileName = mod.LatestVersion ?? $"{mod.ModName}.jar";
                            // Ensure proper extension
                            if (!newFileName.EndsWith(".jar", StringComparison.OrdinalIgnoreCase) &&
                                !newFileName.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
                            {
                                newFileName += ".jar";
                            }

                            var destinationPath = Path.Combine(modsDirectory, newFileName);
                            
                            // Download to temp location first to ensure success
                            var tempPath = destinationPath + ".tmp";
                            await fileSystem.DownloadFileAsync(mod.DownloadUrl, tempPath);
                            
                            // Only proceed with backup and replacement if download succeeded
                            if (localFile != null)
                            {
                                // Create backup
                                var backupPath = await fileSystem.CreateBackupAsync(localFile, backupDir);
                                result.BackupPath = backupPath;
                                result.OldFile = Path.GetFileName(localFile);

                                // Delete old file only after successful download
                                File.Delete(localFile);
                            }
                            
                            // Move temp file to final location
                            File.Move(tempPath, destinationPath, overwrite: true);
                            
                            result.NewFile = newFileName;
                            result.Success = true;
                            result.Message = "Successfully updated";
                        }
                        else
                        {
                            result.Success = false;
                            result.Message = "No download URL available";
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Success = false;
                        result.Message = $"Error: {ex.Message}";
                    }

                    updateResults.Add(result);
                    task.Increment(1);
                }
            });

        return updateResults;
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

public class ModUpdateResult
{
    public string ModName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? OldFile { get; set; }
    public string? NewFile { get; set; }
    public string? BackupPath { get; set; }
}
