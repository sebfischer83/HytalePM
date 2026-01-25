using Microsoft.Extensions.Configuration;
using HytalePM.Console;
using Spectre.Console;

// Parse command line arguments
if (args.Length < 1)
{
    AnsiConsole.Write(new FigletText("HytalePM").Color(Color.Cyan1));
    AnsiConsole.MarkupLine("[cyan]Mod Version Checker for CurseForge[/]");
    AnsiConsole.WriteLine();
    
    var panel = new Panel(new Markup(
        "[yellow]Usage:[/] HytalePM.Console <mods-directory> [[config-file]]\n\n" +
        "[yellow]Arguments:[/]\n" +
        "  [green]<mods-directory>[/]  Path to the directory containing mod .jar files\n" +
        "                     Can be a local path or remote path when using SSH\n" +
        "  [green][[config-file]][/]     Optional path to config file (default: config.json)\n\n" +
        "[yellow]Example:[/]\n" +
        "  HytalePM.Console ./mods\n" +
        "  HytalePM.Console /var/minecraft/mods custom-config.json\n\n" +
        "[yellow]SSH Support:[/]\n" +
        "  Configure SSH settings in the config file to access remote directories.\n" +
        "  When SSH is configured, the mods-directory path is interpreted as a\n" +
        "  path on the remote server."))
    {
        Header = new PanelHeader("[bold]Help[/]"),
        Border = BoxBorder.Rounded
    };
    AnsiConsole.Write(panel);
    return 1;
}

string modsDirectory = args[0];
string configFile = args.Length > 1 ? args[1] : "config.json";

// Check if config file exists
if (!File.Exists(configFile))
{
    AnsiConsole.MarkupLine($"[red]Error:[/] Config file not found: [yellow]{configFile}[/]");
    AnsiConsole.WriteLine();
    
    var examplePanel = new Panel(new Markup(
        "[dim]{\n" +
        "  \"CurseForgeApiKey\": \"your-api-key-here\",\n" +
        "  \"BackupDirectory\": \"backups\",\n" +
        "  \"AutoUpdate\": false,\n" +
        "  \"Mods\": [\n" +
        "    {\n" +
        "      \"Name\": \"ModName\",\n" +
        "      \"CurseForgeUrl\": \"https://www.curseforge.com/minecraft/mc-mods/mod-slug\",\n" +
        "      \"ProjectId\": 12345\n" +
        "    }\n" +
        "  ],\n" +
        "  \"Ssh\": {\n" +
        "    \"Host\": \"example.com\",\n" +
        "    \"Port\": 22,\n" +
        "    \"Username\": \"user\",\n" +
        "    \"Password\": \"password\",\n" +
        "    \"PrivateKeyPath\": \"/path/to/key\",\n" +
        "    \"PrivateKeyPassphrase\": \"passphrase\"\n" +
        "  }\n" +
        "}[/]"))
    {
        Header = new PanelHeader("[bold]Example config.json structure[/]"),
        Border = BoxBorder.Rounded
    };
    AnsiConsole.Write(examplePanel);
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
    AnsiConsole.MarkupLine("[red]Error:[/] CurseForgeApiKey is missing in the config file.");
    return 1;
}

if (config.Mods == null || config.Mods.Count == 0)
{
    AnsiConsole.MarkupLine("[red]Error:[/] No mods configured in the config file.");
    return 1;
}

// Create file system access (local or SSH)
IFileSystemAccess fileSystem;
string connectionType;

