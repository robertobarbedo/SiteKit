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
        ConfigureServices(services, false); // Default to non-verbose

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

        var rootCommand = new RootCommand("SiteKit CLI - Sitecore YAML deployment tool");

        // Add deploy command
        rootCommand.AddCommand(CreateSiteKitCommand(serviceProvider, logger, siteOption, environmentOption, verboseOption));

        // Add init command
        rootCommand.AddCommand(CreateInitCommand(serviceProvider, logger, verboseOption));

        return await rootCommand.InvokeAsync(args);
    }

    private static void ConfigureServices(IServiceCollection services, bool verbose)
    {
        services.AddLogging(builder =>
        {
            builder.AddConsole();
            builder.SetMinimumLevel(verbose ? LogLevel.Debug : LogLevel.Warning);
            
            // Suppress HTTP client logs unless verbose
            if (!verbose)
            {
                builder.AddFilter("System.Net.Http.HttpClient", LogLevel.Warning);
            }
        });

        services.AddHttpClient();
        services.AddSingleton<ISiteKitService, SiteKitService>();
    }

    private static Command CreateSiteKitCommand(ServiceProvider serviceProvider, ILogger<Program> logger, 
        Option<string> siteOption, Option<string> environmentOption, Option<bool> verboseOption)
    {
        var command = new Command("deploy", "Deploy YAML files to Sitecore")
        {
            siteOption,
            environmentOption,
            verboseOption
        };

        command.SetHandler(async (site, environment, verbose) =>
        {
            // Create service provider with correct verbose setting
            var services = new ServiceCollection();
            ConfigureServices(services, verbose);
            var verboseServiceProvider = services.BuildServiceProvider();
            var verboseLogger = verboseServiceProvider.GetRequiredService<ILogger<Program>>();
            
            var siteKitService = verboseServiceProvider.GetRequiredService<ISiteKitService>();

            if (verbose)
            {
                verboseLogger.LogInformation("Verbose mode enabled");
                verboseLogger.LogInformation($"Starting deployment for site: {site}, environment: {environment}");
            }

            try
            {
                await siteKitService.DeployAsync(site, environment, verbose);
                if (!verbose)
                {
                    // Only show essential completion message in non-verbose mode
                    //Console.WriteLine("Deployment completed successfully");
                }
                else
                {
                    verboseLogger.LogInformation("Deployment Finished");
                }
            }
            catch (Exception ex)
            {
                verboseLogger.LogError(ex, "Deployment failed");
                Environment.Exit(1);
            }
        }, siteOption, environmentOption, verboseOption);

        return command;
    }

    private static Command CreateInitCommand(ServiceProvider serviceProvider, ILogger<Program> logger, Option<bool> verboseOption)
    {
        var tenantOption = new Option<string>(
            aliases: new[] { "-t", "--tenant" },
            description: "Tenant name (required)")
        {
            IsRequired = true
        };

        var siteOption = new Option<string>(
            aliases: new[] { "-s", "--site" },
            description: "Site name (required)")
        {
            IsRequired = true
        };

        var command = new Command("init", "Initialize SiteKit project with sample YAML files")
        {
            tenantOption,
            siteOption,
            verboseOption
        };

        command.SetHandler(async (tenant, site, verbose) =>
        {
            // Create service provider with correct verbose setting
            var services = new ServiceCollection();
            ConfigureServices(services, verbose);
            var verboseServiceProvider = services.BuildServiceProvider();
            var verboseLogger = verboseServiceProvider.GetRequiredService<ILogger<Program>>();
            
            var siteKitService = verboseServiceProvider.GetRequiredService<ISiteKitService>();

            if (verbose)
            {
                verboseLogger.LogInformation("Verbose mode enabled");
                verboseLogger.LogInformation($"Initializing SiteKit project for tenant: {tenant}, site: {site}");
            }

            try
            {
                await siteKitService.InitializeAsync(tenant, site, verbose);
                if (!verbose)
                {
                    // Only show essential completion message in non-verbose mode
                    Console.WriteLine("SiteKit project initialized successfully");
                }
                else
                {
                    verboseLogger.LogInformation("SiteKit project initialized successfully");
                }
            }
            catch (Exception ex)
            {
                verboseLogger.LogError(ex, "Initialization failed");
                Environment.Exit(1);
            }
        }, tenantOption, siteOption, verboseOption);

        return command;
    }
}

