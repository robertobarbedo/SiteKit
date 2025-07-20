# _ReadYaml Service Implementation

## Overview
The `_ReadYaml` class implements the `IRun` interface and is responsible for reading YAML files from the SiteKit project directory and populating the `AutoArgs.Yamls` dictionary.

## Implementation Details

### Method: `Run(AutoArgs args)`

**Purpose**: Reads all YAML files from the `.sitekit/<sitename>` folder and populates the `args.Yamls` dictionary.

**Process Flow**:
1. **Path Construction**: Builds the path to `.sitekit/<sitename>` using `args.SiteName`
2. **Folder Validation**: Checks if the directory exists
3. **File Discovery**: Finds all `*.yaml` files in the directory (top-level only)
4. **Content Reading**: Reads each file and adds to `args.Yamls` dictionary
5. **Error Handling**: Sets validation status and messages appropriately

### Key Features

✅ **Directory Validation**
- Validates that `.sitekit/<sitename>` folder exists
- Sets `args.IsValid = false` if folder is missing

✅ **File Discovery**
- Uses `Directory.GetFiles()` with `*.yaml` pattern
- Searches only top-level directory (no subdirectories)

✅ **Dictionary Population**
- **Key**: Filename without extension (e.g., "components" for "components.yaml")
- **Value**: Full file content as string
- Clears existing entries before adding new ones

✅ **Error Handling**
- Handles directory access errors
- Handles individual file read errors
- Sets appropriate validation messages
- Uses `args.IsValid` and `args.ValidationMessage` for status

✅ **Comprehensive Logging**
- Success message includes file count and directory path
- Error messages include specific failure details

### Usage Example

```csharp
var args = new AutoArgs("MySite");
var readYaml = new _ReadYaml();

readYaml.Run(args);

if (args.IsValid)
{
    Console.WriteLine(args.ValidationMessage); // "Successfully read 5 YAML files from..."
    
    foreach (var yaml in args.Yamls)
    {
        Console.WriteLine($"File: {yaml.Key}");
        Console.WriteLine($"Content: {yaml.Value.Substring(0, 100)}...");
    }
}
else
{
    Console.WriteLine($"Error: {args.ValidationMessage}");
}
```

### Expected Directory Structure

```
project-root/
├── .sitekit/
│   └── MySite/
│       ├── components.yaml
│       ├── composition.yaml
│       ├── dictionary.yaml
│       ├── pagetypes.yaml
│       └── sitesettings.yaml
```

### Error Scenarios Handled

1. **Directory Not Found**: `.sitekit/<sitename>` folder doesn't exist
2. **No YAML Files**: Directory exists but contains no `.yaml` files
3. **File Access Error**: Permission issues or locked files
4. **Directory Access Error**: Permission issues with parent directory

### Integration

This class is designed to be used as part of a pipeline pattern where:
- `AutoArgs` carries state between pipeline steps
- `IRun.Run()` processes and modifies the args object
- Validation status is tracked via `IsValid` and `ValidationMessage` properties
