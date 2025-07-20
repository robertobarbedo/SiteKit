using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SiteKit.Types;
using SiteKit.CLI.Services;

namespace SiteKit.CLI.Services.Deploy
{
    public class _BuildPlaceholderSettingsForComponents : IRun
    {
        private readonly IGraphQLService _graphQLService;
        private readonly ILogger _logger;

        public _BuildPlaceholderSettingsForComponents(IGraphQLService graphQLService, ILogger logger)
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
            var components = args.CompositionConfig.Composition.Components;
            foreach (var key in components.Keys)
            {
                var ph = components[key];
                foreach (var phKey in ph.Keys)
                {
                    await CreateOrUpdateAsync(args, key, phKey, components[key][phKey]);
                }
            }
        }

        private async Task CreateOrUpdateAsync(AutoArgs args, string componentName, string phName, List<string> components)
        {
            try
            {
                var component = args.ComponentConfig.Components.Where(c => c.Name == componentName).FirstOrDefault();
                if (component == null)
                {
                    _logger.LogDebug($"Component not found: {componentName}");
                    return;
                }

                var site = args.SiteConfig.Site;
                var name = component.Name.ToLower().Replace(" ", "-") + "-" + phName.ToLowerInvariant();

                // Create global placeholder setting
                await CreateGlobalPlaceholderSettingAsync(args, component, name, components);

                // Create site-specific placeholder setting
                await CreateSitePlaceholderSettingAsync(args, component, name, components);

                // Update rendering with placeholder settings
                await UpdateRenderingPlaceholdersAsync(args, component, name);

                _logger.LogDebug($"Successfully processed placeholder settings for component: {componentName}, placeholder: {phName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing placeholder settings for component: {componentName}, placeholder: {phName}");
                args.IsValid = false;
                args.ValidationMessage = $"Error processing placeholder settings for component: {componentName}, placeholder: {phName} - {ex.Message}";
                throw;
            }
        }

        private async Task CreateGlobalPlaceholderSettingAsync(AutoArgs args, ComponentDefinition component, string name, List<string> components)
        {
            try
            {
                var site = args.SiteConfig.Site;
                var globalPlaceholderPath = $"{site.PlaceholderPath}/{component.Category}/{name}";

                // Try to retrieve the global placeholder setting by path
                var existingPlaceholder = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, globalPlaceholderPath, verbose: true);

                string placeholderSettingId;

                if (existingPlaceholder == null)
                {
                    _logger.LogDebug($"Creating global placeholder setting: {name} at path: {globalPlaceholderPath}");

                    // Get the parent category folder
                    var categoryFolderPath = $"{site.PlaceholderPath}/{component.Category}";
                    var categoryFolder = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, categoryFolderPath, verbose: true);

                    if (categoryFolder == null)
                    {
                        _logger.LogError($"Category folder not found at path: {categoryFolderPath}");
                        args.IsValid = false;
                        args.ValidationMessage = $"Category folder not found at path: {categoryFolderPath}";
                        return;
                    }

                    // Create the placeholder setting using the Placeholder Settings template
                    var createdPlaceholderId = await _graphQLService.CreateItemAsync(
                        args.Endpoint,
                        args.AccessToken,
                        name,
                        "{D2A6884C-04D5-4089-A64E-D27CA9D68D4C}", // Placeholder Settings template ID
                        categoryFolder.ItemId,
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
                placeholderFields["Allowed Controls"] = string.Join("|", await GetControlsAsync(args, components));

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

        private async Task CreateSitePlaceholderSettingAsync(AutoArgs args, ComponentDefinition component, string name, List<string> components)
        {
            try
            {
                var site = args.SiteConfig.Site;
                var sitePlaceholderPath = $"{site.SitePath}/Presentation/Placeholder Settings/{component.Category}/{name}";

                // Try to retrieve the site placeholder setting by path
                var existingPlaceholder = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, sitePlaceholderPath, verbose: true);

                string placeholderSettingId;

                if (existingPlaceholder == null)
                {
                    _logger.LogDebug($"Creating site placeholder setting: {name} at path: {sitePlaceholderPath}");

                    // Get the parent category folder
                    var categoryFolderPath = $"{site.SitePath}/Presentation/Placeholder Settings/{component.Category}";
                    var categoryFolder = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, categoryFolderPath, verbose: true);

                    if (categoryFolder == null)
                    {
                        _logger.LogError($"Site category folder not found at path: {categoryFolderPath}");
                        args.IsValid = false;
                        args.ValidationMessage = $"Site category folder not found at path: {categoryFolderPath}";
                        return;
                    }

                    // Create the site placeholder setting using the Placeholder Settings template
                    var createdPlaceholderId = await _graphQLService.CreateItemAsync(
                        args.Endpoint,
                        args.AccessToken,
                        name,
                        "{D2A6884C-04D5-4089-A64E-D27CA9D68D4C}", // Placeholder Settings template ID
                        categoryFolder.ItemId,
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
                placeholderFields["Allowed Controls"] = string.Join("|", await GetControlsAsync(args, components));

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

        private async Task UpdateRenderingPlaceholdersAsync(AutoArgs args, ComponentDefinition component, string placeholderName)
        {
            try
            {
                var site = args.SiteConfig.Site;
                var renderingPath = $"{site.RenderingPath}/{component.Category}/{component.Name}";

                // Get the rendering item
                var rendering = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, renderingPath, verbose: true);

                if (rendering == null)
                {
                    _logger.LogDebug($"Rendering not found at path: {renderingPath}");
                    return;
                }

                // Get the global placeholder setting ID by path
                var globalPlaceholderPath = $"{site.PlaceholderPath}/{component.Category}/{placeholderName}";
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
                    
                    _logger.LogDebug($"Updated rendering placeholders for: {component.Name}");
                }
                else
                {
                    _logger.LogDebug($"Rendering already contains placeholder setting: {placeholderName}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating rendering placeholders for component: {component.Name}");
                throw;
            }
        }

        private async Task<IEnumerable<string>> GetControlsAsync(AutoArgs args, List<string> components)
        {
            var controls = new List<string>();
            
            foreach (var componentName in components)
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
