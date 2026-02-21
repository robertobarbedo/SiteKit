using Microsoft.Extensions.Logging;
using SiteKit.CLI.Services;
using SiteKit.CLI.Services.Shared;
using SiteKit.Types;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace SiteKit.CLI.Services.Navigation;

public class _BuildNavigationTemplates : IRun
{
    private readonly IGraphQLService _graphQLService;
    private readonly ILogger _logger;

    private const string FOUNDATION_PATH = "/sitecore/templates/Foundation";
    private const string SITEKIT_FOLDER_PATH = "/sitecore/templates/Foundation/SiteKit";
    private const string TEMPLATE_FOLDER_ID = "{0437FEE2-44C9-46A6-ABE9-28858D9FEE8C}";
    private const string STANDARD_VALUES_TEMPLATE_ID = "{1930BBEB-7805-471A-A3BE-4858AC7CF696}";

    public _BuildNavigationTemplates(IGraphQLService graphQLService, ILogger logger)
    {
        _graphQLService = graphQLService;
        _logger = logger;
    }

    public void Run(AutoArgs args)
    {
        try
        {
            _logger.LogInformation("Starting navigation template creation...");

            // Step 1: Ensure SiteKit folder exists under Foundation
            var siteKitFolderId = EnsureSiteKitFolderExists(args).Result;
            if (string.IsNullOrEmpty(siteKitFolderId))
            {
                args.IsValid = false;
                args.ValidationMessage = "Failed to create or find SiteKit folder under Foundation";
                return;
            }

            // Step 2: Create Navigation Domain template
            var navDomainTemplateId = CreateNavigationDomainTemplate(args, siteKitFolderId).Result;
            if (string.IsNullOrEmpty(navDomainTemplateId))
            {
                args.IsValid = false;
                args.ValidationMessage = "Failed to create Navigation Domain template";
                return;
            }

            // Step 3: Create Menu template
            var menuTemplateId = CreateMenuTemplate(args, siteKitFolderId).Result;
            if (string.IsNullOrEmpty(menuTemplateId))
            {
                args.IsValid = false;
                args.ValidationMessage = "Failed to create Menu template";
                return;
            }

            // Step 4: Create Menu Item template
            var menuItemTemplateId = CreateMenuItemTemplate(args, siteKitFolderId).Result;
            if (string.IsNullOrEmpty(menuItemTemplateId))
            {
                args.IsValid = false;
                args.ValidationMessage = "Failed to create Menu Item template";
                return;
            }

            // Step 5: Create Standard Values for Navigation Domain template
            var navDomainStdValuesId = CreateStandardValues(args, "Navigation Domain", navDomainTemplateId, SITEKIT_FOLDER_PATH).Result;
            if (string.IsNullOrEmpty(navDomainStdValuesId))
            {
                args.IsValid = false;
                args.ValidationMessage = "Failed to create Standard Values for Navigation Domain template";
                return;
            }

            // Step 6: Create Standard Values for Menu template
            var menuStdValuesId = CreateStandardValues(args, "Menu", menuTemplateId, SITEKIT_FOLDER_PATH).Result;
            if (string.IsNullOrEmpty(menuStdValuesId))
            {
                args.IsValid = false;
                args.ValidationMessage = "Failed to create Standard Values for Menu template";
                return;
            }

            // Step 7: Create Standard Values for Menu Item template
            var menuItemStdValuesId = CreateStandardValues(args, "Menu Item", menuItemTemplateId, SITEKIT_FOLDER_PATH).Result;
            if (string.IsNullOrEmpty(menuItemStdValuesId))
            {
                args.IsValid = false;
                args.ValidationMessage = "Failed to create Standard Values for Menu Item template";
                return;
            }

            // Step 8: Set __Icon on template items
            SetIconOnItem(args, navDomainTemplateId, "Office/32x32/navigate_subitems.png").Wait();
            SetIconOnItem(args, menuTemplateId, "Office/32x32/list_style_bullets.png").Wait();
            SetIconOnItem(args, menuItemTemplateId, "Office/32x32/navigate_minus.png").Wait();

            // Step 9: Update __Masters field for standard values
            UpdateMastersField(args, navDomainStdValuesId, menuTemplateId).Wait();
            UpdateMastersField(args, menuStdValuesId, menuItemTemplateId).Wait();
            UpdateMastersField(args, menuItemStdValuesId, menuItemTemplateId).Wait();

            // Step 10: Update navigation.yaml with template IDs
            UpdateNavigationYaml(args, navDomainTemplateId, menuTemplateId, menuItemTemplateId);

            _logger.LogInformation("✓ Navigation templates created successfully");
            _logger.LogInformation($"  Navigation Domain Template ID: {navDomainTemplateId}");
            _logger.LogInformation($"  Menu Template ID: {menuTemplateId}");
            _logger.LogInformation($"  Menu Item Template ID: {menuItemTemplateId}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating navigation templates");
            args.IsValid = false;
            args.ValidationMessage = $"Error creating navigation templates: {ex.Message}";
        }
    }

