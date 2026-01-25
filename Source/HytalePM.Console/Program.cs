using Microsoft.Extensions.Configuration;
using HytalePM.Console;

// Parse command line arguments
if (args.Length < 1)
{
    Console.WriteLine("HytalePM.Console - Mod Version Checker for CurseForge");
    Console.WriteLine("Usage: HytalePM.Console <mods-directory> [config-file]");
    Console.WriteLine();
    Console.WriteLine("Arguments:");
    Console.WriteLine("  <mods-directory>  Path to the directory containing mod .jar files");
    Console.WriteLine("  [config-file]     Optional path to config file (default: config.json)");
    Console.WriteLine();
    Console.WriteLine("Example:");
    Console.WriteLine("  HytalePM.Console ./mods");
    Console.WriteLine("  HytalePM.Console ./mods custom-config.json");
    return 1;
}

string modsDirectory = args[0];
string configFile = args.Length > 1 ? args[1] : "config.json";

// Check if config file exists
if (!File.Exists(configFile))
{
    Console.WriteLine($"Error: Config file not found: {configFile}");
    Console.WriteLine();
    Console.WriteLine("Please create a config.json file with the following structure:");
    Console.WriteLine(@"{
  ""CurseForgeApiKey"": ""your-api-key-here"",
  ""Mods"": [
    {
      ""Name"": ""ModName"",
      ""CurseForgeUrl"": ""https://www.curseforge.com/minecraft/mc-mods/mod-slug"",
      ""ProjectId"": 12345
    }
  ]
}");
    return 1;
}

// Load configuration
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile(configFile, optional: false, reloadOnChange: false)
    .Build();

var config = new ModConfig();
configuration.Bind(config);

// Validate configuration
if (string.IsNullOrWhiteSpace(config.CurseForgeApiKey))
{
    Console.WriteLine("Error: CurseForgeApiKey is missing in the config file.");
    return 1;
}

if (config.Mods == null || config.Mods.Count == 0)
{
    Console.WriteLine("Error: No mods configured in the config file.");
    return 1;
}

// Check mods directory
if (!Directory.Exists(modsDirectory))
{
    Console.WriteLine($"Error: Mods directory not found: {modsDirectory}");
    return 1;
}

Console.WriteLine($"HytalePM.Console - Mod Version Checker");
Console.WriteLine($"Configuration: {configFile}");
Console.WriteLine($"Mods Directory: {modsDirectory}");
Console.WriteLine($"Configured Mods: {config.Mods.Count}");
Console.WriteLine(new string('=', 80));

try
{
    var checker = new ModVersionChecker(config.CurseForgeApiKey, config);
    var results = await checker.CheckModsInDirectory(modsDirectory);

    Console.WriteLine();
    Console.WriteLine(new string('=', 80));
    Console.WriteLine("Summary:");
    Console.WriteLine(new string('=', 80));

    var upToDate = results.Count(r => r.Status == "Up to date");
    var updatesAvailable = results.Count(r => r.Status == "Update available");
    var notInstalled = results.Count(r => r.Status == "Not installed");
    var errors = results.Count(r => r.Status.StartsWith("Error"));

    Console.WriteLine($"Up to date:        {upToDate}");
    Console.WriteLine($"Updates available: {updatesAvailable}");
    Console.WriteLine($"Not installed:     {notInstalled}");
    Console.WriteLine($"Errors:            {errors}");

    if (updatesAvailable > 0)
    {
        Console.WriteLine();
        Console.WriteLine("Mods with updates available:");
        foreach (var result in results.Where(r => r.Status == "Update available"))
        {
            Console.WriteLine($"  - {result.ModName}");
            Console.WriteLine($"    Latest: {result.LatestVersion}");
            Console.WriteLine($"    Download: {result.DownloadUrl}");
        }
    }

    return 0;
}
catch (Exception ex)
{
    Console.WriteLine($"Error: {ex.Message}");
    Console.WriteLine(ex.StackTrace);
    return 1;
}
