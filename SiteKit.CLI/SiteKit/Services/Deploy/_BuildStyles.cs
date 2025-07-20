using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SiteKit.Types;
using SiteKit.CLI.Services;

namespace SiteKit.CLI.Services.Deploy
{
    public class _BuildStyles : IRun
    {
        private readonly IGraphQLService _graphQLService;
        private readonly ILogger _logger;

        public _BuildStyles(IGraphQLService graphQLService, ILogger logger)
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
            try
            {
                var components = args.ComponentConfig.Components;
                foreach (var component in components)
                {
                    if (component.Parameters != null && component.Parameters.Count > 0)
                    {
                        await CreateOrUpdateAsync(args, component);
                    }
                }
                
                _logger.LogDebug($"Successfully processed styles for {components.Count} components");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing component styles");
                args.IsValid = false;
                args.ValidationMessage = $"Error processing component styles: {ex.Message}";
                throw;
            }
        }

        private async Task CreateOrUpdateAsync(AutoArgs args, ComponentDefinition component)
        {
            try
            {
                foreach (var param in component.Parameters)
                {
                    // 1 - Create or Update the Styles folder
                    await CreateStylesFolderAsync(args, component, param);

                    // 2 - Add the style options
                    await CreateStyleOptionsAsync(args, component, param);
                }

                _logger.LogDebug($"Successfully processed styles for component: {component.Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing styles for component: {component.Name}");
                throw;
            }
        }

        private async Task CreateStylesFolderAsync(AutoArgs args, ComponentDefinition component, ParameterDefinition param)
        {
            try
            {
                var site = args.SiteConfig.Site;
                var stylesFolderPath = $"{site.SitePath}/Presentation/Styles/{param.Name}";

                // Try to retrieve the styles folder by path
                var existingStylesFolder = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, stylesFolderPath, verbose: true);

                string stylesFolderId;

                if (existingStylesFolder == null)
                {
                    _logger.LogDebug($"Creating styles folder: {param.Name} at path: {stylesFolderPath}");

                    // Get the parent Styles folder
                    var parentStylesPath = $"{site.SitePath}/Presentation/Styles";
                    var parentStylesFolder = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, parentStylesPath, verbose: true);

                    if (parentStylesFolder == null)
                    {
                        _logger.LogError($"Parent styles folder not found at path: {parentStylesPath}");
                        args.IsValid = false;
                        args.ValidationMessage = $"Parent styles folder not found at path: {parentStylesPath}";
                        return;
                    }

                    // Create the styles folder using the Styles Folder template
                    var createdStylesFolderId = await _graphQLService.CreateItemAsync(
                        args.Endpoint,
                        args.AccessToken,
                        param.Name,
                        "{C6DC7393-15BB-4CD7-B798-AB63E77EBAC4}", // Styles Folder template ID
                        parentStylesFolder.ItemId,
                        verbose: true);

                    if (createdStylesFolderId == null)
                    {
                        _logger.LogError($"Failed to create styles folder: {param.Name}");
                        args.IsValid = false;
                        args.ValidationMessage = $"Failed to create styles folder: {param.Name}";
                        return;
                    }

                    stylesFolderId = createdStylesFolderId;
                }
                else
                {
                    stylesFolderId = existingStylesFolder.ItemId;
                    _logger.LogDebug($"Using existing styles folder: {param.Name} (ID: {stylesFolderId})");
                }

                // Update styles folder fields
                var stylesFields = new Dictionary<string, string>();
                stylesFields["Type"] = param.Type;

                await _graphQLService.UpdateItemAsync(args.Endpoint, args.AccessToken, stylesFolderId, stylesFields, verbose: true);

                // Set default fields
                await SetDefaultFieldsAsync(args, stylesFolderId);

                _logger.LogDebug($"Successfully processed styles folder: {param.Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating styles folder: {param.Name}");
                throw;
            }
        }

        private async Task CreateStyleOptionsAsync(AutoArgs args, ComponentDefinition component, ParameterDefinition param)
        {
            try
            {
                var site = args.SiteConfig.Site;
                var stylesFolderPath = $"{site.SitePath}/Presentation/Styles/{param.Name}";

                // Get the styles folder
                var stylesFolder = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, stylesFolderPath, verbose: true);

                if (stylesFolder == null)
                {
                    _logger.LogError($"Styles folder not found at path: {stylesFolderPath}");
                    return;
                }

                // Get the component rendering ID for allowed renderings
                var componentRenderingId = await GetComponentRenderingIdAsync(args, component);
                if (componentRenderingId == null)
                {
                    _logger.LogDebug($"Component rendering not found for component: {component.Name}");
                    return;
                }

                int order = 0;
                foreach (var option in param.Styles)
                {
                    order++;
                    
                    var optionPath = $"{stylesFolderPath}/{option.Name}";

                    // Try to retrieve the style option by path
                    var existingOption = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, optionPath, verbose: true);

                    string optionId;

                    if (existingOption == null)
                    {
                        _logger.LogDebug($"Creating style option: {option.Name} at path: {optionPath}");

                        // Create the style option using the Style Option template
                        var createdOptionId = await _graphQLService.CreateItemAsync(
                            args.Endpoint,
                            args.AccessToken,
                            option.Name,
                            "{6B8AABEF-D650-46E0-97D0-C0B04F7F016B}", // Style Option template ID
                            stylesFolder.ItemId,
                            verbose: true);

                        if (createdOptionId == null)
                        {
                            _logger.LogError($"Failed to create style option: {option.Name}");
                            continue;
                        }

                        optionId = createdOptionId;
                    }
                    else
                    {
                        optionId = existingOption.ItemId;
                        _logger.LogDebug($"Using existing style option: {option.Name} (ID: {optionId})");
                    }

                    // Update style option fields
                    var optionFields = new Dictionary<string, string>();
                    optionFields["Value"] = option.Value;
                    optionFields["__Sortorder"] = order.ToString();

                    // Handle Allowed Renderings field
                    var existingAllowedRenderings = existingOption?.Fields?.FirstOrDefault(f => f.Name == "Allowed Renderings")?.Value ?? "";
                    
                    if (existingAllowedRenderings == "")
                    {
                        optionFields["Allowed Renderings"] = componentRenderingId;
                    }
                    else if (existingAllowedRenderings.IndexOf(componentRenderingId) == -1)
                    {
                        optionFields["Allowed Renderings"] = existingAllowedRenderings + "|" + componentRenderingId;
                    }

                    await _graphQLService.UpdateItemAsync(args.Endpoint, args.AccessToken, optionId, optionFields, verbose: true);

                    // Set default fields
                    await SetDefaultFieldsAsync(args, optionId);

                    _logger.LogDebug($"Successfully processed style option: {option.Name}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating style options for parameter: {param.Name}");
                throw;
            }
        }

        private async Task<string?> GetComponentRenderingIdAsync(AutoArgs args, ComponentDefinition component)
        {
            try
            {
                var site = args.SiteConfig.Site;
                var renderingPath = $"{site.RenderingPath}/{component.Category}/{component.Name}";
                
                var rendering = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, renderingPath, verbose: true);
                
                return rendering?.ItemId;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting component rendering ID for component: {component.Name}");
                return null;
            }
        }

        private async Task SetDefaultFieldsAsync(AutoArgs args, string itemId)
        {
            try
            {
                // Set any default fields that might be needed for style items
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
                _logger.LogError(ex, $"Error setting default fields for item ID: {itemId}");
                // Don't throw here, as this is not critical for the main operation
            }
        }
    }
}