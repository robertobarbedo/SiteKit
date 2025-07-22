using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SiteKit.CLI.Services.Validate
{
    public class _ValidateInsertOptions : IRun
    {
        private readonly IGraphQLService _graphQLService;
        private readonly ILogger _logger;

        public _ValidateInsertOptions(IGraphQLService graphQLService, ILogger logger)
        {
            _graphQLService = graphQLService;
            _logger = logger;
        }

        public void Run(AutoArgs args)
        {
            _logger.LogDebug("Starting insert options validation");

            try
            {
                var task = Task.Run(async () => await ValidateInsertOptionsAsync(args));
                task.Wait();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating insert options");
                args.IsValid = false;
                args.ValidationMessage = $"Error validating insert options: {ex.Message}";
            }
        }

        private async Task ValidateInsertOptionsAsync(AutoArgs args)
        {
            // Check if PageTypesConfig is null
            if (args.PageTypesConfig?.PageTypes == null)
            {
                args.IsValid = false;
                args.ValidationMessage = "PageTypesConfig is not available for insert options validation";
                _logger.LogError("PageTypesConfig is null, cannot validate insert options");
                return;
            }

            // Check if SiteConfig is available
            if (args.SiteConfig?.Site?.SiteTemplatePath == null)
            {
                args.IsValid = false;
                args.ValidationMessage = "SiteConfig or SiteTemplatePath is not available for insert options validation";
                _logger.LogError("SiteConfig or SiteTemplatePath is null, cannot validate insert options");
                return;
            }

            var errors = new List<string>();

            // Get all page type names for fallback validation
            var pageTypeNames = args.PageTypesConfig.PageTypes
                .Select(p => p.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var pageType in args.PageTypesConfig.PageTypes)
            {
                if (pageType.InsertOptions != null && pageType.InsertOptions.Any())
                {
                    _logger.LogDebug($"Validating insert options for page type: {pageType.Name}");

                    foreach (var insertOption in pageType.InsertOptions)
                    {
                        if (string.IsNullOrWhiteSpace(insertOption))
                        {
                            errors.Add($"Page type '{pageType.Name}' has an empty or null insert option");
                            continue;
                        }

                        await ValidateInsertOptionExists(args, pageType.Name, insertOption, pageTypeNames, errors);
                    }
                }
            }

            if (errors.Any())
            {
                args.IsValid = false;
                args.ValidationMessage = $"Insert options validation failed with {errors.Count} error(s): " + string.Join("; ", errors);
                
                _logger.LogError("Insert options validation failed:");
                foreach (var error in errors)
                {
                    _logger.LogError($"  - {error}");
                }
            }
            else
            {
                _logger.LogDebug("Insert options validation completed successfully");
            }
        }

        private async Task ValidateInsertOptionExists(AutoArgs args, string pageTypeName, string insertOption, HashSet<string> pageTypeNames, List<string> errors)
        {
            try
            {
                var insertOptionPath = args.SiteConfig!.Site!.SiteTemplatePath + "/" + insertOption;
                _logger.LogDebug($"Validating insert option path: {insertOptionPath}");

                // First, check if the insert option exists in Sitecore
                var item = await _graphQLService.GetItemByPathAsync(args.Endpoint!, args.AccessToken!, insertOptionPath, verbose: true);

                if (item != null)
                {
                    // Found in Sitecore - validation passes
                    _logger.LogInformation($"✓ Insert option '{insertOption}' for page type '{pageTypeName}': {insertOptionPath} (Found in Sitecore: {item.Name})");
                    return;
                }

                // Not found in Sitecore, check if it exists in local page types list
                if (pageTypeNames.Contains(insertOption))
                {
                    // Found in local page types - validation passes (template will be created during build)
                    _logger.LogInformation($"✓ Insert option '{insertOption}' for page type '{pageTypeName}': Not found in Sitecore but exists in page types configuration (will be created during build)");
                    return;
                }

                // Not found in either Sitecore or local page types - validation fails
                var error = $"Page type '{pageTypeName}' has insert option '{insertOption}' which does not exist in Sitecore at path '{insertOptionPath}' and is not defined in the page types configuration";
                errors.Add(error);
                _logger.LogError(error);
            }
            catch (Exception ex)
            {
                var error = $"Error validating insert option '{insertOption}' for page type '{pageTypeName}': {ex.Message}";
                errors.Add(error);
                _logger.LogError(ex, error);
            }
        }
    }
}
