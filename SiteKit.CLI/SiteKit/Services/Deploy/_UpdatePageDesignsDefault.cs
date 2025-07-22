using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;
using Microsoft.Extensions.Logging;
using SiteKit.CLI.Services;
using SiteKit.Types;

namespace SiteKit.CLI.Services.Deploy
{
    public class _UpdatePageDesignsDefault : IRun
    {
        private readonly IGraphQLService _graphQLService;
        private readonly ILogger _logger;

        private const string DEFAULT_PAGE_DESIGN_NAME = "Default";

        public _UpdatePageDesignsDefault(IGraphQLService graphQLService, ILogger logger)
        {
            _graphQLService = graphQLService;
            _logger = logger;
        }

        public void Run(AutoArgs args)
        {
            try
            {
                _logger.LogDebug("Starting page designs default templates mapping update");

                var task = UpdateTemplatesMappingAsync(args);
                task.Wait();

                _logger.LogDebug("Page designs default templates mapping update completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during page designs default templates mapping update");
                throw;
            }
        }

        private async Task UpdateTemplatesMappingAsync(AutoArgs args)
        {
            try
            {
                _logger.LogDebug("Processing templates mapping update");

                // Get the Default page design item with TemplatesMapping field
                string defaultPageDesignPath = $"{args.SiteConfig.Site.PageDesignsPath}/{DEFAULT_PAGE_DESIGN_NAME}";
                var defaultPageDesignItem = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, defaultPageDesignPath, verbose: true);

                if (defaultPageDesignItem == null)
                {
                    _logger.LogError($"Default page design item not found at path: {defaultPageDesignPath}");
                    return;
                }
                // Get the Default page design item ID for mapping value
                var defaultPageDesignId = new Guid(defaultPageDesignItem.ItemId).ToString("B").ToUpperInvariant();


                // Get the Default page design item
                string pageDesignPath = $"{args.SiteConfig.Site.PageDesignsPath}";
                var pageDesignItem = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, pageDesignPath, verbose: true);

                if (pageDesignItem == null)
                {
                    _logger.LogError($"Page design item not found at path: {pageDesignPath}");
                    return;
                }

                // Get current TemplatesMapping value
                var currentMapping = GetCurrentTemplatesMapping(pageDesignItem);
                _logger.LogDebug($"Current TemplatesMapping: {currentMapping}");

                // Get the Default page design item ID for mapping value
                var pageDesignId = new Guid(pageDesignItem.ItemId).ToString("B").ToUpperInvariant();

                // Build new mappings
                var newMappings = await BuildNewTemplatesMappingsAsync(args, currentMapping, defaultPageDesignId);

                if (newMappings.Any())
                {
                    // Combine existing and new mappings
                    var allMappings = new List<string>();
                    if (!string.IsNullOrEmpty(currentMapping))
                    {
                        allMappings.Add(currentMapping);
                    }
                    allMappings.AddRange(newMappings);

                    var updatedMapping = string.Join("&", allMappings);
                    
                    // Update the Default page design item
                    var fields = new Dictionary<string, string>
                    {
                        ["TemplatesMapping"] = updatedMapping
                    };

                    var response = await _graphQLService.UpdateItemAsync(args.Endpoint, args.AccessToken, pageDesignItem.ItemId, fields, verbose: true);
                    if (response != null)
                    {
                        _logger.LogDebug($"Successfully updated TemplatesMapping with {newMappings.Count} new mappings");
                    }
                    else
                    {
                        _logger.LogError("Failed to update TemplatesMapping");
                    }
                }
                else
                {
                    _logger.LogDebug("No new template mappings to add");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing templates mapping update");
                throw;
            }
        }

        private string GetCurrentTemplatesMapping(GraphQLItemResponse item)
        {
            try
            {
                // Try to get the TemplatesMapping field value from the item
                if (item.Fields != null)
                {
                    var templatesMappingField = item.Fields.FirstOrDefault(f => f.Name == "TemplatesMapping");
                    if (templatesMappingField != null)
                    {
                        return templatesMappingField.Value ?? "";
                    }
                }
                return "";
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Could not retrieve current TemplatesMapping value");
                return "";
            }
        }

        private async Task<List<string>> BuildNewTemplatesMappingsAsync(AutoArgs args, string currentMapping, string defaultPageDesignId)
        {
            var newMappings = new List<string>();

            try
            {
                if (args.PageTypesConfig?.PageTypes == null || !args.PageTypesConfig.PageTypes.Any())
                {
                    _logger.LogDebug("No page types found to create mappings for");
                    return newMappings;
                }

                foreach (var pageType in args.PageTypesConfig.PageTypes)
                {
                    try
                    {
                        // Get the page type template item ID
                        var pageTypeTemplateId = await GetPageTypeTemplateIdAsync(args, pageType);
                        if (pageTypeTemplateId == null)
                        {
                            _logger.LogWarning($"Could not find template ID for page type: {pageType.Name}");
                            continue;
                        }

                        // Check if this template ID is already in the current mapping
                        var encodedTemplateId = HttpUtility.UrlEncode(pageTypeTemplateId);
                        if (!string.IsNullOrEmpty(currentMapping) && currentMapping.Contains(encodedTemplateId))
                        {
                            _logger.LogDebug($"Template ID {pageTypeTemplateId} already exists in mapping, skipping");
                            continue;
                        }

                        // Use the actual page type template ID instead of hardcoded constants
                        string mappingTemplateId = pageTypeTemplateId;

                        // Create the mapping entry: {templateId}={defaultPageDesignId}
                        var mappingEntry = $"{HttpUtility.UrlEncode(mappingTemplateId)}%3d{HttpUtility.UrlEncode(defaultPageDesignId)}";
                        newMappings.Add(mappingEntry);

                        _logger.LogDebug($"Created mapping for page type '{pageType.Name}': {mappingTemplateId} -> {defaultPageDesignId}");
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, $"Error processing page type: {pageType.Name}");
                    }
                }

                _logger.LogDebug($"Built {newMappings.Count} new template mappings");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error building new templates mappings");
            }

            return newMappings;
        }

        private async Task<string?> GetPageTypeTemplateIdAsync(AutoArgs args, PageType pageType)
        {
            try
            {
                // Build the page type template path
                string templatePath = $"{args.SiteConfig.Site.SiteTemplatePath}/{pageType.Name}";
                
                // Get the template item
                var templateItem = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, templatePath, verbose: true);

                if (templateItem != null)
                {
                    return new Guid(templateItem.ItemId).ToString("B").ToUpperInvariant();
                }
                else
                {
                    _logger.LogWarning($"Template item not found for page type: {pageType.Name} at path: {templatePath}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting template ID for page type: {pageType.Name}");
                return null;
            }
        }
    }
}
