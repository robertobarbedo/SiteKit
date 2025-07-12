using System.CommandLine;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace SiteKit.CLI;

public class Program
{
    public static async Task<int> Main(string[] args)
    {
        var services = new ServiceCollection();
        ConfigureServices(services);

        var serviceProvider = services.BuildServiceProvider();
        var logger = serviceProvider.GetRequiredService<ILogger<Program>>();

        // Define global options
        var siteOption = new Option<string>(
            aliases: new[] { "-s", "--site" },
            description: "Site name (required)")
        {
            IsRequired = true
        };

        var environmentOption = new Option<string>(
            aliases: new[] { "-n", "--environment" },
            description: "Environment name (default: xmCloud)")
        {
            IsRequired = false
        };
        environmentOption.SetDefaultValue("xmCloud");

        var verboseOption = new Option<bool>(
            aliases: new[] { "-v", "--verbose" },
            description: "Enable verbose logging")
        {
            IsRequired = false
        };

        var rootCommand = new RootCommand("SiteKit CLI - Sitecore YAML deployment tool")
        {
            siteOption,
            environmentOption,
            verboseOption
        };

        // Add handler to root command for direct deployment
        rootCommand.SetHandler(async (site, environment, verbose) =>
        {
            var siteKitService = serviceProvider.GetRequiredService<ISiteKitService>();

            if (verbose)
            {
                logger.LogInformation("Verbose mode enabled");
                logger.LogInformation($"Starting deployment for site: {site}, environment: {environment}");
            }

            try
            {
                await siteKitService.DeployAsync(site, environment, verbose);
                logger.LogInformation("Deployment completed successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Deployment failed");
                Environment.Exit(1);
            }
        }, siteOption, environmentOption, verboseOption);

        // Also add deploy subcommand for backward compatibility
        rootCommand.AddCommand(CreateSiteKitCommand(serviceProvider, logger, siteOption, environmentOption, verboseOption));

        return await rootCommand.InvokeAsync(args);
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(LogLevel.Information);
        });

        services.AddHttpClient();
        services.AddSingleton<ISiteKitService, SiteKitService>();
    }

    private static Command CreateSiteKitCommand(ServiceProvider serviceProvider, ILogger<Program> logger, 
        Option<string> siteOption, Option<string> environmentOption, Option<bool> verboseOption)
    {
        var command = new Command("deploy", "Deploy YAML files to Sitecore");

        command.SetHandler(async (site, environment, verbose) =>
        {
            var siteKitService = serviceProvider.GetRequiredService<ISiteKitService>();

            if (verbose)
            {
                logger.LogInformation("Verbose mode enabled");
                logger.LogInformation($"Starting deployment for site: {site}, environment: {environment}");
            }

            try
            {
                await siteKitService.DeployAsync(site, environment, verbose);
                logger.LogInformation("Deployment completed successfully");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Deployment failed");
                Environment.Exit(1);
            }
        }, siteOption, environmentOption, verboseOption);

        return command;
    }
}

public interface ISiteKitService
{
    Task DeployAsync(string siteName, string environment, bool verbose);
}

