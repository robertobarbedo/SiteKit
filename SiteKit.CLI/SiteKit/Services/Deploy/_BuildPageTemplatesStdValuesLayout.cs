using Microsoft.Extensions.Logging;
using SiteKit.CLI.Services;
using SiteKit.Types;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.AccessControl;
using System.Text.Json;
using System.Threading.Tasks;

namespace SiteKit.CLI.Services.Deploy
{
    public class _BuildPageTemplatesStdValuesLayout : IRun
    {
        private readonly IGraphQLService _graphqlService;
        private readonly ILogger _logger;
        private readonly HttpClient _httpClient;

        // Standard template IDs
        private const string TEMPLATE_TEMPLATE_ID = "{AB86861A-6030-46C5-B394-E8F99E8B87DB}";
        private const string LAYOUT_FIELD_ID = "{F1A1FE9E-A60C-4DDB-A3A0-BB5B29FE732E}";
        private const string MAIN_PLACEHOLDER_NAME = "headless-main";

        public _BuildPageTemplatesStdValuesLayout(IGraphQLService graphqlService, ILogger logger, HttpClient httpClient)
        {
            _graphqlService = graphqlService;
            _logger = logger;
            _httpClient = httpClient;
        }

        public void Run(AutoArgs args)
        {
            try
            {
                _logger.LogDebug("Starting page template standard values layout build");

                foreach (var pagetype in args.PageTypesConfig.PageTypes)
                {
                    var task = CreateOrUpdateAsync(args, pagetype);
                    task.Wait();
                }

                _logger.LogDebug("Page template standard values layout build completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during page template standard values layout build");
                throw;
            }
        }

