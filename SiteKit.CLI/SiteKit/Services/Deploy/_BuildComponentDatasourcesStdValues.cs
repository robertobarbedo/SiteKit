using System;
using System.Linq;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using SiteKit.Types;
using SiteKit.CLI.Services;

namespace SiteKit.CLI.Services.Deploy
{
    public class _BuildComponentDatasourcesStdValues : IRun
    {
        private readonly IGraphQLService _graphQLService;
        private readonly ILogger _logger;

        public _BuildComponentDatasourcesStdValues(IGraphQLService graphQLService, ILogger logger)
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
                    await CreateOrUpdateStandardValuesAsync(args, component);
            }
        }

        private async Task CreateOrUpdateStandardValuesAsync(AutoArgs args, ComponentDefinition component)
        {
            try
            {
                var site = args.SiteConfig.Site;
                var templatePath = $"{site.DatasourceTemplatePath}/{component.Category}/{component.Name}";
                
                _logger.LogInformation($"Processing standard values for component: {component.Name}");

                // Get the template item to ensure it exists
                var templateItem = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, templatePath, verbose: true);
                
                if (templateItem == null)
                {
                    _logger.LogError($"Template not found at path: {templatePath}. Standard values cannot be created.");
                    args.IsValid = false;
                    args.ValidationMessage = $"Template not found: {templatePath}";
                    return;
                }

                // Standard Values can be retrieved by the template path + "/__Standard Values"
                var standardValuesPath = $"{templatePath}/__Standard Values";
                var standardValues = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, standardValuesPath, verbose: true);

                if (standardValues == null)
                {
                    _logger.LogWarning($"Standard values not found at path: {standardValuesPath}. It should have been created by the template creation process.");
                    // Don't fail here, just log a warning since the main template creation process should handle this
                    return;
                }

                // Prepare the fields to update on the standard values item
                var standardValuesFields = new Dictionary<string, string>();

                // Set system fields for workflow and language fallback
                if (!string.IsNullOrWhiteSpace(site.Defaults?.DatasourceWorkflow))
                {
                    standardValuesFields["__Default workflow"] = site.Defaults.DatasourceWorkflow;
                }

                if (site.Defaults != null)
                {
                    standardValuesFields["__Enable item fallback"] = site.Defaults.LanguageFallback ? "1" : "0";
                }

                // Set default values for component fields
                foreach (var field in component.Fields)
                {
                    if (!string.IsNullOrEmpty(field.Default))
                    {
                        standardValuesFields[field.Name] = field.Default;
                        _logger.LogDebug($"Setting default value for field '{field.Name}': {field.Default}");
                    }
                }

                if (standardValuesFields.Any())
                {
                    // Update the standard values item with all the field values
                    var updateResult = await _graphQLService.UpdateItemAsync(
                        args.Endpoint, 
                        args.AccessToken, 
                        standardValues.ItemId, 
                        standardValuesFields, 
                        verbose: true);

                    if (updateResult != null)
                    {
                        _logger.LogInformation($"Successfully updated standard values for component: {component.Name} with {standardValuesFields.Count} field values");
                    }
                    else
                    {
                        _logger.LogError($"Failed to update standard values for component: {component.Name}");
                        args.IsValid = false;
                        args.ValidationMessage = $"Failed to update standard values for component: {component.Name}";
                    }
                }
                else
                {
                    _logger.LogInformation($"No field defaults to set for component: {component.Name}");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error processing standard values for component: {component.Name}");
                args.IsValid = false;
                args.ValidationMessage = $"Error processing standard values: {component.Name} - {ex.Message}";
                throw;
            }
        }
    }
}