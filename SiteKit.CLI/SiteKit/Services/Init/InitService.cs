using Microsoft.Extensions.Logging;
using SiteKit.CLI.Services;

namespace SiteKit.CLI.Services.Init;

public interface IInitService
{
    Task InitializeAsync(string tenant, string site, bool verbose);
}

public class InitService : BaseService, IInitService
{
    public InitService(HttpClient httpClient, ILogger<InitService> logger) 
        : base(httpClient, logger)
    {
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
}