    private async Task<string?> EnsureSiteKitFolderExists(AutoArgs args)
    {
        _logger.LogDebug($"Checking if SiteKit folder exists at: {SITEKIT_FOLDER_PATH}");

        // Check if SiteKit folder already exists
        var existingFolder = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, SITEKIT_FOLDER_PATH, verbose: true);
        
        if (existingFolder != null)
        {
            _logger.LogInformation($"✓ SiteKit folder already exists at: {SITEKIT_FOLDER_PATH}");
            return existingFolder.ItemId;
        }

        // SiteKit folder doesn't exist, need to create it
        _logger.LogInformation($"Creating SiteKit folder at: {SITEKIT_FOLDER_PATH}");

        // Get Foundation folder to use as parent
        var foundationFolder = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, FOUNDATION_PATH, verbose: true);
        
        if (foundationFolder == null)
        {
            _logger.LogError($"Foundation path not found: {FOUNDATION_PATH}");
            return null;
        }

        // Create SiteKit folder
        var siteKitFolderId = await _graphQLService.CreateItemAsync(
            args.Endpoint,
            args.AccessToken,
            "SiteKit",
            TEMPLATE_FOLDER_ID,
            foundationFolder.ItemId,
            verbose: true);

        if (siteKitFolderId != null)
        {
            _logger.LogInformation($"✓ Created SiteKit folder (ID: {siteKitFolderId})");
        }

        return siteKitFolderId;
    }

    private async Task<string?> CreateNavigationDomainTemplate(AutoArgs args, string parentId)
    {
        var templatePath = $"{SITEKIT_FOLDER_PATH}/Navigation Domain";
        _logger.LogDebug($"Checking if Navigation Domain template exists at: {templatePath}");

        var existingTemplate = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, templatePath, verbose: true);
        
        if (existingTemplate != null)
        {
            _logger.LogInformation($"✓ Navigation Domain template already exists (ID: {existingTemplate.ItemId})");
            return existingTemplate.ItemId;
        }

        _logger.LogInformation("Creating Navigation Domain template...");
        
        var templateResponse = await _graphQLService.CreateTemplateAsync(
            args.Endpoint,
            args.AccessToken,
            "Navigation Domain",
            parentId,
            sections: null,
            verbose: true);

        if (templateResponse == null)
        {
            _logger.LogError("Failed to create Navigation Domain template");
            return null;
        }

        var createdTemplate = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, templatePath, verbose: true);
        
        if (createdTemplate == null)
        {
            _logger.LogError($"Failed to retrieve created Navigation Domain template at path: {templatePath}");
            return null;
        }

        _logger.LogInformation($"✓ Created Navigation Domain template (ID: {createdTemplate.ItemId})");
        return createdTemplate.ItemId;
    }

    private async Task<string?> CreateMenuTemplate(AutoArgs args, string parentId)
    {
        var templatePath = $"{SITEKIT_FOLDER_PATH}/Menu";
        _logger.LogDebug($"Checking if Menu template exists at: {templatePath}");

        // Check if template already exists
        var existingTemplate = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, templatePath, verbose: true);
        
        if (existingTemplate != null)
        {
            _logger.LogInformation($"✓ Menu template already exists (ID: {existingTemplate.ItemId})");
            return existingTemplate.ItemId;
        }

        // Create the template
        _logger.LogInformation("Creating Menu template...");
        
        var templateResponse = await _graphQLService.CreateTemplateAsync(
            args.Endpoint,
            args.AccessToken,
            "Menu",
            parentId,
            sections: null,
            verbose: true);

        if (templateResponse == null)
        {
            _logger.LogError("Failed to create Menu template");
            return null;
        }

        // Get the created template to retrieve its ID
        var createdTemplate = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, templatePath, verbose: true);
        
        if (createdTemplate == null)
        {
            _logger.LogError($"Failed to retrieve created Menu template at path: {templatePath}");
            return null;
        }

        _logger.LogInformation($"✓ Created Menu template (ID: {createdTemplate.ItemId})");
        return createdTemplate.ItemId;
    }

    private async Task<string?> CreateMenuItemTemplate(AutoArgs args, string parentId)
    {
        var templatePath = $"{SITEKIT_FOLDER_PATH}/Menu Item";
        _logger.LogDebug($"Checking if Menu Item template exists at: {templatePath}");

        // Check if template already exists
        var existingTemplate = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, templatePath, verbose: true);
        
        if (existingTemplate != null)
        {
            _logger.LogInformation($"✓ Menu Item template already exists (ID: {existingTemplate.ItemId})");
            return existingTemplate.ItemId;
        }

        // Create the template with fields
        _logger.LogInformation("Creating Menu Item template...");
        
        var sections = new List<TemplateSection>
        {
            new TemplateSection
            {
                Name = "Content",
                Fields = new List<TemplateField>
                {
                    new TemplateField { Name = "menuLabel", Type = "Single-Line Text" },
                    new TemplateField { Name = "menuLink", Type = "General Link" }
                }
            }
        };

        var templateResponse = await _graphQLService.CreateTemplateAsync(
            args.Endpoint,
            args.AccessToken,
            "Menu Item",
            parentId,
            sections: sections,
            verbose: true);

        if (templateResponse == null)
        {
            _logger.LogError("Failed to create Menu Item template");
            return null;
        }

        // Get the created template to retrieve its ID
        var createdTemplate = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, templatePath, verbose: true);
        
        if (createdTemplate == null)
        {
            _logger.LogError($"Failed to retrieve created Menu Item template at path: {templatePath}");
            return null;
        }

        _logger.LogInformation($"✓ Created Menu Item template (ID: {createdTemplate.ItemId})");
        return createdTemplate.ItemId;
    }

    private async Task<string?> CreateStandardValues(AutoArgs args, string templateName, string templateId, string templateParentPath)
    {
        var standardValuesPath = $"{templateParentPath}/{templateName}/__Standard Values";
        _logger.LogDebug($"Checking if standard values exists at: {standardValuesPath}");

        // Check if standard values already exists
        var existingStandardValues = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, standardValuesPath, verbose: true);
        
        if (existingStandardValues != null)
        {
            _logger.LogInformation($"✓ Standard values for {templateName} already exists");
            return existingStandardValues.ItemId;
        }

        // Create standard values item
        _logger.LogInformation($"Creating standard values for {templateName}...");
        
        var standardValuesId = await _graphQLService.CreateItemAsync(
            args.Endpoint,
            args.AccessToken,
            "__Standard Values",
            templateId, // Use the template itself as the template for standard values
            templateId, // Parent is the template
            verbose: true);

        if (standardValuesId == null)
        {
            _logger.LogError($"Failed to create standard values for {templateName}");
            return null;
        }

        // Update template to reference standard values
        var templateFields = new Dictionary<string, string>
        {
            ["__Standard values"] = standardValuesId
        };
        
        await _graphQLService.UpdateItemAsync(args.Endpoint, args.AccessToken, templateId, templateFields, verbose: true);

        _logger.LogInformation($"✓ Created standard values for {templateName} (ID: {standardValuesId})");
        return standardValuesId;
    }

    private async Task SetIconOnItem(AutoArgs args, string itemId, string icon)
    {
        _logger.LogDebug($"Setting __Icon for item: {itemId}");

        var fields = new Dictionary<string, string>
        {
            ["__Icon"] = icon
        };

        var updateResult = await _graphQLService.UpdateItemAsync(
            args.Endpoint,
            args.AccessToken,
            itemId,
            fields,
            verbose: true);

        if (updateResult != null)
        {
            _logger.LogDebug($"✓ Set __Icon for item: {itemId}");
        }
        else
        {
            _logger.LogWarning($"Failed to set __Icon for item: {itemId}");
        }
    }

    private async Task UpdateMastersField(AutoArgs args, string standardValuesId, string menuItemTemplateId)
    {
        _logger.LogDebug($"Updating __Masters field for standard values: {standardValuesId}");

        var fields = new Dictionary<string, string>
        {
            ["__Masters"] = menuItemTemplateId
        };

        var updateResult = await _graphQLService.UpdateItemAsync(
            args.Endpoint,
            args.AccessToken,
            standardValuesId,
            fields,
            verbose: true);

        if (updateResult != null)
        {
            _logger.LogDebug($"✓ Updated __Masters field for standard values: {standardValuesId}");
        }
        else
        {
            _logger.LogWarning($"Failed to update __Masters field for standard values: {standardValuesId}");
        }
    }

    private void UpdateNavigationYaml(AutoArgs args, string navDomainTemplateId, string menuTemplateId, string menuItemTemplateId)
    {
        try
        {
            _logger.LogInformation("Updating navigation.yaml with template IDs...");

            var navigationYamlPath = Path.Combine(args.Directory!, ".sitekit", args.SiteName, "navigation.yaml");

            if (!File.Exists(navigationYamlPath))
            {
                _logger.LogWarning($"navigation.yaml not found at: {navigationYamlPath}");
                return;
            }

            // Read the current YAML content
            var yamlContent = File.ReadAllText(navigationYamlPath);

            // Parse it
            var deserializer = new DeserializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .IgnoreUnmatchedProperties()
                .Build();

            var navigationConfig = deserializer.Deserialize<NavigationConfig>(yamlContent);

            if (navigationConfig?.Navigation?.Templates == null)
            {
                _logger.LogWarning("Invalid navigation.yaml structure");
                return;
            }

            // Update the template IDs
            navigationConfig.Navigation.Templates.NavigationDomain = navDomainTemplateId;
            navigationConfig.Navigation.Templates.Menu = menuTemplateId;
            navigationConfig.Navigation.Templates.MenuItem = menuItemTemplateId;

            // Serialize back to YAML
            var serializer = new SerializerBuilder()
                .WithNamingConvention(UnderscoredNamingConvention.Instance)
                .Build();

            var updatedYaml = serializer.Serialize(navigationConfig);

            // Preserve the schema header if it exists
            var lines = yamlContent.Split('\n');
            var schemaLine = lines.FirstOrDefault(l => l.TrimStart().StartsWith("# yaml-language-server:"));
            
            if (!string.IsNullOrEmpty(schemaLine))
            {
                updatedYaml = schemaLine + "\n" + updatedYaml;
            }

            // Write back to file
            File.WriteAllText(navigationYamlPath, updatedYaml);

            _logger.LogInformation($"✓ Updated navigation.yaml with template IDs");
            _logger.LogInformation($"  File: {navigationYamlPath}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating navigation.yaml");
            // Don't fail the entire process if YAML update fails
        }
    }
}
