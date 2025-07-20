# SiteKit

SiteKit is a command-line tool designed for Sitecore SXA projects. It provides scaffolding capabilities to help you build and manage Sitecore components with a simpler interface.

## Prerequisites

- A Sitecore project scaffolded From the Starter Kit

## Important Notes

- Always run the commands from the root folder of your project

## Installation

### Step 1: Download the CLI Package

Download the latest SiteKit CLI package directly from GitHub:

```powershell
Invoke-WebRequest -Uri "https://github.com/robertobarbedo/SiteKit/raw/refs/heads/main/Releases/SiteKit.CLI.1.0.0.nupkg" -OutFile "SiteKit.CLI.1.0.0.nupkg"
```

### Step 2: Install the CLI Tool

Install SiteKit as a global .NET tool:

```powershell
dotnet tool install --global --add-source . SiteKit.CLI
```

## Getting Started

Once installed, navigate to the root folder of your Sitecore project and initialize SiteKit:

```powershell
sitekit init --tenant "MyTenant" --site "MySite"
```

Replace `"MyTenant"` and `"MySite"` with your actual tenant and site names.


To deploy and generate all components and page types in Sitecore.
```powershell
sitekit deploy -s "MySite" 
```

To validate your YAML files
```powershell
sitekit validate -s "MySite" 
```

## XM Cloud

To deploy to XM Cloud you need:

Login on your XM Cloud organization.
```powershell
dotnet sitecore cloud login
```

Connect to the environment. You need this step only once, the environment will be registred in user.json.
```powershell
dotnet sitecore cloud environment connect --environment-id <environment-id>
```

After connection, edit file .\.sitecore\user.json
In the connected environment, change allowWrite to true. 

```powershell
sitekit deploy -n <environment-name> -s "MySite" 
```
