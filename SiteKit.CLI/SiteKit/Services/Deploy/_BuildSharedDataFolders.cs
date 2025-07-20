using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using SiteKit.Types;
using SiteKit.CLI.Services;

namespace SiteKit.CLI.Services.Deploy
{
    public class _BuildSharedDataFolders : IRun
    {
        private readonly IGraphQLService _graphQLService;
        private readonly ILogger _logger;

        public _BuildSharedDataFolders(IGraphQLService graphQLService, ILogger logger)
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
                if (component.HasFields())
                    await CreateOrUpdateAsync(args, component);
            }
        }

        private async Task CreateOrUpdateAsync(AutoArgs args, ComponentDefinition component)
        {
            try
            {
                var site = args.SiteConfig.Site;
                var sharedFolderName = $"{component.Name} Shared Folder";
                var sharedFolderPath = $"{site.SitePath}/Data/{sharedFolderName}";
                
                // Try to retrieve the shared folder by path
                var existingSharedFolder = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, sharedFolderPath, verbose: true);

                if (existingSharedFolder == null)
                {
                    _logger.LogInformation($"Creating shared data folder: {sharedFolderName} at path: {sharedFolderPath}");
                    
                    // Get the folder template
                    var folderTemplatePath = $"{site.DatasourceTemplatePath}/{component.Category}/{component.Name} Folder";
                    var folderTemplate = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, folderTemplatePath, verbose: true);
                    
                    if (folderTemplate == null)
                    {
                        _logger.LogError($"Folder template not found at path: {folderTemplatePath}");
                        args.IsValid = false;
                        args.ValidationMessage = $"Folder template not found at path: {folderTemplatePath}";
                        return;
                    }

                    // Get the parent Data folder
                    var dataFolderPath = $"{site.SitePath}/Data";
                    var dataFolder = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, dataFolderPath, verbose: true);
                    
                    if (dataFolder == null)
                    {
                        _logger.LogError($"Data folder not found at path: {dataFolderPath}");
                        args.IsValid = false;
                        args.ValidationMessage = $"Data folder not found at path: {dataFolderPath}";
                        return;
                    }

                    // Create the shared folder
                    var sharedFolderId = await _graphQLService.CreateItemAsync(
                        args.Endpoint,
                        args.AccessToken,
                        sharedFolderName,
                        folderTemplate.ItemId,
                        dataFolder.ItemId,
                        verbose: true);

                    if (sharedFolderId == null)
                    {
                        _logger.LogError($"Failed to create shared data folder: {sharedFolderName}");
                        args.IsValid = false;
                        args.ValidationMessage = $"Failed to create shared data folder: {sharedFolderName}";
                        return;
                    }

                    // Set default fields if needed
                    await SetDefaultFieldsAsync(args, sharedFolderId);

                    _logger.LogInformation($"Successfully created shared data folder: {sharedFolderName}");
                }
                else
                {
                    _logger.LogInformation($"Using existing shared data folder: {sharedFolderName} (ID: {existingSharedFolder.ItemId})");
                    
                    // Set default fields if needed
                    await SetDefaultFieldsAsync(args, existingSharedFolder.ItemId);
                }

                _logger.LogInformation($"Successfully processed shared data folder: {sharedFolderName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing shared data folder for component: {component.Name}");
                args.IsValid = false;
                args.ValidationMessage = $"Error processing shared data folder for component: {component.Name} - {ex.Message}";
                throw;
            }
        }

        private async Task SetDefaultFieldsAsync(AutoArgs args, string itemId)
        {
            try
            {
                // Set any default fields that might be needed for shared folders
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
                    _logger.LogInformation($"Set default fields for shared folder item ID: {itemId}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error setting default fields for shared folder item ID: {itemId}");
                // Don't throw here, as this is not critical for the main operation
            }
        }

    }
}