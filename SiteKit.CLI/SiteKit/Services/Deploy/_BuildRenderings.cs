using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using SiteKit.Types;
using SiteKit.CLI.Services;

namespace SiteKit.CLI.Services.Deploy
{
    public class _BuildRenderings : IRun
    {
        private readonly IGraphQLService _graphQLService;
        private readonly ILogger _logger;

        public _BuildRenderings(IGraphQLService graphQLService, ILogger logger)
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
            var components = args.ComponentConfig.Components;
            foreach (var component in components)
            {
                await CreateOrUpdateAsync(args, component);
            }
        }

        private async Task CreateOrUpdateAsync(AutoArgs args, ComponentDefinition component)
        {
            try
            {
                var site = args.SiteConfig.Site;
                var renderingPath = $"{site.RenderingPath}/{component.Category}/{component.Name}";
                
                // Try to retrieve the rendering by path
                var existingRendering = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, renderingPath, verbose: true);

                string renderingId;

                if (existingRendering == null)
                {
                    _logger.LogInformation($"Creating rendering: {component.Name} at path: {renderingPath}");
                    
                    // Get the parent folder (rendering category path)
                    var categoryFolderPath = $"{site.RenderingPath}/{component.Category}";
                    var categoryFolder = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, categoryFolderPath, verbose: true);
                    
                    if (categoryFolder == null)
                    {
                        _logger.LogError($"Category folder not found at path: {categoryFolderPath}");
                        args.IsValid = false;
                        args.ValidationMessage = $"Category folder not found at path: {categoryFolderPath}";
                        return;
                    }

                    // Create the rendering using the Controller Rendering template
                    var createdRenderingId = await _graphQLService.CreateItemAsync(
                        args.Endpoint,
                        args.AccessToken,
                        component.Name,
                        "{04646A89-996F-4EE7-878A-FFDBF1F0EF0D}", // Controller Rendering template ID
                        categoryFolder.ItemId,
                        verbose: true);

                    if (createdRenderingId == null)
                    {
                        _logger.LogError($"Failed to create rendering: {component.Name}");
                        args.IsValid = false;
                        args.ValidationMessage = $"Failed to create rendering: {component.Name}";
                        return;
                    }

                    renderingId = createdRenderingId;

                    _logger.LogInformation($"Successfully created rendering: {component.Name} (ID: {renderingId})");
                }
                else
                {
                    renderingId = existingRendering.ItemId;
                    _logger.LogInformation($"Using existing rendering: {component.Name} (ID: {renderingId})");
                }

                // Update rendering fields
                var renderingFields = new Dictionary<string, string>();

                // Set component name (without spaces)
                renderingFields["componentName"] = component.Name.Replace(" ", "");

                // Set other properties
                renderingFields["OtherProperties"] = "IsAutoDatasourceRendering=true&IsRenderingsWithDynamicPlaceholders=true";

                // Set parameters template
                renderingFields["Parameters Template"] = "{45130BDB-BAE3-4EDC-AEA8-7B68B9D94C7F}";

                // Set datasource location and template if component has fields
                if (component.HasFields())
                {
                    renderingFields["Datasource Location"] = $"query:$site/*[@@name='Data']/*[@@templatename='{component.Name} Folder']|query:$sharedSites/*[@@name='Data']/*[@@templatename='{component.Name} Folder']";
                    
                    // Get the datasource template path
                    var datasourceTemplatePath = $"{site.DatasourceTemplatePath}/{component.Category}/{component.Name}";
                    renderingFields["Datasource Template"] = datasourceTemplatePath;
                }

                // Set icon if specified
                if (!string.IsNullOrWhiteSpace(component.Icon))
                {
                    renderingFields["__Icon"] = component.Icon;
                }

                // Add any extra fields from component rendering configuration
                if (component.Rendering != null && component.Rendering.Any())
                {
                    foreach (var extraField in component.Rendering)
                    {
                        if (!string.IsNullOrEmpty(extraField.Name) && !string.IsNullOrEmpty(extraField.Value))
                        {
                            renderingFields[extraField.Name] = extraField.Value;
                        }
                    }
                }

                // Update all rendering fields
                if (renderingFields.Any())
                {
                    await _graphQLService.UpdateItemAsync(args.Endpoint, args.AccessToken, renderingId, renderingFields, verbose: true);
                }

                // Set default fields
                await SetDefaultFieldsAsync(args, renderingId);

                _logger.LogInformation($"Successfully processed rendering: {component.Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing rendering for component: {component.Name}");
                args.IsValid = false;
                args.ValidationMessage = $"Error processing rendering for component: {component.Name} - {ex.Message}";
                throw;
            }
        }

        private async Task SetDefaultFieldsAsync(AutoArgs args, string itemId)
        {
            try
            {
                // Set any default fields that might be needed for renderings
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
                    _logger.LogInformation($"Set default fields for rendering item ID: {itemId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error setting default fields for rendering item ID: {itemId}");
                // Don't throw here, as this is not critical for the main operation
            }
        }
    }
}