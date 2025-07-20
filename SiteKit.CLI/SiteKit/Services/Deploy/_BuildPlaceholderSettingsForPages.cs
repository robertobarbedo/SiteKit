using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SiteKit.Types;
using SiteKit.CLI.Services;

namespace SiteKit.CLI.Services.Deploy
{
    public class _BuildPlaceholderSettingsForPages : IRun
    {
        private readonly IGraphQLService _graphQLService;
        private readonly ILogger _logger;

        public _BuildPlaceholderSettingsForPages(IGraphQLService graphQLService, ILogger logger)
        {
            _graphQLService = graphQLService;
            _logger = logger;
        }

        public void Run(AutoArgs args)
        {
            // This is now async but IRun interface expects sync - we'll use Task.Run to handle it
            Task.Run(async () => await ProcessAsync(args)).Wait();
        }

        public async Task ProcessAsync(AutoArgs args)
        {
            var pages = args.CompositionConfig.Composition.Pages;
            foreach (var key in pages.Keys)
            {
                var ph = pages[key];
                foreach (var phKey in ph.Keys)
                {
                    await CreateOrUpdateAsync(args, key, phKey, pages[key][phKey]);
                }
            }
        }

        private async Task CreateOrUpdateAsync(AutoArgs args, string pageTypeName, string phName, List<string> pageTypes)
        {
            try
            {
                var pageType = args.PageTypesConfig.PageTypes.Where(c => c.Name == pageTypeName).FirstOrDefault();
                if (pageType == null)
                {
                    _logger.LogDebug($"Page type not found: {pageTypeName}");
                    return;
                }

                var site = args.SiteConfig.Site;
                var name = pageType.Name.ToLower().Replace(" ", "-") + "-" + phName.ToLowerInvariant();

                // Create global placeholder setting (under Layout)
                await CreateGlobalPlaceholderSettingAsync(args, pageType, name, pageTypes);

                // Create site-specific placeholder setting (under /Presentation/Placeholder Settings)
                await CreateSitePlaceholderSettingAsync(args, pageType, name, pageTypes);

                // Update rendering with placeholder settings
                await UpdateRenderingPlaceholdersAsync(args, pageType, name);

                _logger.LogDebug($"Successfully processed placeholder settings for page type: {pageTypeName}, placeholder: {phName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing placeholder settings for page type: {pageTypeName}, placeholder: {phName}");
                args.IsValid = false;
                args.ValidationMessage = $"Error processing placeholder settings for page type: {pageTypeName}, placeholder: {phName} - {ex.Message}";
                throw;
            }
        }

        private async Task CreateGlobalPlaceholderSettingAsync(AutoArgs args, PageType pageType, string name, List<string> pageTypes)
        {
            try
            {
                var site = args.SiteConfig.Site;
                var globalPlaceholderPath = $"{site.PlaceholderPath}/{name}";

                // Try to retrieve the global placeholder setting by path
                var existingPlaceholder = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, globalPlaceholderPath, verbose: true);

                string placeholderSettingId;

                if (existingPlaceholder == null)
                {
                    _logger.LogDebug($"Creating global placeholder setting: {name} at path: {globalPlaceholderPath}");

                    // Get the parent placeholder folder
                    var placeholderFolder = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, site.PlaceholderPath, verbose: true);

                    if (placeholderFolder == null)
                    {
                        _logger.LogError($"Placeholder folder not found at path: {site.PlaceholderPath}");
                        args.IsValid = false;
                        args.ValidationMessage = $"Placeholder folder not found at path: {site.PlaceholderPath}";
                        return;
                    }

                    // Create the placeholder setting using the Placeholder Settings template
                    var createdPlaceholderId = await _graphQLService.CreateItemAsync(
                        args.Endpoint,
                        args.AccessToken,
                        name,
                        "{D2A6884C-04D5-4089-A64E-D27CA9D68D4C}", // Placeholder Settings template ID
                        placeholderFolder.ItemId,
                        verbose: true);

                    if (createdPlaceholderId == null)
                    {
                        _logger.LogError($"Failed to create global placeholder setting: {name}");
                        args.IsValid = false;
                        args.ValidationMessage = $"Failed to create global placeholder setting: {name}";
                        return;
                    }

                    placeholderSettingId = createdPlaceholderId;
                }
                else
                {
                    placeholderSettingId = existingPlaceholder.ItemId;
                    _logger.LogDebug($"Using existing global placeholder setting: {name} (ID: {placeholderSettingId})");
                }

