using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SiteKit.CLI.Services.Deploy
{
    public class _BuildInsertOptions : IRun
    {
        private readonly IGraphQLService _graphQLService;
        private readonly ILogger _logger;

        public _BuildInsertOptions(IGraphQLService graphQLService, ILogger logger)
        {
            _graphQLService = graphQLService;
            _logger = logger;
        }

        public void Run(AutoArgs args)
        {
            // Use Task.Run to handle async operation within sync interface
            Task.Run(async () => await ProcessAsync(args)).Wait();
        }

        public async Task ProcessAsync(AutoArgs args)
        {
            _logger.LogDebug("Starting insert options build process");

            if (args.PageTypesConfig?.PageTypes == null)
            {
                _logger.LogWarning("No page types found for insert options processing");
                return;
            }

            if (args.SiteConfig?.Site?.SiteTemplatePath == null)
            {
                _logger.LogError("SiteTemplatePath not configured");
                args.IsValid = false;
                args.ValidationMessage = "SiteTemplatePath not configured for insert options processing";
                return;
            }

            foreach (var pageType in args.PageTypesConfig.PageTypes)
            {
                if (pageType.InsertOptions != null && pageType.InsertOptions.Any())
                {
                    await ProcessPageTypeInsertOptions(args, pageType);
                }
            }

            _logger.LogDebug("Insert options build process completed");
        }

        private async Task ProcessPageTypeInsertOptions(AutoArgs args, SiteKit.Types.PageType pageType)
        {
            try
            {
                _logger.LogDebug($"Processing insert options for page type: {pageType.Name}");

                // Step 1: Fetch IDs of all insert options
                var insertOptionIds = new List<string>();
                
                foreach (var insertOption in pageType.InsertOptions!)
                {
                    var insertOptionPath = args.SiteConfig!.Site!.SiteTemplatePath + "/" + insertOption;
                    var insertOptionItem = await _graphQLService.GetItemByPathAsync(
                        args.Endpoint!, 
                        args.AccessToken!, 
                        insertOptionPath, 
                        verbose: true);

                    if (insertOptionItem != null)
                    {
                        insertOptionIds.Add(insertOptionItem.ItemId);
                        _logger.LogDebug($"Found insert option '{insertOption}' with ID: {insertOptionItem.ItemId}");
                    }
                    else
                    {
                        _logger.LogWarning($"Insert option '{insertOption}' not found at path: {insertOptionPath}");
                    }
                }

                if (insertOptionIds.Any())
                {
                    // Step 2: Update the PageType template's __Masters field
                    await UpdatePageTypeMastersField(args, pageType, insertOptionIds);
                }
                else
                {
                    _logger.LogWarning($"No valid insert options found for page type: {pageType.Name}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing insert options for page type: {pageType.Name}");
                args.IsValid = false;
                args.ValidationMessage = $"Error processing insert options for page type '{pageType.Name}': {ex.Message}";
            }
        }

        private async Task UpdatePageTypeMastersField(AutoArgs args, SiteKit.Types.PageType pageType, List<string> insertOptionIds)
        {
            try
            {
                var pageTypePath = args.SiteConfig!.Site!.SiteTemplatePath + "/" + pageType.Name;
                var standardValuesPath = pageTypePath + "/__Standard Values";
                
                // Get the page type's standard values item
                var standardValuesItem = await _graphQLService.GetItemByPathAsync(
                    args.Endpoint!, 
                    args.AccessToken!, 
                    standardValuesPath, 
                    verbose: true);

                if (standardValuesItem == null)
                {
                    _logger.LogError($"Standard Values item not found at path: {standardValuesPath}");
                    return;
                }

                // Convert IDs to GUID format with braces and uppercase
                var formattedIds = new List<string>();
                foreach (var id in insertOptionIds)
                {
                    try
                    {
                        var guid = Guid.Parse(id);
                        var formattedId = guid.ToString("B").ToUpper();
                        formattedIds.Add(formattedId);
                        _logger.LogDebug($"Formatted ID '{id}' to '{formattedId}'");
                    }
                    catch (FormatException ex)
                    {
                        _logger.LogWarning($"Failed to parse ID '{id}' as GUID: {ex.Message}");
                        // Keep the original ID if parsing fails
                        formattedIds.Add(id);
                    }
                }

                // Prepare the __Masters field value (pipe-separated list of formatted IDs)
                var mastersValue = string.Join("|", formattedIds);
                
                var fields = new Dictionary<string, string>
                {
                    ["__Masters"] = mastersValue
                };

                // Update the standard values item with the __Masters field
                var updateResult = await _graphQLService.UpdateItemAsync(
                    args.Endpoint!, 
                    args.AccessToken!, 
                    standardValuesItem.ItemId, 
                    fields, 
                    verbose: true);

                if (updateResult != null)
                {
                    _logger.LogInformation($"✓ Updated insert options for page type '{pageType.Name}' Standard Values - __Masters field set to: {mastersValue}");
                }
                else
                {
                    _logger.LogError($"Failed to update __Masters field for page type '{pageType.Name}' Standard Values");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error updating __Masters field for page type '{pageType.Name}' Standard Values");
                throw;
            }
        }
    }
}
