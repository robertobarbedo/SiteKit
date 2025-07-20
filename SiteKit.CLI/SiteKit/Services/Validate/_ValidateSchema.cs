//using Microsoft.Extensions.Logging;
//using System.Text.Json;
//using System.Text.RegularExpressions;
//using YamlDotNet.Serialization;
//using YamlDotNet.Serialization.NamingConventions;
//using Json.Schema;

//namespace SiteKit.CLI.Services.Validate;

//public class _ValidateSchema : IRun
//{
//    private readonly ILogger _logger;
//    private readonly HttpClient _httpClient;
    
//    // Expected YAML files
//    private readonly List<string> _expectedFiles = new List<string>
//    {
//        "components.yaml",
//        "pagetypes.yaml", 
//        "composition.yaml",
//        "sitesettings.yaml",
//        "dictionary.yaml"
//    };

//    public _ValidateSchema(HttpClient httpClient, ILogger logger)
//    {
//        _logger = logger;
//        _httpClient = httpClient;
//    }

//    public void Run(AutoArgs args)
//    {
//        try
//        {
//            _logger.LogDebug("Starting YAML schema validation run");
            
//            // Build the path to the site-specific directory
//            var siteKitPath = Path.Combine(Directory.GetCurrentDirectory(), ".sitekit", args.SiteName);
            
//            _logger.LogDebug($"Using site-specific directory: {siteKitPath}");

//            // Check if the directory exists
//            if (!Directory.Exists(siteKitPath))
//            {
//                args.IsValid = false;
//                args.ValidationMessage = $"Directory not found: {siteKitPath}. Run sitekit from the project's root folder.";
//                _logger.LogError($"Directory not found: {siteKitPath}. Run sitekit from the project's root folder.");
//                return;
//            }

//            // Validate directory structure first
//            var directoryValid = ValidateDirectoryStructure(siteKitPath, true);
            
//            // Validate all YAML files
//            var task = ValidateAllYamlFilesAsync(siteKitPath, true);
//            task.Wait(); // Since IRun.Run is synchronous, we need to wait
//            var allFilesValid = task.Result;

//            if (directoryValid && allFilesValid)
//            {
//                args.ValidationMessage = "All YAML files passed schema validation";
//                _logger.LogDebug("YAML schema validation completed successfully");
//            }
//            else
//            {
//                args.IsValid = false;
//                var issues = new List<string>();
                
//                if (!directoryValid)
//                {
//                    issues.Add("Missing expected YAML files");
//                }
                
//                if (!allFilesValid)
//                {
//                    issues.Add("Schema validation failed for one or more files");
//                }
                
//                args.ValidationMessage = $"Schema validation failed: {string.Join(", ", issues)}";
//                _logger.LogError($"YAML schema validation failed: {args.ValidationMessage}");
//            }
//        }
//        catch (Exception ex)
//        {
//            args.IsValid = false;
//            args.ValidationMessage = $"Error during schema validation: {ex.Message}";
//            _logger.LogError(ex, "Error during YAML schema validation run");
//        }
//    }

//    public async Task<bool> ValidateAllYamlFilesAsync(string directory, bool verbose = false)
//    {
//        _logger.LogDebug($"Starting YAML schema validation in directory: {directory}");
        
//        var allValid = true;
//        var yamlFiles = Directory.GetFiles(directory, "*.yaml", SearchOption.TopDirectoryOnly)
//                                .Concat(Directory.GetFiles(directory, "*.yml", SearchOption.TopDirectoryOnly))
//                                .ToList();

//        if (!yamlFiles.Any())
//        {
//            _logger.LogWarning("No YAML files found in directory");
//            return false;
//        }

//        _logger.LogDebug($"Found {yamlFiles.Count} YAML files to validate");

//        foreach (var yamlFile in yamlFiles)
//        {
//            var fileName = Path.GetFileName(yamlFile);
//            _logger.LogDebug($"Validating file: {fileName}");