                // Update placeholder setting fields
                var placeholderFields = new Dictionary<string, string>();
                placeholderFields["Placeholder Key"] = name + "-{*}";
                placeholderFields["Allowed Controls"] = string.Join("|", await GetControlsAsync(args, pageTypes));

                await _graphQLService.UpdateItemAsync(args.Endpoint, args.AccessToken, placeholderSettingId, placeholderFields, verbose: true);

                // Set default fields
                await SetDefaultFieldsAsync(args, placeholderSettingId);

                _logger.LogDebug($"Successfully processed global placeholder setting: {name}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating global placeholder setting: {name}");
                throw;
            }
        }

        private async Task CreateSitePlaceholderSettingAsync(AutoArgs args, PageType pageType, string name, List<string> pageTypes)
        {
            try
            {
                var site = args.SiteConfig.Site;
                var sitePlaceholderPath = $"{site.SitePath}/Presentation/Placeholder Settings/{name}";

                // Try to retrieve the site placeholder setting by path
                var existingPlaceholder = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, sitePlaceholderPath, verbose: true);

                string placeholderSettingId;

                if (existingPlaceholder == null)
                {
                    _logger.LogDebug($"Creating site placeholder setting: {name} at path: {sitePlaceholderPath}");

                    // Get the parent placeholder settings folder
                    var placeholderSettingsPath = $"{site.SitePath}/Presentation/Placeholder Settings";
                    var placeholderSettingsFolder = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, placeholderSettingsPath, verbose: true);

                    if (placeholderSettingsFolder == null)
                    {
                        _logger.LogError($"Site placeholder settings folder not found at path: {placeholderSettingsPath}");
                        args.IsValid = false;
                        args.ValidationMessage = $"Site placeholder settings folder not found at path: {placeholderSettingsPath}";
                        return;
                    }

                    // Create the site placeholder setting using the Placeholder Settings template
                    var createdPlaceholderId = await _graphQLService.CreateItemAsync(
                        args.Endpoint,
                        args.AccessToken,
                        name,
                        "{D2A6884C-04D5-4089-A64E-D27CA9D68D4C}", // Placeholder Settings template ID
                        placeholderSettingsFolder.ItemId,
                        verbose: true);

                    if (createdPlaceholderId == null)
                    {
                        _logger.LogError($"Failed to create site placeholder setting: {name}");
                        args.IsValid = false;
                        args.ValidationMessage = $"Failed to create site placeholder setting: {name}";
                        return;
                    }

                    placeholderSettingId = createdPlaceholderId;
                }
                else
                {
                    placeholderSettingId = existingPlaceholder.ItemId;
                    _logger.LogDebug($"Using existing site placeholder setting: {name} (ID: {placeholderSettingId})");
                }

                // Update placeholder setting fields (note: site version uses "*" instead of "-{*}")
                var placeholderFields = new Dictionary<string, string>();
                placeholderFields["Placeholder Key"] = name + "*";
                placeholderFields["Allowed Controls"] = string.Join("|", await GetControlsAsync(args, pageTypes));

                await _graphQLService.UpdateItemAsync(args.Endpoint, args.AccessToken, placeholderSettingId, placeholderFields, verbose: true);

                // Set default fields
                await SetDefaultFieldsAsync(args, placeholderSettingId);

                _logger.LogDebug($"Successfully processed site placeholder setting: {name}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating site placeholder setting: {name}");
                throw;
            }
        }

        private async Task UpdateRenderingPlaceholdersAsync(AutoArgs args, PageType pageType, string placeholderName)
        {
            try
            {
                var site = args.SiteConfig.Site;
                var renderingPath = $"{site.RenderingPath}/Pages/{pageType.Name}";

                // Get the rendering item
                var rendering = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, renderingPath, verbose: true);

                if (rendering == null)
                {
                    _logger.LogDebug($"Page rendering not found at path: {renderingPath}");
                    return;
                }

                // Get the global placeholder setting ID by path
                var globalPlaceholderPath = $"{site.PlaceholderPath}/{placeholderName}";
                var globalPlaceholder = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, globalPlaceholderPath, verbose: true);

                if (globalPlaceholder == null)
                {
                    _logger.LogDebug($"Global placeholder setting not found at path: {globalPlaceholderPath}");
                    return;
                }

                // Check if the rendering already has this placeholder setting
                var existingPlaceholders = rendering.Fields?.FirstOrDefault(f => f.Name == "Placeholders")?.Value ?? "";
                
                if (existingPlaceholders.IndexOf(globalPlaceholder.ItemId) == -1)
                {
                    // Add the placeholder setting ID to the rendering's Placeholders field
                    var updatedPlaceholders = existingPlaceholders + (existingPlaceholders == "" ? "" : "|") + globalPlaceholder.ItemId;
                    
                    var renderingFields = new Dictionary<string, string>
                    {
                        ["Placeholders"] = updatedPlaceholders
                    };

                    await _graphQLService.UpdateItemAsync(args.Endpoint, args.AccessToken, rendering.ItemId, renderingFields, verbose: true);
                    
                    _logger.LogDebug($"Updated rendering placeholders for page type: {pageType.Name}");
                }
                else
                {
                    _logger.LogDebug($"Page rendering already contains placeholder setting: {placeholderName}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating rendering placeholders for page type: {pageType.Name}");
                throw;
            }
        }

        private async Task<IEnumerable<string>> GetControlsAsync(AutoArgs args, List<string> componentNames)
        {
            var controls = new List<string>();
            
            foreach (var componentName in componentNames)
            {
                var component = args.ComponentConfig.Components.Where(c => c.Name == componentName).FirstOrDefault();
                if (component == null)
                {
                    _logger.LogDebug($"Component not found when getting controls: {componentName}");
                    continue;
                }

                try
                {
                    // Get the rendering path for this component
                    var site = args.SiteConfig.Site;
                    var renderingPath = $"{site.RenderingPath}/{component.Category}/{component.Name}";
                    
                    var rendering = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, renderingPath, verbose: true);
                    
                    if (rendering != null)
                    {
                        controls.Add(rendering.ItemId);
                    }
                    else
                    {
                        _logger.LogDebug($"Rendering not found for component: {componentName} at path: {renderingPath}");
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error getting control ID for component: {componentName}");
                    // Continue with other components
                }
            }
            
            return controls;
        }

        private async Task SetDefaultFieldsAsync(AutoArgs args, string itemId)
        {
            try
            {
                // Set any default fields that might be needed for placeholder settings
                var defaultFields = new Dictionary<string, string>();
                
                // Add any default fields if specified in site configuration
                var site = args.SiteConfig.Site;
                if (site.Defaults != null)
                {
                    if (!string.IsNullOrEmpty(site.Defaults.DatasourceWorkflow))
                    {
                        defaultFields["__Default workflow"] = site.Defaults.DatasourceWorkflow;
                    }
                    
                    if (site.Defaults.LanguageFallback)
                    {
                        defaultFields["__Enable item fallback"] = "1";
                    }
                }

                if (defaultFields.Any())
                {
                    await _graphQLService.UpdateItemAsync(args.Endpoint, args.AccessToken, itemId, defaultFields, verbose: true);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error setting default fields for placeholder setting item ID: {itemId}");
                // Don't throw here, as this is not critical for the main operation
            }
        }
    }
}
