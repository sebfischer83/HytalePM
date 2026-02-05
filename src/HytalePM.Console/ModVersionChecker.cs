using CurseForge.APIClient;
using CurseForge.APIClient.Models.Mods;
using Spectre.Console;
using Serilog;

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
        Log.Information("Scan complete: {FileCount} mod files found in {ModsDirectory}.", modFiles.Count, modsDirectory);

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
                        Log.Information("Checking mod {ModName} (ProjectId={ProjectId}).", mod.Name, mod.ProjectId);
                        var modInfo = await _curseForgeClient.GetModAsync(mod.ProjectId);
                        
                        if (modInfo?.Data == null)
                        {
                            Log.Warning("No mod data returned for ProjectId {ProjectId}.", mod.ProjectId);
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
                            Log.Warning("No files found for ProjectId {ProjectId}.", mod.ProjectId);
                            results.Add(new ModCheckResult
                            {
                                ModName = mod.Name,
                                ProjectId = mod.ProjectId,
                                Status = "Error: No files found for this mod"
                            });
                            task.Increment(1);
                            continue;
                        }

                        var matchKeys = BuildMatchKeys(mod, modInfo.Data);
                        Log.Debug("Match keys for {ModName}: {MatchKeys}", mod.Name, matchKeys);
                        var localModFile = FindLocalModFile(modFiles, fileSystem, matchKeys);
                        Log.Information("Local file match for {ModName}: {LocalFile}", mod.Name, localModFile ?? "none");

                        var result = new ModCheckResult
                        {
                            ModName = mod.Name,
                            ProjectId = mod.ProjectId,
                            LatestVersion = latestFile.DisplayName ?? latestFile.FileName,
                            LatestFileDate = latestFile.FileDate,
                            DownloadUrl = latestFile.DownloadUrl,
                            LocalFile = localModFile != null ? fileSystem.GetFileName(localModFile) : null,
                            MatchKeys = matchKeys
                        };

                        if (localModFile != null)
                        {
                            var localFileName = Path.GetFileNameWithoutExtension(localModFile);
                            var latestFileNames = BuildLatestFileNames(modInfo.Data);
                            
                            if (MatchesAnyLatestFile(localFileName, latestFileNames))
                            {
                                result.Status = "Up to date";
                                Log.Information("{ModName} is up to date (Local={LocalFile}, Latest={LatestFile}).",
                                    mod.Name, localFileName, string.Join(", ", latestFileNames));
                            }
                            else
                            {
                                result.Status = "Update available";
                                Log.Information("{ModName} update available (Local={LocalFile}, Latest={LatestFile}).",
                                    mod.Name, localFileName, string.Join(", ", latestFileNames));
                            }
                        }
                        else
                        {
                            result.Status = "Not installed";
                            Log.Information("{ModName} not installed.", mod.Name);
                        }

                        results.Add(result);
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Error while checking mod {ModName} (ProjectId={ProjectId}).", mod.Name, mod.ProjectId);
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

        var backupDir = CombinePath(modsDirectory, _config.BackupDirectory, fileSystem.IsLocal);

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
                        Log.Information("Updating mod {ModName}.", mod.ModName);
                        // Find the local file
                        var modFiles = await fileSystem.ListModFilesAsync(modsDirectory);
                        var localFile = FindLocalModFile(modFiles, fileSystem, mod.MatchKeys ?? new[] { mod.ModName });
                        Log.Information("Update target match for {ModName}: {LocalFile}", mod.ModName, localFile ?? "none");

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

                            var destinationPath = CombinePath(modsDirectory, newFileName, fileSystem.IsLocal);
                            
                            // Download to temp location first to ensure success
                            var tempPath = destinationPath + ".tmp";
                            Log.Information("Downloading {ModName} from {Url} to {TempPath}.", mod.ModName, mod.DownloadUrl, tempPath);
                            await fileSystem.DownloadFileAsync(mod.DownloadUrl, tempPath);
                            
                            // Only proceed with backup and replacement if download succeeded
                            if (localFile != null)
                            {
                                // Create backup
                                Log.Information("Creating backup for {ModName} from {LocalFile} to {BackupDirectory}.",
                                    mod.ModName, localFile, backupDir);
                                var backupPath = await fileSystem.CreateBackupAsync(localFile, backupDir);
                                result.BackupPath = backupPath;
                                result.OldFile = fileSystem.GetFileName(localFile);

                                // Delete old file only after successful download
                                Log.Information("Deleting old file for {ModName}: {LocalFile}", mod.ModName, localFile);
                                await fileSystem.DeleteFileAsync(localFile);
                            }
                            
                            // Move temp file to final location
                            Log.Information("Moving {TempPath} to {DestinationPath} for {ModName}.",
                                tempPath, destinationPath, mod.ModName);
                            await fileSystem.MoveFileAsync(tempPath, destinationPath);
                            
                            result.NewFile = newFileName;
                            result.Success = true;
                            result.Message = "Successfully updated";
                        }
                        else
                        {
                            Log.Warning("No download URL for {ModName}.", mod.ModName);
                            result.Success = false;
                            result.Message = "No download URL available";
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error(ex, "Update failed for {ModName}.", mod.ModName);
                        result.Success = false;
                        result.Message = $"Error: {ex.Message}";
                    }

                    updateResults.Add(result);
                    task.Increment(1);
                }
            });

        return updateResults;
    }

    private static string NormalizeName(string value)
    {
        var buffer = new char[value.Length];
        var index = 0;

        foreach (var ch in value)
        {
            if (char.IsLetterOrDigit(ch))
            {
                buffer[index++] = char.ToLowerInvariant(ch);
            }
        }

        return new string(buffer, 0, index);
    }

    private static IReadOnlyList<string> BuildMatchKeys(ModInfo configMod, Mod modData)
    {
        var keys = new List<string>();
        AddKey(keys, configMod.Name);
        AddKey(keys, modData.Slug);
        AddKey(keys, modData.Name);

        if (modData.LatestFiles != null)
        {
            foreach (var file in modData.LatestFiles)
            {
                AddKey(keys, file.FileName);
                AddKey(keys, file.DisplayName);
            }
        }

        return keys;
    }

    private static string? FindLocalModFile(
        IReadOnlyList<string> modFiles,
        IFileSystemAccess fileSystem,
        IReadOnlyList<string> matchKeys)
    {
        var normalizedKeys = matchKeys
            .Where(k => !string.IsNullOrWhiteSpace(k))
            .Select(NormalizeName)
            .Where(k => k.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (normalizedKeys.Count == 0)
        {
            return null;
        }

        foreach (var file in modFiles)
        {
            var normalizedFileName = NormalizeName(fileSystem.GetFileName(file));
            if (normalizedKeys.Any(k => IsLooseMatch(normalizedFileName, k)))
            {
                Log.Debug("Matched local file {File} using keys {MatchKeys}.", file, normalizedKeys);
                return file;
            }
        }

        return null;
    }

    private static void AddKey(List<string> keys, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            keys.Add(value);
        }
    }

    private static string CombinePath(string directory, string fileName, bool isLocal)
    {
        if (isLocal)
        {
            return Path.Combine(directory, fileName);
        }

        var left = directory.TrimEnd('/', '\\');
        var right = fileName.TrimStart('/', '\\');
        return $"{left}/{right}";
    }

    private static IReadOnlyList<string> BuildLatestFileNames(Mod modData)
    {
        if (modData.LatestFiles == null)
        {
            return Array.Empty<string>();
        }

        return modData.LatestFiles
            .SelectMany(file => new[] { file.FileName, file.DisplayName })
            .Where(name => !string.IsNullOrWhiteSpace(name))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool MatchesAnyLatestFile(string localFileName, IReadOnlyList<string> latestFileNames)
    {
        if (latestFileNames.Count == 0)
        {
            return false;
        }

        var normalizedLocal = NormalizeName(StripKnownArchiveExtension(localFileName));
        if (string.IsNullOrEmpty(normalizedLocal))
        {
            return false;
        }

        foreach (var candidate in latestFileNames)
        {
            var normalizedCandidate = NormalizeName(StripKnownArchiveExtension(candidate));
            if (normalizedCandidate.Length == 0)
            {
                continue;
            }

            if (string.Equals(normalizedLocal, normalizedCandidate, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static string StripKnownArchiveExtension(string value)
    {
        if (value.EndsWith(".jar", StringComparison.OrdinalIgnoreCase))
        {
            return value[..^4];
        }

        if (value.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
        {
            return value[..^4];
        }

        return value;
    }

    private static bool IsLooseMatch(string normalizedFileName, string normalizedKey)
    {
        if (normalizedFileName.Contains(normalizedKey, StringComparison.OrdinalIgnoreCase) ||
            normalizedKey.Contains(normalizedFileName, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var variant in GetPluralVariants(normalizedKey))
        {
            if (normalizedFileName.Contains(variant, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        foreach (var variant in GetPluralVariants(normalizedFileName))
        {
            if (normalizedKey.Contains(variant, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static IEnumerable<string> GetPluralVariants(string value)
    {
        yield return value;

        if (value.EndsWith("es", StringComparison.OrdinalIgnoreCase) && value.Length > 2)
        {
            yield return value[..^2];
        }

        if (value.EndsWith("s", StringComparison.OrdinalIgnoreCase) && value.Length > 1)
        {
            yield return value[..^1];
        }
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
    public IReadOnlyList<string>? MatchKeys { get; set; }
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
