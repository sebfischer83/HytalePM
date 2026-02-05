using Microsoft.Extensions.Configuration;
using HytalePM.Console;
using Spectre.Console;
using Serilog;
using Serilog.Events;

var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
Directory.CreateDirectory(logDirectory);

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .WriteTo.File(
        Path.Combine(logDirectory, "hytalepm.log"),
        restrictedToMinimumLevel: LogEventLevel.Debug,
        rollingInterval: RollingInterval.Day)
    .CreateLogger();

Log.Information("Starting HytalePM");

IFileSystemAccess? fileSystem = null;

try
{
    // Parse command line arguments
    if (args.Length < 1)
    {
        Log.Warning("Missing mods-directory argument.");
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
    Log.Information("Using mods directory {ModsDirectory} and config {ConfigFile}", modsDirectory, configFile);

    // Check if config file exists
    if (!File.Exists(configFile))
    {
        Log.Error("Config file not found: {ConfigFile}", configFile);
        AnsiConsole.MarkupLine($"[red]Error:[/] Config file not found: [yellow]{configFile}[/]");
        AnsiConsole.WriteLine();

        var exampleJson =
            "{\n" +
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
            "}";

        var examplePanel = new Panel(new Markup($"[dim]{Markup.Escape(exampleJson)}[/]"))
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
        Log.Error("CurseForgeApiKey is missing in config.");
        AnsiConsole.MarkupLine("[red]Error:[/] CurseForgeApiKey is missing in the config file.");
        return 1;
    }

    if (config.Mods == null || config.Mods.Count == 0)
    {
        Log.Error("No mods configured in config file.");
        AnsiConsole.MarkupLine("[red]Error:[/] No mods configured in the config file.");
        return 1;
    }

    Log.Information("Configured mods: {ModCount}", config.Mods.Count);

    // Create file system access (local or SSH)
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
            Log.Error(connectionError, "Error connecting to SSH server.");
            AnsiConsole.MarkupLine($"[red]Error connecting to SSH server:[/] {connectionError.Message}");
            return 1;
        }

        fileSystem = tempFileSystem!;
        connectionType = tempConnectionType!;

        Log.Information("SSH connection established using {ConnectionType}", connectionType);
        AnsiConsole.MarkupLine("[green]✓[/] SSH connection established");
    }
    else
    {
        // Check local mods directory
        if (!Directory.Exists(modsDirectory))
        {
            Log.Error("Mods directory not found: {ModsDirectory}", modsDirectory);
            AnsiConsole.MarkupLine($"[red]Error:[/] Mods directory not found: [yellow]{modsDirectory}[/]");
            return 1;
        }

        fileSystem = new LocalFileSystemAccess();
        connectionType = "Local";
        Log.Information("Using local file system.");
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
    Log.Information("Summary: {UpToDate} up to date, {UpdatesAvailable} updates, {NotInstalled} not installed, {Errors} errors.",
        upToDate, updatesAvailable, notInstalled, errors);

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

    if (updatesAvailable > 0 || notInstalled > 0)
    {
        AnsiConsole.WriteLine();
        var actionableMods = results
            .Where(r => r.Status == "Update available" || r.Status == "Not installed")
            .ToList();
        var updatesPanel = new Panel(
            string.Join("\n", actionableMods
                .Select(r =>
                {
                    var status = r.Status == "Not installed" ? "[blue]Not installed[/]" : "[yellow]Update available[/]";
                    return $"[yellow]•[/] [bold]{r.ModName}[/]\n  Status: {status}\n  Latest: [cyan]{r.LatestVersion}[/]\n  Download: [link]{r.DownloadUrl}[/]";
                })))
        {
            Header = new PanelHeader("[bold yellow]⚠ Mods with updates or missing[/]"),
            Border = BoxBorder.Rounded,
            BorderStyle = new Style(Color.Yellow)
        };
        AnsiConsole.Write(updatesPanel);

        // Offer to update if auto-update is enabled or ask user
        bool shouldUpdate = config.AutoUpdate;

        if (!config.AutoUpdate)
        {
            AnsiConsole.WriteLine();
            shouldUpdate = AnsiConsole.Confirm("[yellow]Do you want to update/install these mods now?[/]", false);
        }

        if (shouldUpdate)
        {
            Log.Information("Starting update/install for {ActionableCount} mods.", actionableMods.Count);
            AnsiConsole.WriteLine();
            var backupDirectory = fileSystem.IsLocal
                ? Path.Combine(modsDirectory, config.BackupDirectory)
                : $"{modsDirectory.TrimEnd('/', '\\')}/{config.BackupDirectory.TrimStart('/', '\\')}";
            AnsiConsole.MarkupLine($"[cyan]Backup directory:[/] {backupDirectory}");
            AnsiConsole.WriteLine();

            var modsToUpdate = actionableMods;
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
                    result.BackupPath != null ? fileSystem.GetFileName(result.BackupPath) : "[dim]N/A[/]"
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
            Log.Information("Update results: {SuccessCount} succeeded, {FailCount} failed.", successCount, failCount);

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

    return 0;
}
catch (Exception ex)
{
    Log.Error(ex, "Unhandled exception.");
    AnsiConsole.WriteException(ex);
    return 1;
}
finally
{
    fileSystem?.Dispose();
    Log.CloseAndFlush();
}
