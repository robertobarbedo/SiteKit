using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using SiteKit.CLI.Services;
using SiteKit.Types;

namespace SiteKit.CLI.Services.Deploy
{
    public class _BuildComponentCategoryFolders : IRun
    {
        private readonly IGraphQLService _graphQLService;
        private readonly ILogger _logger;

        public _BuildComponentCategoryFolders(IGraphQLService graphQLService, ILogger logger)
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
            var types = args.ComponentConfig.Components.Select(c => c.Category).Distinct().OrderBy(c => c).ToList();

            var site = args.SiteConfig.Site;

            foreach (var type in types)
            {
                //datasource_template_path
                await CreateOrUpdateAsync("{0437FEE2-44C9-46A6-ABE9-28858D9FEE8C}", type, site.DatasourceTemplatePath, args);

                //rendering_path
                await CreateOrUpdateAsync("{7EE0975B-0698-493E-B3A2-0B2EF33D0522}", type, site.RenderingPath, args);

                //available_ren_path
                await CreateOrUpdateAsync("{76DA0A8D-FC7E-42B2-AF1E-205B49E43F98}", type, site.AvailableRenderingsPath, args);

                //placeholder_s_path
                await CreateOrUpdateAsync("{C3B037A0-46E5-4B67-AC7A-A144B962A56F}", type, site.PlaceholderPath, args);

                //placeholder_s_path_in_site
                await CreateOrUpdateAsync("{52288E39-7830-4694-B62D-32A54C6EF7BA}", type, site.SitePath + "/Presentation/Placeholder Settings", args);
            }
        }

        private async Task CreateOrUpdateAsync(string templateId, string itemName, string parentFolderPath, AutoArgs args)
        {
            try
            {
                // Construct the full path where the item should exist
                var itemPath = $"{parentFolderPath}/{itemName}";

                // First, try to get the item by path to see if it already exists
                var existingItem = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, itemPath, verbose: true);

                if (existingItem == null)
                {
                    _logger.LogInformation($"Creating folder item: {itemName} at path: {parentFolderPath}");

                    // Get the parent folder to ensure it exists
                    var parentItem = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, parentFolderPath, verbose: true);
                    
                    if (parentItem == null)
                    {
                        _logger.LogError($"Parent folder not found at path: {parentFolderPath}");
                        args.IsValid = false;
                        args.ValidationMessage = $"Parent folder not found at path: {parentFolderPath}";
                        return;
                    }

                    // Create the new folder item
                    var newItemId = await _graphQLService.CreateItemAsync(args.Endpoint, args.AccessToken, itemName, templateId, parentItem.ItemId, verbose: true);
                    
                    if (newItemId != null)
                    {
                        _logger.LogInformation($"Successfully created folder: {itemName} with ID: {newItemId}");
                    }
                    else
                    {
                        _logger.LogError($"Failed to create folder: {itemName} at path: {parentFolderPath}");
                        args.IsValid = false;
                        args.ValidationMessage = $"Failed to create folder: {itemName} at path: {parentFolderPath}";
                    }
                }
                else
                {
                    _logger.LogInformation($"Folder already exists: {itemName} at path: {itemPath} (ID: {existingItem.ItemId})");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating/updating folder: {itemName} at path: {parentFolderPath}");
                args.IsValid = false;
                args.ValidationMessage = $"Error creating/updating folder: {itemName} - {ex.Message}";
            }
        }

    }
}