        private async Task CreateOrUpdateAsync(AutoArgs args, SiteKit.Types.PageType pagetype)
        {
            try
            {
                _logger.LogDebug($"Processing page template layout for: {pagetype.Name}");

                // Build the template path
                string templatePath = $"{args.SiteConfig.Site.SiteTemplatePath}/{pagetype.Name}";
                
                // Get the template item
                var templateItem = await _graphqlService.GetItemByPathAsync(args.Endpoint, args.AccessToken, templatePath);
                if (templateItem == null)
                {
                    _logger.LogError($"Template not found: {templatePath}");
                    return;
                }

                // Get the standard values item
                string standardValuesPath = $"{templatePath}/__Standard Values";
                var standardValuesItem = await _graphqlService.GetItemByPathAsync(args.Endpoint, args.AccessToken, standardValuesPath);
                if (standardValuesItem == null)
                {
                    _logger.LogError($"Standard values item not found: {standardValuesPath}");
                    return;
                }

                // Build the layout XML
                var layoutXml = await BuildLayoutXmlAsync(args, pagetype);
                
                // Update the standard values item with the layout
                var fields = new Dictionary<string, string>
                {
                    [LAYOUT_FIELD_ID] = layoutXml
                };

                var response = await _graphqlService.UpdateItemAsync(args.Endpoint, args.AccessToken, standardValuesItem.ItemId, fields);
                if (response != null)
                {
                    _logger.LogDebug($"Successfully updated standard values layout for: {pagetype.Name}");
                }
                else
                {
                    _logger.LogError($"Failed to update standard values layout for: {pagetype.Name}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing page template layout for: {pagetype.Name}");
                throw;
            }
        }

        private async Task<string> BuildLayoutXmlAsync(AutoArgs args, SiteKit.Types.PageType pagetype)
        {
            try
            {
                var layoutBuilder = new LayoutBuilder();
                int phId = 1;

                // Add the main page type rendering
                var pageTypeRenderingId = await GetPageTypeRenderingIdAsync(args, pagetype);
                if (pageTypeRenderingId != null)
                {
                    layoutBuilder.AddRendering(pageTypeRenderingId, MAIN_PLACEHOLDER_NAME, phId);
                }

                // Find the composition for this page type
                var compositionPageTypeKey = args.CompositionConfig.Composition.Pages.Keys
                    .FirstOrDefault(c => c == pagetype.Name);
                
                if (compositionPageTypeKey == null)
                {
                    _logger.LogWarning($"Composition page type key not found: {pagetype.Name}");
                    return layoutBuilder.ToXml();
                }

                var compositionPageType = args.CompositionConfig.Composition.Pages[compositionPageTypeKey];

                // Build layout components if they exist
                if (pagetype.Layout != null && pagetype.Layout.Any())
                {
                    string accumulatedPlaceholder = $"/{MAIN_PLACEHOLDER_NAME}/{pagetype.Name.Replace(" ", "-")}-{compositionPageType.Keys.FirstOrDefault()}-{phId}".ToLowerInvariant();
                    
                    phId = await ProcessLayoutComponentsAsync(layoutBuilder, args, pagetype.Layout, accumulatedPlaceholder, phId);
                }

                return layoutBuilder.ToXml();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error building layout XML for: {pagetype.Name}");
                throw;
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
                        //_logger.LogError($"Composition component key not found: {component.Component}");
                        //continue;
                    }

                    var compositionComponent = compositionComponentKey == null ? null : args.CompositionConfig.Composition.Components[compositionComponentKey];
                    //if (compositionComponent == null)
                    //{
                        //_logger.LogError($"Composition component not found: {component.Component}");
                        //continue;
                    //}

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
                    if (compositionComponent != null && indexComponent < compositionComponent.Keys.Count)
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

        private async Task<string?> GetPageTypeRenderingIdAsync(AutoArgs args, SiteKit.Types.PageType pagetype)
        {
            try
            {
                // Build the rendering path
                string renderingPath = $"{args.SiteConfig.Site.RenderingPath}/{pagetype.Name}";

                // Get the rendering item
                var renderingItem = await _graphqlService.GetItemByPathAsync(args.Endpoint, args.AccessToken, renderingPath);

                if (renderingItem != null)
                {
                    return new Guid(renderingItem.ItemId).ToString("B").ToUpperInvariant();
                }
                else
                {
                    _logger.LogError($"renderingItem is null: {pagetype.Name}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting page type rendering ID for: {pagetype.Name}");
                return null;
            }
        }

        private async Task<string?> GetComponentRenderingIdAsync(AutoArgs args, SiteKit.Types.ComponentDefinition componentDefinition)
        {
            try
            {
                // Build the rendering path
                string renderingPath = $"{args.SiteConfig.Site.RenderingPath}/{componentDefinition.Category}/{componentDefinition.Name}";
                
                // Get the rendering item
                var renderingItem = await _graphqlService.GetItemByPathAsync(args.Endpoint, args.AccessToken, renderingPath);

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

    // Helper class to build layout XML
    public class LayoutBuilder
    {
        private readonly List<RenderingDefinition> _renderings = new List<RenderingDefinition>();

        public void AddRendering(string itemId, string placeholder, int dynamicPlaceholderId)
        {
            if (string.IsNullOrEmpty(itemId)) return;

            _renderings.Add(new RenderingDefinition
            {
                ItemId = itemId,
                Placeholder = placeholder,
                Parameters = $"DynamicPlaceholderId={dynamicPlaceholderId}"
            });
        }

        public string ToXml()
        {
            ;
            if (!_renderings.Any())
                return "<r xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\" ><d id=\"{FE5D7FDF-89C0-4D99-9AA3-B5FBD009C9F3}\" l=\"{96E5F4BA-A2CF-4A4C-A4E7-64DA88226362}\" /></r>";

            var renderingsXml = string.Join("", _renderings.Select(r => 
                $"<r id=\"{r.ItemId}\" ph=\"{r.Placeholder}\" par=\"{r.Parameters}\" uid=\"{Guid.NewGuid().ToString("B").ToUpperInvariant()}\" />"));

            return $"<r xmlns:xsd=\"http://www.w3.org/2001/XMLSchema\"><d id=\"{{FE5D7FDF-89C0-4D99-9AA3-B5FBD009C9F3}}\" l=\"{{96E5F4BA-A2CF-4A4C-A4E7-64DA88226362}}\">{renderingsXml}</d></r>";
        }
    }

    public class RenderingDefinition
    {
        public string ItemId { get; set; } = string.Empty;
        public string Placeholder { get; set; } = string.Empty;
        public string Parameters { get; set; } = string.Empty;
    }
}