if (config.Ssh != null && !string.IsNullOrWhiteSpace(config.Ssh.Host))
{
    IFileSystemAccess? tempFileSystem = null;
    string? tempConnectionType = null;
    Exception? connectionError = null;
    
    AnsiConsole.Status()
        .Spinner(Spinner.Known.Dots)
        .Start($"[yellow]Connecting to SSH server: {config.Ssh.Host}:{config.Ssh.Port}...[/]", ctx =>
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(config.Ssh.PrivateKeyPath))
                {
                    // Use private key authentication
                    tempFileSystem = new SftpFileSystemAccess(
                        config.Ssh.Host, 
                        config.Ssh.Port, 
                        config.Ssh.Username,
                        config.Ssh.PrivateKeyPath,
                        config.Ssh.PrivateKeyPassphrase);
                    tempConnectionType = "SSH (private key)";
                }
                else if (!string.IsNullOrWhiteSpace(config.Ssh.Password))
                {
                    // Use password authentication
                    tempFileSystem = new SftpFileSystemAccess(
                        config.Ssh.Host, 
                        config.Ssh.Port, 
                        config.Ssh.Username,
                        config.Ssh.Password);
                    tempConnectionType = "SSH (password)";
                }
                else
                {
                    throw new InvalidOperationException("SSH configuration requires either Password or PrivateKeyPath.");
                }
                
                ctx.Status("[green]SSH connection established successfully.[/]");
                Thread.Sleep(500);
            }
            catch (Exception ex)
            {
                connectionError = ex;
            }
        });
    
    if (connectionError != null)
    {
        AnsiConsole.MarkupLine($"[red]Error connecting to SSH server:[/] {connectionError.Message}");
        return 1;
    }
    
    fileSystem = tempFileSystem!;
    connectionType = tempConnectionType!;
    
    AnsiConsole.MarkupLine("[green]✓[/] SSH connection established");
}
else
{
    // Check local mods directory
    if (!Directory.Exists(modsDirectory))
    {
        AnsiConsole.MarkupLine($"[red]Error:[/] Mods directory not found: [yellow]{modsDirectory}[/]");
        return 1;
    }
    
    fileSystem = new LocalFileSystemAccess();
    connectionType = "Local";
}

// Display header
AnsiConsole.Clear();
AnsiConsole.Write(new FigletText("HytalePM").Color(Color.Cyan1));
AnsiConsole.MarkupLine("[dim]Mod Version Checker for CurseForge[/]");
AnsiConsole.WriteLine();

var infoTable = new Table()
    .Border(TableBorder.Rounded)
    .BorderColor(Color.Grey)
    .AddColumn(new TableColumn("[bold]Setting[/]").Centered())
    .AddColumn(new TableColumn("[bold]Value[/]"))
    .AddRow("[cyan]Configuration[/]", configFile)
    .AddRow("[cyan]Connection Type[/]", connectionType == "Local" ? "[green]Local[/]" : $"[yellow]{connectionType}[/]")
    .AddRow("[cyan]Mods Directory[/]", modsDirectory)
    .AddRow("[cyan]Configured Mods[/]", config.Mods.Count.ToString());

AnsiConsole.Write(infoTable);
AnsiConsole.WriteLine();

