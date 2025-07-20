using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SiteKit.CLI.Services;
using SiteKit.Types;

namespace SiteKit.CLI.Services.Deploy
{
    public class _BuildVariants : IRun
    {
        private readonly IGraphQLService _graphQLService;
        private readonly ILogger _logger;

        public _BuildVariants(IGraphQLService graphQLService, ILogger logger)
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
                    if (component.Variants != null && component.Variants.Count > 0)
                    {
                        await CreateOrUpdateAsync(args, component);
                    }
                }
                
                _logger.LogInformation($"Successfully processed variants for {components.Count} components");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing component variants");
                args.IsValid = false;
                args.ValidationMessage = $"Error processing component variants: {ex.Message}";
                throw;
            }
        }

        private async Task CreateOrUpdateAsync(AutoArgs args, ComponentDefinition component)
        {
            try
            {
                if (component.Variants != null && component.Variants.Count > 0)
                {
                    // 1 - Create or Update the Variants folder
                    await CreateVariantsFolderAsync(args, component);

                    // 2 - Add the variant items
                    await CreateVariantItemsAsync(args, component);
                }

                _logger.LogInformation($"Successfully processed variants for component: {component.Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing variants for component: {component.Name}");
                throw;
            }
        }

        private async Task CreateVariantsFolderAsync(AutoArgs args, ComponentDefinition component)
        {
            try
            {
                var site = args.SiteConfig.Site;
                var variantsFolderPath = $"{site.SitePath}/Presentation/Headless Variants/{component.Name}";

                // Try to retrieve the variants folder by path
                var existingVariantsFolder = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, variantsFolderPath, verbose: true);

                string variantsFolderId;

                if (existingVariantsFolder == null)
                {
                    _logger.LogInformation($"Creating variants folder: {component.Name} at path: {variantsFolderPath}");

                    // Get the parent Headless Variants folder
                    var parentVariantsPath = $"{site.SitePath}/Presentation/Headless Variants";
                    var parentVariantsFolder = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, parentVariantsPath, verbose: true);

                    if (parentVariantsFolder == null)
                    {
                        _logger.LogError($"Parent headless variants folder not found at path: {parentVariantsPath}");
                        args.IsValid = false;
                        args.ValidationMessage = $"Parent headless variants folder not found at path: {parentVariantsPath}";
                        return;
                    }

                    // Create the variants folder using the Variants Folder template
                    var createdVariantsFolderId = await _graphQLService.CreateItemAsync(
                        args.Endpoint,
                        args.AccessToken,
                        component.Name,
                        "{49C111D0-6867-4798-A724-1F103166E6E9}", // Variants Folder template ID
                        parentVariantsFolder.ItemId,
                        verbose: true);

                    if (createdVariantsFolderId == null)
                    {
                        _logger.LogError($"Failed to create variants folder: {component.Name}");
                        args.IsValid = false;
                        args.ValidationMessage = $"Failed to create variants folder: {component.Name}";
                        return;
                    }

                    variantsFolderId = createdVariantsFolderId;
                }
                else
                {
                    variantsFolderId = existingVariantsFolder.ItemId;
                    _logger.LogInformation($"Using existing variants folder: {component.Name} (ID: {variantsFolderId})");
                }

                // Set default fields for the variants folder
                await SetDefaultFieldsAsync(args, variantsFolderId);

                _logger.LogInformation($"Successfully processed variants folder: {component.Name}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating variants folder: {component.Name}");
                throw;
            }
        }

        private async Task CreateVariantItemsAsync(AutoArgs args, ComponentDefinition component)
        {
            try
            {
                var site = args.SiteConfig.Site;
                var variantsFolderPath = $"{site.SitePath}/Presentation/Headless Variants/{component.Name}";

                // Get the variants folder
                var variantsFolder = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, variantsFolderPath, verbose: true);

                if (variantsFolder == null)
                {
                    _logger.LogError($"Variants folder not found at path: {variantsFolderPath}");
                    return;
                }

                int order = 0;
                foreach (var variant in component.Variants)
                {
                    order++;
                    
                    var variantPath = $"{variantsFolderPath}/{variant}";

                    // Try to retrieve the variant item by path
                    var existingVariant = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, variantPath, verbose: true);

                    string variantId;

                    if (existingVariant == null)
                    {
                        _logger.LogInformation($"Creating variant item: {variant} at path: {variantPath}");

                        // Create the variant item using the Variant template
                        var createdVariantId = await _graphQLService.CreateItemAsync(
                            args.Endpoint,
                            args.AccessToken,
                            variant,
                            "{4D50CDAE-C2D9-4DE8-B080-8F992BFB1B55}", // Variant template ID
                            variantsFolder.ItemId,
                            verbose: true);

                        if (createdVariantId == null)
                        {
                            _logger.LogError($"Failed to create variant item: {variant}");
                            continue;
                        }

                        variantId = createdVariantId;
                    }
                    else
                    {
                        variantId = existingVariant.ItemId;
                        _logger.LogInformation($"Using existing variant item: {variant} (ID: {variantId})");
                    }

                    // Update variant item fields
                    var variantFields = new Dictionary<string, string>();
                    variantFields["__Sortorder"] = order.ToString();

                    await _graphQLService.UpdateItemAsync(args.Endpoint, args.AccessToken, variantId, variantFields, verbose: true);

                    // Set default fields for the variant item
                    await SetDefaultFieldsAsync(args, variantId);

                    _logger.LogInformation($"Successfully processed variant item: {variant}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating variant items for component: {component.Name}");
                throw;
            }
        }

        private async Task SetDefaultFieldsAsync(AutoArgs args, string itemId)
        {
            try
            {
                // Set any default fields that might be needed for variant items
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