public interface ISiteKitService
{
    Task DeployAsync(string siteName, string environment, bool verbose);
    Task InitializeAsync(string tenant, string site, bool verbose);
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

    public async Task InitializeAsync(string tenant, string site, bool verbose)
    {
        var currentDir = Directory.GetCurrentDirectory();
        var siteKitDir = Path.Combine(currentDir, ".sitekit");
        var siteDir = Path.Combine(siteKitDir, site);

        // Create .sitekit folder if it doesn't exist
        if (!Directory.Exists(siteKitDir))
        {
            Directory.CreateDirectory(siteKitDir);
            if (verbose)
            {
                _logger.LogInformation($"Created .sitekit directory at: {siteKitDir}");
            }
        }

        // Create site subfolder
        if (!Directory.Exists(siteDir))
        {
            Directory.CreateDirectory(siteDir);
            if (verbose)
            {
                _logger.LogInformation($"Created site directory at: {siteDir}");
            }
        }

        // Define files to download
        var filesToDownload = new Dictionary<string, string>
        {
            { "components.yaml", "https://raw.githubusercontent.com/robertobarbedo/SiteKit/refs/heads/main/SiteKit.CLI/SiteKit/samples/components.yaml" },
            { "composition.yaml", "https://raw.githubusercontent.com/robertobarbedo/SiteKit/refs/heads/main/SiteKit.CLI/SiteKit/samples/composition.yaml" },
            { "dictionary.yaml", "https://raw.githubusercontent.com/robertobarbedo/SiteKit/refs/heads/main/SiteKit.CLI/SiteKit/samples/dictionary.yaml" },
            { "pagetypes.yaml", "https://raw.githubusercontent.com/robertobarbedo/SiteKit/refs/heads/main/SiteKit.CLI/SiteKit/samples/pagetypes.yaml" },
            { "sitesettings.yaml", "https://raw.githubusercontent.com/robertobarbedo/SiteKit/refs/heads/main/SiteKit.CLI/SiteKit/samples/sitesettings.yaml" }
        };

        // Download files
        foreach (var file in filesToDownload)
        {
            var filePath = Path.Combine(siteDir, file.Key);
            
            if (verbose)
            {
                _logger.LogInformation($"Downloading {file.Key} from {file.Value}");
            }

            try
            {
                var response = await _httpClient.GetAsync(file.Value);
                response.EnsureSuccessStatusCode();
                
                var content = await response.Content.ReadAsStringAsync();
                await File.WriteAllTextAsync(filePath, content);
                
                if (verbose)
                {
                    _logger.LogInformation($"Downloaded {file.Key} to {filePath}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Failed to download {file.Key}");
                throw;
            }
        }

        // Replace placeholders in sitesettings.yaml
        var siteSettingsPath = Path.Combine(siteDir, "sitesettings.yaml");
        if (File.Exists(siteSettingsPath))
        {
            var content = await File.ReadAllTextAsync(siteSettingsPath);
            
            // Replace placeholders
            content = content
                .Replace("$(name)", $"\"{site}\"")
                .Replace("$(site_path)", $"/sitecore/content/{tenant}/{site}")
                .Replace("$(dictionary_path)", $"/sitecore/content/{tenant}/{site}/Dictionary")
                .Replace("$(site_template_path)", $"/sitecore/templates/Project/{tenant}")
                .Replace("$(datasource_template_path)", $"/sitecore/templates/Feature/{tenant}")
                .Replace("$(rendering_path)", $"/sitecore/layout/Renderings/Feature/{tenant}")
                .Replace("$(placeholder_path)", $"/sitecore/layout/Placeholder Settings/Feature/{tenant}")
                .Replace("$(available_renderings_path)", $"/sitecore/content/{tenant}/{site}/Presentation/Available Renderings")
                .Replace("$(site_placeholder_path)", $"/sitecore/content/{tenant}/{site}/Presentation/Placeholder Settings");

            await File.WriteAllTextAsync(siteSettingsPath, content);
            
            if (verbose)
            {
                _logger.LogInformation($"Updated sitesettings.yaml with tenant: {tenant} and site: {site}");
            }
        }

        if (verbose)
        {
            _logger.LogInformation($"SiteKit project initialized successfully in {siteDir}");
        }
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

        // Always show log value regardless of verbose mode
        if (!string.IsNullOrEmpty(logValue))
        {
            Console.WriteLine($"Log value: {logValue}");
        }
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
