# HytalePM.Console

A .NET 10 console application for checking mod versions against CurseForge.

## Features

- Reads configuration from a JSON file containing mod list and CurseForge API token
- Scans a specified directory for installed mods (.jar files)
- **Supports both local and remote (SSH/SFTP) directory access**
- Checks if mod versions are up to date using the CurseForge API
- Reports which mods need updates with download links

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

The application will:
1. Display progress as it checks each mod
2. Show the status of each mod (Up to date, Update available, Not installed, or Error)
3. Provide a summary at the end
4. List download URLs for mods that have updates available

Example output:
```
HytalePM.Console - Mod Version Checker
Configuration: config.json
Connection Type: SSH (private key)
Mods Directory: /var/minecraft/mods
Configured Mods: 2
================================================================================
Found 5 jar files in /var/minecraft/mods

Checking mod: jei (Project ID: 238222)
  Status: Up to date
  Latest: JEI 1.20.1-15.2.0.27

Checking mod: journeymap (Project ID: 32274)
  Status: Update available
  Latest: JourneyMap 1.20.1-5.9.18
  Local:  journeymap-1.20.1-5.9.15.jar

================================================================================
Summary:
================================================================================
Up to date:        1
Updates available: 1
Not installed:     0
Errors:            0

Mods with updates available:
  - journeymap
    Latest: JourneyMap 1.20.1-5.9.18
    Download: https://www.curseforge.com/api/v1/mods/32274/files/4892156/download
```

## Dependencies

This project uses:
- [CurseForgeCommunity/.NET-APIClient](https://github.com/CurseForgeCommunity/.NET-APIClient) - Official CurseForge API client
- [SSH.NET](https://github.com/sshnet/SSH.NET) - SSH and SFTP client for .NET
- Microsoft.Extensions.Configuration - For configuration management

## License

This project is part of the HytalePM repository.
