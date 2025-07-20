using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using SiteKit.Types;
using SiteKit.CLI.Services;

namespace SiteKit.CLI.Services.Deploy
{
    public class _BuildRenderingsPageContainers : IRun
    {
        private readonly IGraphQLService _graphQLService;
        private readonly ILogger _logger;

        public _BuildRenderingsPageContainers(IGraphQLService graphQLService, ILogger logger)
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
            var pageTypes = args.PageTypesConfig.PageTypes;
            foreach (var pageType in pageTypes)
            {
                await CreateOrUpdateAsync(args, pageType);
            }
        }

        private async Task CreateOrUpdateAsync(AutoArgs args, PageType pageType)
        {
            try
            {
                var site = args.SiteConfig.Site;
                var name = pageType.Name;
                var renderingPath = $"{site.RenderingPath}/{name}";
                
                // Try to retrieve the page container rendering by path
                var existingRendering = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, renderingPath, verbose: true);

                string renderingId;

                if (existingRendering == null)
                {
                    _logger.LogDebug($"Creating page container rendering: {name} at path: {renderingPath}");
                    
                    // Get the parent rendering folder
                    var renderingFolder = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, site.RenderingPath, verbose: true);
                    
                    if (renderingFolder == null)
                    {
                        _logger.LogError($"Rendering folder not found at path: {site.RenderingPath}");
                        args.IsValid = false;
                        args.ValidationMessage = $"Rendering folder not found at path: {site.RenderingPath}";
                        return;
                    }

                    // Create the page container rendering using the Controller Rendering template
                    var createdRenderingId = await _graphQLService.CreateItemAsync(
                        args.Endpoint,
                        args.AccessToken,
                        name,
                        "{04646A89-996F-4EE7-878A-FFDBF1F0EF0D}", // Controller Rendering template ID
                        renderingFolder.ItemId,
                        verbose: true);

                    if (createdRenderingId == null)
                    {
                        _logger.LogError($"Failed to create page container rendering: {name}");
                        args.IsValid = false;
                        args.ValidationMessage = $"Failed to create page container rendering: {name}";
                        return;
                    }

                    renderingId = createdRenderingId;
                    _logger.LogDebug($"Successfully created page container rendering: {name} (ID: {renderingId})");
                }
                else
                {
                    renderingId = existingRendering.ItemId;
                    _logger.LogDebug($"Using existing page container rendering: {name} (ID: {renderingId})");
                }

                // Update rendering fields
                var renderingFields = new Dictionary<string, string>();

                // Set component name (without spaces)
                renderingFields["componentName"] = name.Replace(" ", "");

                // Set other properties for page containers (no auto datasource, but with dynamic placeholders)
                renderingFields["OtherProperties"] = "IsRenderingsWithDynamicPlaceholders=true";

                // Set parameters template
                renderingFields["Parameters Template"] = "{45130BDB-BAE3-4EDC-AEA8-7B68B9D94C7F}";

                // Update all rendering fields
                if (renderingFields.Any())
                {
                    await _graphQLService.UpdateItemAsync(args.Endpoint, args.AccessToken, renderingId, renderingFields, verbose: true);
                }

                // Set default fields
                await SetDefaultFieldsAsync(args, renderingId);

                _logger.LogDebug($"Successfully processed page container rendering: {name}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing page container rendering for page type: {pageType.Name}");
                args.IsValid = false;
                args.ValidationMessage = $"Error processing page container rendering for page type: {pageType.Name} - {ex.Message}";
                throw;
            }
        }

        private async Task SetDefaultFieldsAsync(AutoArgs args, string itemId)
        {
            try
            {
                // Set any default fields that might be needed for page container renderings
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
                    _logger.LogDebug($"Set default fields for page container rendering item ID: {itemId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error setting default fields for page container rendering item ID: {itemId}");
                // Don't throw here, as this is not critical for the main operation
            }
        }
    }
}
