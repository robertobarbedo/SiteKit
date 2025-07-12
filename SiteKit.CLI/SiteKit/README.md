# SiteKit CLI Plugin

A Sitecore CLI plugin for deploying YAML files to Sitecore instances.

## Installation

### Option 1: Install as a .NET Global Tool

1. Build and pack the project:
   ```bash
   dotnet pack -c Release
   ```

2. Install the tool globally:
   ```bash
   dotnet tool install -g SiteKit.CLI
   ```

### Option 2: Install as Sitecore CLI Plugin

1. Build the project:
   ```bash
   dotnet build -c Release
   ```

2. Install the plugin in Sitecore CLI:
   ```bash
   dotnet sitecore plugin add -n SiteKit.CLI
   ```

## Usage

### As a standalone tool:
```bash
sitekit deploy -s MySite -n xmCloud -v
```

### As a Sitecore CLI plugin:
```bash
dotnet sitecore sitekit deploy -s MySite -n xmCloud -v
```

## Parameters

- `-s, --site` (required): Site name
- `-n, --environment` (optional): Environment name (default: xmCloud)
- `-v, --verbose` (optional): Enable verbose logging

## Examples

### Deploy to default environment (xmCloud):
```bash
dotnet sitecore sitekit deploy -s MySite
```

### Deploy to specific environment with verbose logging:
```bash
dotnet sitecore sitekit deploy -s MySite -n production -v
```

### Deploy using short parameter names:
```bash
dotnet sitecore sitekit deploy -s MySite -n xmCloud -v
```

## Prerequisites

1. **Sitecore CLI** must be installed and configured
2. **Authentication** - Ensure you're logged in to the target environment:
   ```bash
   dotnet sitecore login
   ```
3. **YAML Files** - Place your YAML files in the `.sitekit/{SiteName}` directory relative to your project root

## Directory Structure

```
YourProject/
├── .sitecore/
│   └── user.json           # Authentication tokens
├── .sitekit/
│   └── MySite/             # Site-specific YAML files
│       ├── components.yaml
│       ├── dictionary.yaml
│       └── other.yaml
└── ...
```

## Configuration

The plugin reads authentication tokens from `.sitecore/user.json` file, which is automatically created when you log in to Sitecore CLI.

## Troubleshooting

### "Could not find parent item" error
- Ensure the parent item `/sitecore/system/Modules/SiteKit/{SiteName}` exists in your Sitecore instance
- Check that you have the correct permissions to create items in that location

### "Token file not found" error
- Run `dotnet sitecore login` to authenticate with your Sitecore instance
- Ensure the `.sitecore/user.json` file exists in your project directory

### "No YAML files found" error
- Check that your YAML files are placed in the correct directory: `.sitekit/{SiteName}/`
- Ensure the files have the `.yaml` extension

## Development

### Building the project:
```bash
dotnet build
```

### Running locally:
```bash
dotnet run -- deploy -s MySite -n xmCloud -v
```

### Testing the plugin:
```bash
# Install locally for testing
dotnet tool install -g --add-source ./bin/Release SiteKit.CLI

# Test the command
sitekit deploy -s TestSite -v
```

## License

This project is licensed under the MIT License. 