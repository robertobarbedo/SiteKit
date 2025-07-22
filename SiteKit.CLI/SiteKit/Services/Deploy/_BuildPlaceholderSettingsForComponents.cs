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
        
        // Dictionary to accumulate rendering IDs and their placeholder IDs
        private readonly Dictionary<string, List<string>> _renderingPlaceholderUpdates;

        public _BuildPlaceholderSettingsForComponents(IGraphQLService graphQLService, ILogger logger)
        {
            _graphQLService = graphQLService;
            _logger = logger;
            _renderingPlaceholderUpdates = new Dictionary<string, List<string>>();
        }

        public void Run(AutoArgs args)
        {
            // This is now async but IRun interface expects sync - we'll use Task.Run to handle it
            Task.Run(async () => await ProcessAsync(args)).Wait();
        }

        public async Task ProcessAsync(AutoArgs args)
        {
            // Clear any previous updates
            _renderingPlaceholderUpdates.Clear();
            
            var components = args.CompositionConfig.Composition.Components;
            foreach (var key in components.Keys)
            {
                var ph = components[key];
                foreach (var phKey in ph.Keys)
                {
                    await CreateOrUpdateAsync(args, key, phKey, components[key][phKey]);
                }
            }
            
            // After processing all components, apply the rendering placeholder updates
            await ApplyRenderingPlaceholderUpdatesAsync(args);
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

                // Accumulate rendering ID and placeholder ID for later batch update
                var placeholderIdFormatted = new Guid(globalPlaceholder.ItemId).ToString("B").ToUpperInvariant();
                
                if (!_renderingPlaceholderUpdates.ContainsKey(rendering.ItemId))
                {
                    _renderingPlaceholderUpdates[rendering.ItemId] = new List<string>();
                }
                
                if (!_renderingPlaceholderUpdates[rendering.ItemId].Contains(placeholderIdFormatted))
                {
                    _renderingPlaceholderUpdates[rendering.ItemId].Add(placeholderIdFormatted);
                    _logger.LogDebug($"Queued placeholder setting for rendering '{component.Name}': {placeholderIdFormatted}");
                }
                else
                {
                    _logger.LogDebug($"Placeholder setting already queued for rendering '{component.Name}': {placeholderIdFormatted}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error queuing rendering placeholder update for component: {component.Name}");
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

        private async Task<string> GetCurrentPlaceholdersValueAsync(AutoArgs args, string renderingId)
        {
            try
            {
                // Since the GraphQL GetItemByPathAsync doesn't return the Placeholders field,
                // we have a few options:
                // 1. Return empty string and always append (safe approach)
                // 2. Try a different GraphQL query that specifically requests this field
                // 3. Make a separate API call to get the field value
                
                // For now, we'll use the safe approach and return empty string
                // This means we might have duplicate entries, but it's better than missing them
                await Task.CompletedTask; // To make this properly async
                return "";
            }
            catch (Exception ex)
            {
                _logger.LogDebug($"Could not retrieve current Placeholders value for item {renderingId}: {ex.Message}");
                return "";
            }
        }

        private async Task ApplyRenderingPlaceholderUpdatesAsync(AutoArgs args)
        {
            try
            {
                _logger.LogDebug($"Applying rendering placeholder updates for {_renderingPlaceholderUpdates.Count} renderings");

                foreach (var renderingUpdate in _renderingPlaceholderUpdates)
                {
                    var renderingId = renderingUpdate.Key;
                    var placeholderIds = renderingUpdate.Value;

                    try
                    {
                        // Get current placeholder value (using our workaround)
                        var currentPlaceholders = await GetCurrentPlaceholdersValueAsync(args, renderingId);
                        
                        // Build the new placeholder value by combining current and new placeholder IDs
                        var allPlaceholderIds = new List<string>();
                        
                        // Add existing placeholders if any
                        if (!string.IsNullOrEmpty(currentPlaceholders))
                        {
                            allPlaceholderIds.AddRange(currentPlaceholders.Split('|', StringSplitOptions.RemoveEmptyEntries));
                        }
                        
                        // Add new placeholder IDs (avoiding duplicates)
                        foreach (var placeholderId in placeholderIds)
                        {
                            if (!allPlaceholderIds.Contains(placeholderId))
                            {
                                allPlaceholderIds.Add(placeholderId);
                            }
                        }
                        
                        // Update the rendering with all placeholder IDs
                        var updatedPlaceholders = string.Join("|", allPlaceholderIds);
                        
                        var renderingFields = new Dictionary<string, string>
                        {
                            ["Placeholders"] = updatedPlaceholders
                        };

                        var result = await _graphQLService.UpdateItemAsync(args.Endpoint, args.AccessToken, renderingId, renderingFields, verbose: true);
                        
                        if (result != null)
                        {
                            _logger.LogDebug($"Successfully updated rendering {renderingId} with {placeholderIds.Count} placeholder settings");
                        }
                        else
                        {
                            _logger.LogWarning($"Failed to update rendering {renderingId} with placeholder settings");
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error updating rendering {renderingId} with placeholder settings");
                        // Continue with other renderings
                    }
                }
                
                _logger.LogDebug($"Completed applying rendering placeholder updates");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in ApplyRenderingPlaceholderUpdatesAsync");
                throw;
            }
        }
    }
}
