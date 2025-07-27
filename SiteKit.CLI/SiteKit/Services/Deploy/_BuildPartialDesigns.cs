using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SiteKit.CLI.Services;
using SiteKit.Types;

namespace SiteKit.CLI.Services.Deploy
{
    public class _BuildPartialDesigns : IRun
    {
        private readonly IGraphQLService _graphQLService;
        private readonly ILogger _logger;

        // Standard template IDs
        private const string LAYOUT_FIELD = "__Renderings";

        public _BuildPartialDesigns(IGraphQLService graphQLService, ILogger logger)
        {
            _graphQLService = graphQLService;
            _logger = logger;
        }

        public void Run(AutoArgs args)
        {
            try
            {
                _logger.LogDebug("Starting partial designs build");

                if (args.PartialsConfig?.Partials == null || !args.PartialsConfig.Partials.Any())
                {
                    _logger.LogDebug("No partials found to build");
                    return;
                }

                foreach (var partial in args.PartialsConfig.Partials)
                {
                    var task = CreateOrUpdateAsync(args, partial);
                    task.Wait();
                }

                _logger.LogDebug("Partial designs build completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during partial designs build");
                throw;
            }
        }

        private async Task CreateOrUpdateAsync(AutoArgs args, SiteKit.Types.Partial partial)
        {
            try
            {
                _logger.LogDebug($"Processing partial design for: {partial.Name}");

                // Build the partial design item path
                string partialPath = $"{args.SiteConfig.Site.PartialDesignsPath}/{partial.Name}";
                
                // Try to get existing partial design item
                var existingItem = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, partialPath, verbose: true);

                string partialItemId;

                if (existingItem == null)
                {
                    // Create new partial design item
                    _logger.LogDebug($"Creating new partial design: {partial.Name}");

                    // Get parent folder
                    var parentItem = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, args.SiteConfig.Site.PartialDesignsPath, verbose: true);
                    if (parentItem == null)
                    {
                        _logger.LogError($"Partial designs parent folder not found: {args.SiteConfig.Site.PartialDesignsPath}");
                        return;
                    }

                    // Create the partial design item using a standard Sitecore item template
                    // You may need to adjust the template ID based on your Sitecore setup
                    var createdItemId = await _graphQLService.CreateItemAsync(
                        args.Endpoint,
                        args.AccessToken,
                        partial.Name,
                        "{FD2059FD-6043-4DFE-8C04-E2437CE87634}", // Standard template ID - adjust as needed
                        parentItem.ItemId,
                        verbose: true);

                    if (createdItemId == null)
                    {
                        _logger.LogError($"Failed to create partial design: {partial.Name}");
                        return;
                    }

                    partialItemId = createdItemId;
                }
                else
                {
                    partialItemId = existingItem.ItemId;
                    _logger.LogDebug($"Using existing partial design: {partial.Name} (ID: {partialItemId})");
                }

                // Build the layout XML
                var layoutXml = await BuildLayoutXmlAsync(args, partial);
                
                // Update the partial design item with the layout
                var fields = new Dictionary<string, string>
                {
                    [LAYOUT_FIELD] = layoutXml
                };

                var response = await _graphQLService.UpdateItemAsync(args.Endpoint, args.AccessToken, partialItemId, fields, verbose: true);
                if (response != null)
                {
                    _logger.LogDebug($"Successfully updated partial design layout for: {partial.Name}");
                }
                else
                {
                    _logger.LogError($"Failed to update partial design layout for: {partial.Name}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing partial design for: {partial.Name}");
                throw;
            }
        }

        private async Task<string> BuildLayoutXmlAsync(AutoArgs args, SiteKit.Types.Partial partial)
        {
            try
            {
                var layoutBuilder = new LayoutBuilder();
                int phId = 1;

                // Determine the main placeholder name based on partial name
                string mainPlaceholderName = GetMainPlaceholderName(partial.Name);

                // Process layout components if they exist
                if (partial.Layout != null && partial.Layout.Any())
                {
                    string accumulatedPlaceholder = $"/{mainPlaceholderName}";
                    
                    phId = await ProcessLayoutComponentsAsync(layoutBuilder, args, partial.Layout, accumulatedPlaceholder, phId);
                }

                return layoutBuilder.ToXml();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error building layout XML for partial: {partial.Name}");
                throw;
            }
        }

        private string GetMainPlaceholderName(string partialName)
        {
            switch (partialName.ToLowerInvariant())
            {
                case "header":
                    return "headless-header";
                case "footer":
                    return "headless-footer";
                default:
                    return "headless-main";
            }
        }

        private async Task<int> ProcessLayoutComponentsAsync(LayoutBuilder layoutBuilder, AutoArgs args,
            List<SiteKit.Types.LayoutComponent> components, string accumulatedPlaceholder, int phId)
        {
            string previousComponentName = "";
            int indexComponent = 0;

            foreach (var component in components)
            {
                try
                {
                    // Find component definition
                    var componentDefinition = args.ComponentConfig.Components
                        .FirstOrDefault(c => c.Name == component.Component);
                    if (componentDefinition == null)
                    {
                        _logger.LogError($"Component not found: {component.Component}");
                        continue;
                    }

                    // Find composition component
                    var compositionComponentKey = args.CompositionConfig.Composition.Components.Keys
                        .FirstOrDefault(c => c == component.Component);
                    if (compositionComponentKey == null)
                    {
                        _logger.LogError($"Composition component key not found: {component.Component}");
                        continue;
                    }

                    var compositionComponent = args.CompositionConfig.Composition.Components[compositionComponentKey];
                    if (compositionComponent == null)
                    {
                        _logger.LogError($"Composition component not found: {component.Component}");
                        continue;
                    }

                    // Handle component indexing
                    if (previousComponentName != component.Component)
                    {
                        indexComponent = 0;
                        previousComponentName = component.Component;
                        phId++;

                        // Get rendering ID and add to layout
                        var renderingId = await GetComponentRenderingIdAsync(args, componentDefinition);
                        if (renderingId != null)
                        {
                            layoutBuilder.AddRendering(renderingId, accumulatedPlaceholder, phId);
                        }
                    }
                    else
                    {
                        indexComponent++;
                    }

                    // Build child placeholder
                    if (indexComponent < compositionComponent.Keys.Count)
                    {
                        var keys = compositionComponent.Keys.ToList()[indexComponent];
                        string childPlaceholder = $"{accumulatedPlaceholder}/{component.Component.Replace(" ", "-")}-{keys}-{phId - indexComponent}".ToLowerInvariant();

                        // Process children recursively
                        if (component.Children != null && component.Children.Any())
                        {
                            phId = await ProcessLayoutComponentsAsync(layoutBuilder, args, component.Children, childPlaceholder, phId);
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error processing component: {component.Component}");
                }
            }

            return phId;
        }

        private async Task<string?> GetComponentRenderingIdAsync(AutoArgs args, SiteKit.Types.ComponentDefinition componentDefinition)
        {
            try
            {
                // Build the rendering path
                string renderingPath = $"{args.SiteConfig.Site.RenderingPath}/{componentDefinition.Category}/{componentDefinition.Name}";
                
                // Get the rendering item
                var renderingItem = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, renderingPath, verbose: true);

                if (renderingItem != null)
                {
                    return new Guid(renderingItem.ItemId).ToString("B").ToUpperInvariant();
                }
                else
                {
                    _logger.LogError($"renderingItem is null: {componentDefinition.Name}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting rendering ID for component: {componentDefinition.Name}");
                return null;
            }
        }
    }
}
