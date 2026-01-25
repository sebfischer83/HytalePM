# HytalePM.Console

A .NET 10 console application for checking mod versions against CurseForge.

## Features

- Beautiful terminal UI with colors, tables, and progress indicators (powered by Spectre.Console)
- Reads configuration from a JSON file containing mod list and CurseForge API token
- Scans a specified directory for installed mods (.jar files)
- **Supports both local and remote (SSH/SFTP) directory access**
- Checks if mod versions are up to date using the CurseForge API
- Reports which mods need updates with download links
- Real-time progress tracking during mod checks

## Prerequisites

- .NET 10 SDK
- CurseForge API Key (get one from https://console.curseforge.com/)

## Configuration

Create a `config.json` file with the following structure:

```json
{
  "CurseForgeApiKey": "your-curseforge-api-key-here",
  "Mods": [
    {
      "Name": "jei",
      "CurseForgeUrl": "https://www.curseforge.com/minecraft/mc-mods/jei",
      "ProjectId": 238222
    },
    {
      "Name": "journeymap",
      "CurseForgeUrl": "https://www.curseforge.com/minecraft/mc-mods/journeymap",
      "ProjectId": 32274
    }
  ],
  "Ssh": {
    "Host": "your-server.com",
    "Port": 22,
    "Username": "your-username",
    "Password": "your-password",
    "PrivateKeyPath": "/path/to/private/key",
    "PrivateKeyPassphrase": "key-passphrase-if-needed"
  }
}
```

**Note:** The `Ssh` section is optional. If omitted, the application will access the local file system. For SSH authentication, provide either:
- `Password` for password-based authentication
- `PrivateKeyPath` (and optionally `PrivateKeyPassphrase`) for key-based authentication

### Finding Project IDs

You can find the Project ID in the CurseForge URL or on the mod's page:
- URL format: `https://www.curseforge.com/minecraft/mc-mods/{mod-slug}`
- The Project ID is visible on the mod's "About Project" page

Alternatively, you can use the CurseForge API to search for mods by name.

## Building

```bash
cd Source/HytalePM.Console
dotnet build
```

## Usage

```bash
dotnet run --project Source/HytalePM.Console -- <mods-directory> [config-file]
```

Or after building:

```bash
./Source/HytalePM.Console/bin/Debug/net10.0/HytalePM.Console <mods-directory> [config-file]
```

### Arguments

- `<mods-directory>`: Path to the directory containing mod .jar files (required)
  - For local access: use a local path like `./mods` or `/path/to/mods`
  - For SSH access: use the remote path on the SSH server like `/var/minecraft/mods`
- `[config-file]`: Path to the configuration file (optional, defaults to `config.json`)

### Examples

**Local Directory Access:**

Check mods in the `./mods` directory using the default `config.json`:
```bash
dotnet run --project Source/HytalePM.Console -- ./mods
```

Check mods using a custom config file:
```bash
dotnet run --project Source/HytalePM.Console -- ./mods my-config.json
```

**Remote Directory Access (SSH/SFTP):**

Configure the `Ssh` section in your `config.json`, then specify the remote path:
```bash
dotnet run --project Source/HytalePM.Console -- /var/minecraft/mods
```

The application will automatically use SSH when the `Ssh` configuration is present.

## Output

The application features a beautiful terminal UI with:
1. ASCII art header with the application name
2. Organized information table showing configuration and connection details
3. Real-time progress bar during mod checking
4. Color-coded results table with status indicators
5. Summary panel with statistics
6. Detailed update information for mods requiring updates

The interface uses:
- ✓ Green for up-to-date mods
- ⚠ Yellow for mods with available updates
- ○ Blue for mods not installed
- ✗ Red for errors

Example output:
```
_   _           _             _          ____    __  __ 
 | | | |  _   _  | |_    __ _  | |   ___  |  _ \  |  \/  |
 | |_| | | | | | | __|  / _` | | |  / _ \ | |_) | | |\/| |
 |  _  | | |_| | | |_  | (_| | | | |  __/ |  __/  | |  | |
 |_| |_|  \__, |  \__|  \__,_| |_|  \___| |_|     |_|  |_|
          |___/                                           
Mod Version Checker for CurseForge

╭───────────────┬─────────────────────────╮
│    Setting    │          Value          │
├───────────────┼─────────────────────────┤
│ Configuration │ config.json             │
│ Connection    │ SSH (private key)       │
│ Mods Directory│ /var/minecraft/mods     │
│ Configured    │ 2                       │
╰───────────────┴─────────────────────────╯
Checking mods against CurseForge... [Progress Bar] 50%

╭────────────┬────────────────────┬──────────────────┬────────────────────╮
│  Mod Name  │       Status       │  Local Version   │  Latest Version    │
├────────────┼────────────────────┼──────────────────┼────────────────────┤
│ jei        │ ✓ Up to date       │ jei-1.20.1-...   │ JEI 1.20.1-...     │
│ journeymap │ ⚠ Update available │ journeymap-...   │ JourneyMap 1.20... │
╰────────────┴────────────────────┴──────────────────┴────────────────────╯

╭─Summary────────────────────────────────────────╮
│ Up to date: 1        Updates available: 1      │
│ Not installed: 0     Errors: 0                 │
╰────────────────────────────────────────────────╯

╭─⚠ Mods with updates available──────────────────────────────────╮
│ • journeymap                                                    │
│   Latest: JourneyMap 1.20.1-5.9.18                             │
│   Download: https://www.curseforge.com/api/v1/mods/...         │
╰─────────────────────────────────────────────────────────────────╯
```

## Dependencies

This project uses:
- [CurseForgeCommunity/.NET-APIClient](https://github.com/CurseForgeCommunity/.NET-APIClient) - Official CurseForge API client
- [SSH.NET](https://github.com/sshnet/SSH.NET) - SSH and SFTP client for .NET
- [Spectre.Console](https://github.com/spectreconsole/spectre.console) - Beautiful terminal UI library
- Microsoft.Extensions.Configuration - For configuration management

## License

This project is part of the HytalePM repository.