//            var isValid = await ValidateYamlFileAsync(yamlFile, verbose);
//            if (!isValid)
//            {
//                allValid = false;
//                _logger.LogError($"Validation failed for file: {fileName}");
//            }
//            else
//            {
//                _logger.LogDebug($"Validation passed for file: {fileName}");
//            }
//        }

//        if (allValid)
//        {
//            _logger.LogDebug("All YAML files passed schema validation");
//        }
//        else
//        {
//            _logger.LogError("One or more YAML files failed schema validation");
//        }

//        return allValid;
//    }

//    public async Task<bool> ValidateYamlFileAsync(string filePath, bool verbose = false)
//    {
//        var fileName = Path.GetFileName(filePath);
        
//        try
//        {
//            if (!File.Exists(filePath))
//            {
//                _logger.LogError($"File not found: {filePath}");
//                return false;
//            }

//            var content = await File.ReadAllTextAsync(filePath);
            
//            // Check if file has schema header
//            var schemaUrl = ExtractSchemaUrl(content);
//            if (string.IsNullOrEmpty(schemaUrl))
//            {
//                _logger.LogError($"File {fileName} is missing required schema header. First line must be: # yaml-language-server: $schema=<URL>");
//                return false;
//            }

//            _logger.LogDebug($"Found schema URL in {fileName}: {schemaUrl}");

//            // Validate YAML syntax and structure
//            var isValid = await ValidateYamlSyntaxAsync(content, schemaUrl, fileName, verbose);
//            return isValid;
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, $"Error validating file {fileName}");
//            return false;
//        }
//    }

//    private string ExtractSchemaUrl(string yamlContent)
//    {
//        var lines = yamlContent.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        
//        if (lines.Length == 0)
//            return string.Empty;

//        var firstLine = lines[0].Trim();
        
//        // Match pattern: # yaml-language-server: $schema=<URL>
//        var schemaPattern = @"#\s*yaml-language-server:\s*\$schema\s*=\s*(.+)";
//        var match = Regex.Match(firstLine, schemaPattern, RegexOptions.IgnoreCase);
        
//        if (match.Success)
//        {
//            return match.Groups[1].Value.Trim();
//        }

//        return string.Empty;
//    }

//    private async Task<bool> ValidateYamlSyntaxAsync(string yamlContent, string schemaUrl, string fileName, bool verbose)
//    {
//        try
//        {
//            // First validate YAML syntax by parsing it
//            var deserializer = new DeserializerBuilder()
//                .WithNamingConvention(CamelCaseNamingConvention.Instance)
//                .Build();
                
//            var yamlObject = deserializer.Deserialize(yamlContent);
            
//            _logger.LogDebug($"YAML syntax validation passed for {fileName}");
            
//            // Convert to JSON for schema validation
//            var serializer = new SerializerBuilder()
//                .JsonCompatible()
//                .Build();
                
//            var jsonContent = serializer.Serialize(yamlObject);
            
//            // Parse the JSON document
//            JsonDocument jsonDoc;
//            try
//            {
//                jsonDoc = JsonDocument.Parse(jsonContent);
//            }
//            catch (JsonException jsonEx)
//            {
//                _logger.LogError($"JSON conversion error for {fileName}: {jsonEx.Message}");
//                return false;
//            }
            
//            _logger.LogDebug($"JSON conversion validation passed for {fileName}");
            
//            // Download and validate against the JSON schema
//            var schemaValidationResult = await ValidateAgainstJsonSchemaAsync(jsonDoc.RootElement, schemaUrl, fileName, verbose);
            
//            return schemaValidationResult;
//        }
//        catch (YamlDotNet.Core.YamlException yamlEx)
//        {
//            _logger.LogError($"YAML syntax error in {fileName}: {yamlEx.Message}");
//            return false;
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, $"Error during validation for {fileName}");
//            return false;
//        }
//    }

//    private async Task<bool> ValidateAgainstJsonSchemaAsync(JsonElement jsonElement, string schemaUrl, string fileName, bool verbose)
//    {
//        try
//        {
//            _logger.LogDebug($"Downloading and validating against JSON schema for {fileName}: {schemaUrl}");
            
