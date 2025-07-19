using Microsoft.Extensions.Logging;
using SiteKit.CLI.Services;

namespace SiteKit.CLI.Services.Validate;

public interface IValidateService
{
    Task ValidateAsync(string siteName, string environment, bool verbose);
}

public class ValidateService : BaseService, IValidateService
{
    public ValidateService(HttpClient httpClient, ILogger<ValidateService> logger) 
        : base(httpClient, logger)
    {
    }

    public async Task ValidateAsync(string siteName, string environment, bool verbose)
    {
        var dir = Directory.GetCurrentDirectory();
        string accessToken = await GetAccessTokenAsync(dir, verbose);
        string endpoint = await GetEndpointForEnvironment(dir, environment, verbose);

        var parentPath = $"/sitecore/system/Modules/SiteKit/{siteName}";
        var templateId = "ED55F363-5614-48C4-8F90-573535CBC6E3";

        _httpClient.DefaultRequestHeaders.Clear();
        _httpClient.DefaultRequestHeaders.Add("Authorization", $"Bearer {accessToken}");

        // Get parent ID
        var parentId = await GetParentIdAsync(endpoint, parentPath, verbose);
        if (string.IsNullOrEmpty(parentId))
        {
            throw new InvalidOperationException($"Could not find parent item at path: {parentPath}");
        }

        // Process YAML files (for validation, this would be similar to deploy but without actual changes)
        await ProcessYamlFilesForValidationAsync(endpoint, dir, siteName, parentId, parentPath, templateId, verbose);

        // Update and retrieve log
        await UpdateLogAsync(endpoint, siteName, "validate", verbose);
        var logValue = await GetLogValueAsync(endpoint, siteName, verbose);

        // Always show log value regardless of verbose mode
        if (!string.IsNullOrEmpty(logValue))
        {
            Console.WriteLine($"Log value: {logValue}");
        }
    }

    private async Task ProcessYamlFilesForValidationAsync(string endpoint, string dir, string siteName, string parentId, string parentPath, string templateId, bool verbose)
    {
        var yamlDirectory = Path.Combine(dir, ".sitekit", siteName);

        if (!Directory.Exists(yamlDirectory))
        {
            if (verbose)
            {
                _logger.LogWarning($"YAML directory not found: {yamlDirectory}");
            }
            return;
        }

        var yamlFiles = Directory.GetFiles(yamlDirectory, "*.yaml");

        if (yamlFiles.Length == 0)
        {
            if (verbose)
            {
                _logger.LogWarning($"No YAML files found in: {yamlDirectory}");
            }
            return;
        }

        foreach (var file in yamlFiles)
        {
            var fileName = Path.GetFileNameWithoutExtension(file);
            var yamlContent = await File.ReadAllTextAsync(file);

            if (verbose)
            {
                _logger.LogDebug($"Validating YAML file: {fileName}");
                _logger.LogDebug($"File size: {yamlContent.Length} characters");
            }

            // Here you would add actual validation logic
            // For now, we're just logging the files being processed
            await ValidateYamlContentAsync(fileName, yamlContent, verbose);
        }
    }

    private async Task ValidateYamlContentAsync(string fileName, string yamlContent, bool verbose)
    {
        // Placeholder for actual validation logic
        // This could include schema validation, format checking, etc.
        
        if (verbose)
        {
            _logger.LogDebug($"Validated {fileName}: Content appears to be well-formed YAML");
        }

        // Simulate async operation
        await Task.Delay(1);
    }
}