public class SiteKitService : ISiteKitService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<SiteKitService> _logger;

    public SiteKitService(HttpClient httpClient, ILogger<SiteKitService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
    }

    public async Task DeployAsync(string siteName, string environment, bool verbose)
    {
        var dir = Directory.GetCurrentDirectory();
        string accessToken = await GetAccessTokenAsync(dir, environment, verbose);

        var endpoint = GetEndpointForEnvironment(environment);
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

        // Process YAML files
        await ProcessYamlFilesAsync(endpoint, dir, siteName, parentId, parentPath, templateId, verbose);

        // Update and retrieve log
        await UpdateLogAsync(endpoint, siteName, verbose);
        var logValue = await GetLogValueAsync(endpoint, siteName, verbose);

        _logger.LogInformation($"Log value: {logValue}");
    }

    private async Task<string> GetAccessTokenAsync(string dir, string environment, bool verbose)
    {
        var tokenJsonPath = Path.Combine(dir, @".sitecore\user.json");

        if (!File.Exists(tokenJsonPath))
        {
            throw new FileNotFoundException($"Token file not found at: {tokenJsonPath}");
        }

        var jsonContent = await File.ReadAllTextAsync(tokenJsonPath);
        var document = JsonDocument.Parse(jsonContent);
        var root = document.RootElement;

        var accessToken = root
            .GetProperty("endpoints")
            .GetProperty(environment)
            .GetProperty("accessToken")
            .GetString();

        if (verbose)
        {
            _logger.LogDebug($"Access token retrieved for environment: {environment}");
        }

        return accessToken ?? throw new InvalidOperationException($"No access token found for environment: {environment}");
    }

    private static string GetEndpointForEnvironment(string environment)
    {
        // This could be made configurable
        return environment switch
        {
            "xmCloud" => "https://xmcloudcm.localhost/sitecore/api/authoring/graphql/v1",
            _ => "https://xmcloudcm.localhost/sitecore/api/authoring/graphql/v1"
        };
    }

    private async Task<string> GetParentIdAsync(string endpoint, string parentPath, bool verbose)
    {
        var query = $@"
query {{
    item(
        where: {{
            database: ""master"",
            path: ""{parentPath.ToLowerInvariant()}""
        }}){{
        itemId
    }}
}}";

        var requestBody = new { query };
        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        if (verbose)
        {
            _logger.LogDebug($"Fetching parent ID for path: {parentPath}");
        }

        var response = await _httpClient.PostAsync(endpoint, content);
        var responseText = await response.Content.ReadAsStringAsync();

        if (verbose)
        {
            _logger.LogDebug($"Parent ID response: {responseText}");
        }

        var document = JsonDocument.Parse(responseText);
        var root = document.RootElement;

        if (root.TryGetProperty("data", out var data) &&
            data.TryGetProperty("item", out var item) &&
            item.ValueKind != JsonValueKind.Null &&
            item.TryGetProperty("itemId", out var itemIdProperty))
        {
            var parentId = itemIdProperty.GetString();
            if (verbose)
            {
                _logger.LogDebug($"Found parent ID: {parentId}");
            }
            return parentId!;
        }

        return string.Empty;
    }

    private async Task ProcessYamlFilesAsync(string endpoint, string dir, string siteName, string parentId, string parentPath, string templateId, bool verbose)
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
            var yamlContent = (await File.ReadAllTextAsync(file))
                .Replace("\"", "\\\"")
                .Replace("\n", "\\n")
                .Replace("\r", "");

            await CreateOrUpdateItemAsync(endpoint, fileName, yamlContent, parentId, parentPath, templateId, verbose);
        }
    }

    private async Task CreateOrUpdateItemAsync(string endpoint, string fileName, string yamlContent, string parentId, string parentPath, string templateId, bool verbose)
    {
        var createMutation = $@"
mutation {{
  createItem(
    input: {{
      database: ""master""
      fields: [
        {{ name: ""YAML"", value: ""{yamlContent}"" }} 
      ]
      language: ""en""
      name: ""{fileName}""
      parent: ""{new Guid(parentId).ToString()}""
      templateId: ""{{{templateId}}}""
    }}
  ) {{
    item {{
      itemId
      path
    }}
  }}
}}";

        var requestBody = new { query = createMutation };
        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        if (verbose)
        {
            _logger.LogDebug($"Creating item from file: {fileName}");
        }

        var response = await _httpClient.PostAsync(endpoint, content);
        var responseText = await response.Content.ReadAsStringAsync();

        if (responseText.Contains("\"errors\""))
        {
            if (verbose)
            {
                _logger.LogDebug($"Create failed. Attempting update for item: {fileName}");
            }

            await UpdateItemAsync(endpoint, fileName, yamlContent, parentPath, verbose);
        }
        else
        {
            if (verbose)
            {
                _logger.LogDebug($"Create response for {fileName}: {responseText}");
            }
        }
    }

    private async Task UpdateItemAsync(string endpoint, string fileName, string yamlContent, string parentPath, bool verbose)
    {
        var itemPath = $"{parentPath}/{fileName}".Replace(" ", "-").ToLowerInvariant();

        var updateMutation = $@"
mutation {{
  updateItem(
    input: {{
      database: ""master""
      path: ""{itemPath}""
      language: ""en""
      fields: [
        {{ name: ""YAML"", value: ""{yamlContent}"" }}
      ]
    }}
  ) {{
    item {{
      itemId
      path
    }}
  }}
}}";

        var requestBody = new { query = updateMutation };
        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        var response = await _httpClient.PostAsync(endpoint, content);
        var responseText = await response.Content.ReadAsStringAsync();

        if (verbose)
        {
            _logger.LogDebug($"Update response for {fileName}: {responseText}");
        }
    }

    private async Task UpdateLogAsync(string endpoint, string siteName, bool verbose)
    {
        var updateLogMutation = $@"
mutation {{
  updateItem(
    input: {{
      database: ""master""
      path: ""/sitecore/system/modules/sitekit/{siteName}""
      language: ""en""
      fields: [
        {{ name: ""Log"", value: """" }}
      ]
    }}
  ) {{
    item {{
      itemId
      path
    }}
  }}
}}";

        var requestBody = new { query = updateLogMutation };
        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        if (verbose)
        {
            _logger.LogDebug("Updating Log field to empty");
        }

        var response = await _httpClient.PostAsync(endpoint, content);
        var responseText = await response.Content.ReadAsStringAsync();

        if (verbose)
        {
            _logger.LogDebug($"Update log response: {responseText}");
        }
    }

    private async Task<string> GetLogValueAsync(string endpoint, string siteName, bool verbose)
    {
        var getLogQuery = $@"
query {{
    item(
        where: {{
            database: ""master"",
            path: ""/sitecore/system/modules/sitekit/{siteName}""
    }}){{
        itemId
        name
        path
        fields(ownFields: true, excludeStandardFields: true) {{
            nodes {{
                name
                value
            }}
        }}
    }}
}}";

        var requestBody = new { query = getLogQuery };
        var json = JsonSerializer.Serialize(requestBody);
        var content = new StringContent(json, System.Text.Encoding.UTF8, "application/json");

        if (verbose)
        {
            _logger.LogDebug("Retrieving Log field value");
        }

        var response = await _httpClient.PostAsync(endpoint, content);
        var responseText = await response.Content.ReadAsStringAsync();

        if (verbose)
        {
            _logger.LogDebug($"Get Log response: {responseText}");
        }

        var document = JsonDocument.Parse(responseText);
        var root = document.RootElement;

        if (root.TryGetProperty("data", out var data) &&
            data.TryGetProperty("item", out var item) &&
            item.ValueKind != JsonValueKind.Null &&
            item.TryGetProperty("fields", out var fields) &&
            fields.TryGetProperty("nodes", out var nodes) &&
            nodes.ValueKind == JsonValueKind.Array)
        {
            foreach (var field in nodes.EnumerateArray())
            {
                if (field.TryGetProperty("name", out var fieldName) &&
                    fieldName.GetString() == "Log" &&
                    field.TryGetProperty("value", out var fieldValue))
                {
                    return fieldValue.GetString() ?? string.Empty;
                }
            }
        }

        return string.Empty;
    }
}