try
{
    var checker = new ModVersionChecker(config.CurseForgeApiKey, config);
    var results = await checker.CheckModsInDirectory(modsDirectory, fileSystem);

    AnsiConsole.WriteLine();
    
    // Create results table
    var resultsTable = new Table()
        .Border(TableBorder.Rounded)
        .BorderColor(Color.Grey)
        .AddColumn(new TableColumn("[bold]Mod Name[/]").Centered())
        .AddColumn(new TableColumn("[bold]Status[/]").Centered())
        .AddColumn(new TableColumn("[bold]Local Version[/]"))
        .AddColumn(new TableColumn("[bold]Latest Version[/]"));

    foreach (var result in results)
    {
        string statusMarkup = result.Status switch
        {
            "Up to date" => "[green]✓ Up to date[/]",
            "Update available" => "[yellow]⚠ Update available[/]",
            "Not installed" => "[blue]○ Not installed[/]",
            _ when result.Status.StartsWith("Error") => "[red]✗ Error[/]",
            _ => result.Status
        };

        resultsTable.AddRow(
            result.ModName,
            statusMarkup,
            result.LocalFile ?? "[dim]N/A[/]",
            result.LatestVersion ?? "[dim]N/A[/]"
        );
    }

    AnsiConsole.Write(resultsTable);
    AnsiConsole.WriteLine();

    // Summary panel
    var upToDate = results.Count(r => r.Status == "Up to date");
    var updatesAvailable = results.Count(r => r.Status == "Update available");
    var notInstalled = results.Count(r => r.Status == "Not installed");
    var errors = results.Count(r => r.Status.StartsWith("Error"));

    var summaryGrid = new Grid()
        .AddColumn()
        .AddColumn()
        .AddRow($"[green]Up to date:[/] {upToDate}", $"[yellow]Updates available:[/] {updatesAvailable}")
        .AddRow($"[blue]Not installed:[/] {notInstalled}", $"[red]Errors:[/] {errors}");

    var summaryPanel = new Panel(summaryGrid)
    {
        Header = new PanelHeader("[bold]Summary[/]"),
        Border = BoxBorder.Rounded,
        BorderStyle = new Style(Color.Cyan1)
    };
    
    AnsiConsole.Write(summaryPanel);

    if (updatesAvailable > 0)
    {
        AnsiConsole.WriteLine();
        var updatesPanel = new Panel(
            string.Join("\n", results.Where(r => r.Status == "Update available")
                .Select(r => $"[yellow]•[/] [bold]{r.ModName}[/]\n  Latest: [cyan]{r.LatestVersion}[/]\n  Download: [link]{r.DownloadUrl}[/]")))
        {
            Header = new PanelHeader("[bold yellow]⚠ Mods with updates available[/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Yellow)
        };
        AnsiConsole.Write(updatesPanel);

        // Offer to update if auto-update is enabled or ask user
        if (fileSystem.IsLocal)
        {
            bool shouldUpdate = config.AutoUpdate;
            
            if (!config.AutoUpdate)
            {
                AnsiConsole.WriteLine();
                shouldUpdate = AnsiConsole.Confirm("[yellow]Do you want to update these mods now?[/]", false);
            }

            if (shouldUpdate)
            {
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine($"[cyan]Backup directory:[/] {Path.Combine(modsDirectory, config.BackupDirectory)}");
                AnsiConsole.WriteLine();

                var modsToUpdate = results.Where(r => r.Status == "Update available").ToList();
                var updateResults = await checker.UpdateModsAsync(modsDirectory, fileSystem, modsToUpdate);

                AnsiConsole.WriteLine();

                // Display update results
                var updateTable = new Table()
                    .Border(TableBorder.Rounded)
                    .BorderColor(Color.Grey)
                    .AddColumn(new TableColumn("[bold]Mod[/]").Centered())
                    .AddColumn(new TableColumn("[bold]Status[/]").Centered())
                    .AddColumn(new TableColumn("[bold]Old File[/]"))
                    .AddColumn(new TableColumn("[bold]New File[/]"))
                    .AddColumn(new TableColumn("[bold]Backup[/]"));

                foreach (var result in updateResults)
                {
                    string statusMarkup = result.Success 
                        ? "[green]✓ Success[/]" 
                        : $"[red]✗ Failed[/]\n[dim]{result.Message}[/]";

                    updateTable.AddRow(
                        result.ModName,
                        statusMarkup,
                        result.OldFile ?? "[dim]N/A[/]",
                        result.NewFile ?? "[dim]N/A[/]",
                        result.BackupPath != null ? Path.GetFileName(result.BackupPath) : "[dim]N/A[/]"
                    );
                }

                var updatePanel = new Panel(updateTable)
                {
                    Header = new PanelHeader("[bold green]Update Results[/]"),
                    Border = BoxBorder.Rounded,
                    BorderStyle = new Style(Color.Green)
                };
                AnsiConsole.Write(updatePanel);

                var successCount = updateResults.Count(r => r.Success);
                var failCount = updateResults.Count(r => !r.Success);
                
                AnsiConsole.WriteLine();
                if (successCount > 0)
                {
                    AnsiConsole.MarkupLine($"[green]✓ Successfully updated {successCount} mod(s)[/]");
                }
                if (failCount > 0)
                {
                    AnsiConsole.MarkupLine($"[red]✗ Failed to update {failCount} mod(s)[/]");
                }
            }
        }
        else
        {
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine("[yellow]Note:[/] Automatic updates are only available for local file systems.");
        }
    }

    return 0;
}
catch (Exception ex)
{
    AnsiConsole.WriteException(ex);
    return 1;
}
finally
{
    fileSystem?.Dispose();
}
