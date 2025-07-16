using System.CommandLine;
using System.IO.Compression;
using System.Linq;
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

        // Add validate command
        rootCommand.AddCommand(CreateValidateCommand(serviceProvider, logger, siteOption, environmentOption, verboseOption));

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
                if (verbose)
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

    private static Command CreateValidateCommand(ServiceProvider serviceProvider, ILogger<Program> logger, 
        Option<string> siteOption, Option<string> environmentOption, Option<bool> verboseOption)
    {
        var command = new Command("validate", "Validate YAML files against Sitecore")
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
                verboseLogger.LogInformation($"Starting validation for site: {site}, environment: {environment}");
            }

            try
            {
                await siteKitService.ValidateAsync(site, environment, verbose);
                if (verbose)
                {
                    verboseLogger.LogInformation("Validation Finished");
                }
            }
            catch (Exception ex)
            {
                verboseLogger.LogError(ex, "Validation failed");
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
    Task ValidateAsync(string siteName, string environment, bool verbose);
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

        // Track user responses
        bool addToSolution = false;
        bool deployToDocker = false;
        
        // Find solution file
        var solutionFile = FindSolutionFile(currentDir, verbose);
        if (!string.IsNullOrEmpty(solutionFile))
        {
            Console.Write($"Do you want to add the client package(dll and config) in solution {solutionFile}? (Y/n): ");
            var response = Console.ReadLine();
            
            if (string.IsNullOrEmpty(response) || response.Trim().ToLowerInvariant() == "y" || response.Trim().ToLowerInvariant() == "yes")
            {
                Console.WriteLine("-");
                Console.WriteLine("A config file 'app_config/include/SiteKit.config' is added.");
                Console.WriteLine("A bin 'app_data/sitekit/SiteKit.dll' is added.");
                Console.WriteLine("A bin 'app_data/sitekit/YamlDotNet.dll' is added.");
                Console.WriteLine("You need to edit your solution to include these files for XM Cloud Deployment.");
                Console.WriteLine("-");
                addToSolution = true;
            }
            else
            {
                if (verbose)
                {
                    _logger.LogInformation("User chose not to add client package to solution");
                }
            }
        }

        // Find docker deploy folder
        var dockerDeployFolder = FindDockerDeployFolder(currentDir, verbose);
        if (!string.IsNullOrEmpty(dockerDeployFolder))
        {
            Console.Write($"Do you want to deploy the package content(dll and config) to the docker deploy folder {dockerDeployFolder}? (Y/n): ");
            var deployResponse = Console.ReadLine();
            
            if (string.IsNullOrEmpty(deployResponse) || deployResponse.Trim().ToLowerInvariant() == "y" || deployResponse.Trim().ToLowerInvariant() == "yes")
            {
                deployToDocker = true;
                Console.WriteLine("-");
                Console.WriteLine("The configs and bins were added to your docker/deploy folder and should be ready to use with the 'containered' CM.");
                Console.WriteLine("-");
            }
            else
            {
                if (verbose)
                {
                    _logger.LogInformation("User chose not to deploy package content to docker deploy folder");
                }
            }
        }

        // If either answer was YES, download and extract the package
        if (addToSolution || deployToDocker)
        {
            await DownloadAndExtractPackageAsync(solutionFile, dockerDeployFolder, addToSolution, deployToDocker, verbose);
        }

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

    private string FindSolutionFile(string currentDir, bool verbose)
    {
        // First check in the "authoring\" folder
        var authoringDir = Path.Combine(currentDir, "authoring");
        if (Directory.Exists(authoringDir))
        {
            var authoringSolutions = Directory.GetFiles(authoringDir, "*.sln");
            if (authoringSolutions.Length > 0)
            {
                if (verbose)
                {
                    _logger.LogInformation($"Found solution file in authoring folder: {authoringSolutions[0]}");
                }
                return authoringSolutions[0];
            }
        }

        // If not found in authoring folder, recursively search for the first *.sln file
        var solutions = Directory.GetFiles(currentDir, "*.sln", SearchOption.AllDirectories);
        if (solutions.Length > 0)
        {
            if (verbose)
            {
                _logger.LogInformation($"Found solution file: {solutions[0]}");
            }
            return solutions[0];
        }

        if (verbose)
        {
            _logger.LogInformation("No solution file found");
        }
        
        return string.Empty;
    }

    private string FindDockerDeployFolder(string currentDir, bool verbose)
    {
        // Recursively search for docker/deploy folder
        var dockerDeployFolders = Directory.GetDirectories(currentDir, "deploy", SearchOption.AllDirectories)
            .Where(dir => Path.GetFileName(Path.GetDirectoryName(dir))?.ToLowerInvariant() == "docker")
            .ToArray();
            
        if (dockerDeployFolders.Length > 0)
        {
            if (verbose)
            {
                _logger.LogInformation($"Found docker deploy folder: {dockerDeployFolders[0]}");
            }
            return dockerDeployFolders[0];
        }

        if (verbose)
        {
            _logger.LogInformation("No docker/deploy folder found");
        }
        
        return string.Empty;
    }

    private async Task DownloadAndExtractPackageAsync(string solutionFile, string dockerDeployFolder, bool addToSolution, bool deployToDocker, bool verbose)
    {
        var tempDir = Path.Combine(Path.GetTempPath(), "SiteKit_Package_" + Guid.NewGuid().ToString("N"));
        
        try
        {
            Directory.CreateDirectory(tempDir);
            
            if (verbose)
            {
                _logger.LogInformation($"Created temp directory: {tempDir}");
            }

            // Download the package
            var packageUrl = "https://github.com/robertobarbedo/SiteKit/raw/refs/heads/main/Releases/SiteKit_Client_Package.zip";
            var packageZipPath = Path.Combine(tempDir, "SiteKit_Client_Package.zip");
            
            if (verbose)
            {
                _logger.LogInformation($"Downloading package from: {packageUrl}");
            }

            using (var response = await _httpClient.GetAsync(packageUrl))
            {
                response.EnsureSuccessStatusCode();
                await using var fileStream = File.Create(packageZipPath);
                await response.Content.CopyToAsync(fileStream);
            }

            if (verbose)
            {
                _logger.LogInformation($"Downloaded package to: {packageZipPath}");
            }

            // Extract the outer zip file
            System.IO.Compression.ZipFile.ExtractToDirectory(packageZipPath, tempDir);
            
            if (verbose)
            {
                _logger.LogInformation("Extracted outer zip file");
            }

            // Extract the inner package.zip
            var innerZipPath = Path.Combine(tempDir, "package.zip");
            if (File.Exists(innerZipPath))
            {
                System.IO.Compression.ZipFile.ExtractToDirectory(innerZipPath, tempDir);
                
                if (verbose)
                {
                    _logger.LogInformation("Extracted inner package.zip");
                }

                // Copy files to respective locations
                var filesDir = Path.Combine(tempDir, "files");
                if (Directory.Exists(filesDir))
                {
                    if (addToSolution && !string.IsNullOrEmpty(solutionFile))
                    {
                        var platformDir = Path.Combine(Path.GetDirectoryName(solutionFile)!, "Platform");
                        CopyDirectory(filesDir, platformDir, verbose);
                        
                        if (verbose)
                        {
                            _logger.LogInformation($"Copied files to solution platform directory: {platformDir}");
                        }
                    }

                    if (deployToDocker && !string.IsNullOrEmpty(dockerDeployFolder))
                    {
                        var dockerPlatformDir = Path.Combine(dockerDeployFolder, "Platform");
                        CopyDirectory(filesDir, dockerPlatformDir, verbose);
                        
                        if (verbose)
                        {
                            _logger.LogInformation($"Copied files to docker deploy platform directory: {dockerPlatformDir}");
                        }
                    }
                }
                else
                {
                    if (verbose)
                    {
                        _logger.LogWarning($"Files directory not found: {filesDir}");
                    }
                }
            }
            else
            {
                if (verbose)
                {
                    _logger.LogWarning($"Inner package.zip not found: {innerZipPath}");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to download and extract package");
            throw;
        }
        finally
        {
            // Clean up temp directory
            if (Directory.Exists(tempDir))
            {
                try
                {
                    Directory.Delete(tempDir, true);
                    if (verbose)
                    {
                        _logger.LogInformation($"Cleaned up temp directory: {tempDir}");
                    }
                }
                catch (Exception ex)
                {
                    if (verbose)
                    {
                        _logger.LogWarning(ex, $"Failed to clean up temp directory: {tempDir}");
                    }
                }
            }
        }
    }

    private void CopyDirectory(string sourceDir, string destinationDir, bool verbose)
    {
        // Create destination directory if it doesn't exist
        Directory.CreateDirectory(destinationDir);

        // Copy all files
        foreach (var file in Directory.GetFiles(sourceDir))
        {
            var fileName = Path.GetFileName(file);
            var destFile = Path.Combine(destinationDir, fileName);
            File.Copy(file, destFile, true);
            
            if (verbose)
            {
                _logger.LogDebug($"Copied file: {fileName} to {destFile}");
            }
        }

        // Copy all subdirectories recursively
        foreach (var directory in Directory.GetDirectories(sourceDir))
        {
            var dirName = Path.GetFileName(directory);
            var destDir = Path.Combine(destinationDir, dirName);
            CopyDirectory(directory, destDir, verbose);
        }
    }

    public async Task DeployAsync(string siteName, string environment, bool verbose)
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

        // Process YAML files
        await ProcessYamlFilesAsync(endpoint, dir, siteName, parentId, parentPath, templateId, verbose);

        // Update and retrieve log
        await UpdateLogAsync(endpoint, siteName, "deploy", verbose);
        var logValue = await GetLogValueAsync(endpoint, siteName, verbose);

        // Always show log value regardless of verbose mode
        if (!string.IsNullOrEmpty(logValue))
        {
            Console.WriteLine($"Log value: {logValue}");
        }
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

        // Process YAML files
        await ProcessYamlFilesAsync(endpoint, dir, siteName, parentId, parentPath, templateId, verbose);

        // Update and retrieve log
        await UpdateLogAsync(endpoint, siteName, "validate", verbose);
        var logValue = await GetLogValueAsync(endpoint, siteName, verbose);

        // Always show log value regardless of verbose mode
        if (!string.IsNullOrEmpty(logValue))
        {
            Console.WriteLine($"Log value: {logValue}");
        }
    }

    private async Task<string> GetAccessTokenAsync(string dir, bool verbose)
    {
        string environment = "xmCloud";

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

    private async Task<string> GetEndpointForEnvironment(string dir, string environment, bool verbose)
    {
        var tokenJsonPath = Path.Combine(dir, @".sitecore\user.json");

        if (!File.Exists(tokenJsonPath))
        {
            throw new FileNotFoundException($"Token file not found at: {tokenJsonPath}");
        }

        var jsonContent = await File.ReadAllTextAsync(tokenJsonPath);
        var document = JsonDocument.Parse(jsonContent);
        var root = document.RootElement;

        var host = root
            .GetProperty("endpoints")
            .GetProperty(environment)
            .GetProperty("host")
            .GetString();

        if (verbose)
        {
            _logger.LogDebug($"Evironment Endpoint retrieved host: {host}");
        }

        string endpoint = host + "/sitecore/api/authoring/graphql/v1";

        return endpoint ?? throw new InvalidOperationException($"No endpoint found for environment: {environment}");
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

    private async Task UpdateLogAsync(string endpoint, string siteName, string logValue, bool verbose)
    {
        var updateLogMutation = $@"
mutation {{
  updateItem(
    input: {{
      database: ""master""
      path: ""/sitecore/system/modules/sitekit/{siteName}""
      language: ""en""
      fields: [
        {{ name: ""Log"", value: ""{logValue}"" }}
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
            _logger.LogDebug($"Updating Log field to: {logValue}");
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