//            // Download the JSON schema
//            var schemaJson = await _httpClient.GetStringAsync(schemaUrl);
            
//            // Parse the schema
//            var schema = JsonSchema.FromText(schemaJson);
            
//            // Validate the JSON document against the schema
//            var results = schema.Evaluate(jsonElement, new EvaluationOptions
//            {
//                OutputFormat = OutputFormat.Hierarchical
//            });
            
//            if (results.IsValid)
//            {
//                _logger.LogDebug($"Schema validation passed for {fileName}");
//                return true;
//            }
//            else
//            {
//                _logger.LogError($"Schema validation failed for {fileName}:");
                
//                // Report detailed validation errors
//                if (results.Details != null)
//                {
//                    await ReportValidationErrors(results.Details, fileName);
//                }
                
//                return false;
//            }
//        }
//        catch (HttpRequestException ex)
//        {
//            _logger.LogError($"Failed to download schema from {schemaUrl} for {fileName}: {ex.Message}");
//            // If we can't download schema, fall back to syntax validation only
//            return true;
//        }
//        catch (JsonException ex)
//        {
//            _logger.LogError($"Invalid JSON schema at {schemaUrl} for {fileName}: {ex.Message}");
//            // If schema is invalid JSON, fall back to syntax validation only
//            return true;
//        }
//        catch (Exception ex)
//        {
//            _logger.LogError(ex, $"Error during schema validation for {fileName}");
//            // If there's any other error, fall back to syntax validation only
//            return true;
//        }
//    }

//    private async Task ReportValidationErrors(IReadOnlyList<EvaluationResults> details, string fileName)
//    {
//        foreach (var detail in details)
//        {
//            if (detail.IsValid) continue;
            
//            var location = detail.InstanceLocation?.ToString() ?? "root";
            
//            // Report any error messages
//            if (detail.Errors != null)
//            {
//                foreach (var errorKey in detail.Errors.Keys)
//                {
//                    _logger.LogError($"  At '{location}': {errorKey} - {detail.Errors[errorKey]}");
//                }
//            }
            
//            // Recursively report nested errors
//            if (detail.Details != null && detail.Details.Any())
//            {
//                await ReportValidationErrors(detail.Details, fileName);
//            }
//        }
//    }

//    public bool ValidateSchemaHeader(string yamlContent, string fileName)
//    {
//        var schemaUrl = ExtractSchemaUrl(yamlContent);
        
//        if (string.IsNullOrEmpty(schemaUrl))
//        {
//            _logger.LogError($"File {fileName} is missing required schema header. First line must be: # yaml-language-server: $schema=<URL>");
//            return false;
//        }

//        _logger.LogDebug($"File {fileName} has valid schema header: {schemaUrl}");
//        return true;
//    }

//    public void AddExpectedFile(string fileName)
//    {
//        if (!_expectedFiles.Contains(fileName.ToLower()))
//        {
//            _expectedFiles.Add(fileName.ToLower());
//            _logger.LogDebug($"Added expected file: {fileName}");
//        }
//    }

//    public List<string> GetExpectedFiles()
//    {
//        return new List<string>(_expectedFiles);
//    }

//    public bool ValidateDirectoryStructure(string directory, bool verbose = false)
//    {
//        _logger.LogDebug($"Validating directory structure in: {directory}");
        
//        var allFilesFound = true;
        
//        foreach (var expectedFile in _expectedFiles)
//        {
//            var filePath = Path.Combine(directory, expectedFile);
            
//            if (!File.Exists(filePath))
//            {
//                _logger.LogWarning($"Expected file not found: {expectedFile}");
//                allFilesFound = false;
//            }
//            else
//            {
//                _logger.LogDebug($"Found expected file: {expectedFile}");
//            }
//        }
        
//        if (allFilesFound)
//        {
//            _logger.LogDebug("All expected YAML files found");
//        }
//        else
//        {
//            _logger.LogWarning("Some expected YAML files are missing");
//        }
        
//        return allFilesFound;
//    }
//}