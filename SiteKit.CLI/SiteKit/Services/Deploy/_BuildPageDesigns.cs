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
    public class _BuildPageDesigns : IRun
    {
        private readonly IGraphQLService _graphQLService;
        private readonly ILogger _logger;

        // Template ID for page designs
        private const string PAGE_DESIGN_TEMPLATE_ID = "{1105B8F8-1E00-426B-BF1F-C840742D827B}";
        private const string DEFAULT_PAGE_DESIGN_NAME = "Default";

        public _BuildPageDesigns(IGraphQLService graphQLService, ILogger logger)
        {
            _graphQLService = graphQLService;
            _logger = logger;
        }

        public void Run(AutoArgs args)
        {
            try
            {
                _logger.LogDebug("Starting page designs build");

                var task = CreateOrUpdateDefaultPageDesignAsync(args);
                task.Wait();

                _logger.LogDebug("Page designs build completed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during page designs build");
                throw;
            }
        }

        private async Task CreateOrUpdateDefaultPageDesignAsync(AutoArgs args)
        {
            try
            {
                _logger.LogDebug($"Processing default page design");

                // Build the default page design item path
                string defaultPageDesignPath = $"{args.SiteConfig.Site.PageDesignsPath}/{DEFAULT_PAGE_DESIGN_NAME}";
                
                // Try to get existing default page design item
                var existingItem = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, defaultPageDesignPath, verbose: true);

                string pageDesignItemId;

                if (existingItem == null)
                {
                    // Create new default page design item
                    _logger.LogDebug($"Creating new default page design: {DEFAULT_PAGE_DESIGN_NAME}");

                    // Get parent folder
                    var parentItem = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, args.SiteConfig.Site.PageDesignsPath, verbose: true);
                    if (parentItem == null)
                    {
                        _logger.LogError($"Page designs parent folder not found: {args.SiteConfig.Site.PageDesignsPath}");
                        return;
                    }

                    // Create the default page design item
                    var createdItemId = await _graphQLService.CreateItemAsync(
                        args.Endpoint,
                        args.AccessToken,
                        DEFAULT_PAGE_DESIGN_NAME,
                        PAGE_DESIGN_TEMPLATE_ID,
                        parentItem.ItemId,
                        verbose: true);

                    if (createdItemId == null)
                    {
                        _logger.LogError($"Failed to create default page design: {DEFAULT_PAGE_DESIGN_NAME}");
                        return;
                    }

                    pageDesignItemId = createdItemId;
                }
                else
                {
                    pageDesignItemId = existingItem.ItemId;
                    _logger.LogDebug($"Using existing default page design: {DEFAULT_PAGE_DESIGN_NAME} (ID: {pageDesignItemId})");
                }

                // Get partial design IDs for Header and Footer
                var partialDesignIds = await GetPartialDesignIdsAsync(args);
                
                // Update the default page design with partial designs
                if (partialDesignIds.Any())
                {
                    var fields = new Dictionary<string, string>
                    {
                        ["PartialDesigns"] = string.Join("|", partialDesignIds)
                    };

                    var response = await _graphQLService.UpdateItemAsync(args.Endpoint, args.AccessToken, pageDesignItemId, fields, verbose: true);
                    if (response != null)
                    {
                        _logger.LogDebug($"Successfully updated default page design with {partialDesignIds.Count} partial designs");
                    }
                    else
                    {
                        _logger.LogError($"Failed to update default page design with partial designs");
                    }
                }
                else
                {
                    _logger.LogDebug("No Header or Footer partials found to link to default page design");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing default page design");
                throw;
            }
        }

        private async Task<List<string>> GetPartialDesignIdsAsync(AutoArgs args)
        {
            var partialDesignIds = new List<string>();

            try
            {
                if (args.PartialsConfig?.Partials == null || !args.PartialsConfig.Partials.Any())
                {
                    _logger.LogDebug("No partials configuration found");
                    return partialDesignIds;
                }

                // Check for Header partial
                var headerPartial = args.PartialsConfig.Partials.FirstOrDefault(p => 
                    p.Name.Equals("Header", StringComparison.OrdinalIgnoreCase));
                
                if (headerPartial != null)
                {
                    var headerItemId = await GetPartialDesignItemIdAsync(args, headerPartial.Name);
                    if (headerItemId != null)
                    {
                        partialDesignIds.Add(new Guid(headerItemId).ToString("B").ToUpperInvariant());
                        _logger.LogDebug($"Found Header partial design: {headerItemId}");
                    }
                }

                // Check for Footer partial
                var footerPartial = args.PartialsConfig.Partials.FirstOrDefault(p => 
                    p.Name.Equals("Footer", StringComparison.OrdinalIgnoreCase));
                
                if (footerPartial != null)
                {
                    var footerItemId = await GetPartialDesignItemIdAsync(args, footerPartial.Name);
                    if (footerItemId != null)
                    {
                        partialDesignIds.Add(new Guid(footerItemId).ToString("B").ToUpperInvariant());
                        _logger.LogDebug($"Found Footer partial design: {footerItemId}");
                    }
                }

                _logger.LogDebug($"Found {partialDesignIds.Count} partial designs to link to default page design");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting partial design IDs");
            }

            return partialDesignIds;
        }

        private async Task<string?> GetPartialDesignItemIdAsync(AutoArgs args, string partialName)
        {
            try
            {
                // Build the partial design item path
                string partialPath = $"{args.SiteConfig.Site.PartialDesignsPath}/{partialName}";
                
                // Get the partial design item
                var partialItem = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, partialPath, verbose: true);

                if (partialItem != null)
                {
                    return partialItem.ItemId;
                }
                else
                {
                    _logger.LogWarning($"Partial design item not found: {partialName} at path: {partialPath}");
                    return null;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error getting partial design item ID for: {partialName}");
                return null;
            }
        }
    }
}
