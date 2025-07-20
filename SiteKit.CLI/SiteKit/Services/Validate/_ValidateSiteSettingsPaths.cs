using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SiteKit.CLI.Services.Validate
{
    public class _ValidateSiteSettingsPaths : IRun
    {
        private readonly IGraphQLService _graphQLService;
        private readonly ILogger _logger;

        public _ValidateSiteSettingsPaths(IGraphQLService graphQLService, ILogger logger)
        {
            _graphQLService = graphQLService;
            _logger = logger;
        }

        public void Run(AutoArgs args)
        {
            _logger.LogDebug("Starting site settings paths validation");

            try
            {
                var task = Task.Run(async () => await ValidatePathsAsync(args));
                task.Wait();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating site settings paths");
                args.IsValid = false;
                args.ValidationMessage = $"Error validating site settings paths: {ex.Message}";
            }
        }

        private async Task ValidatePathsAsync(AutoArgs args)
        {
            var errors = new List<string>();

            try
            {
                // Validate required paths from SiteDefinition
                await ValidateRequiredPath(args, args.SiteConfig.Site.SitePath, "SitePath", errors);
                await ValidateRequiredPath(args, args.SiteConfig.Site.DictionaryPath, "DictionaryPath", errors);
                await ValidateRequiredPath(args, args.SiteConfig.Site.SiteTemplatePath, "SiteTemplatePath", errors);
                await ValidateRequiredPath(args, args.SiteConfig.Site.DatasourceTemplatePath, "DatasourceTemplatePath", errors);
                await ValidateRequiredPath(args, args.SiteConfig.Site.RenderingPath, "RenderingPath", errors);
                await ValidateRequiredPath(args, args.SiteConfig.Site.PlaceholderPath, "PlaceholderPath", errors);
                await ValidateRequiredPath(args, args.SiteConfig.Site.AvailableRenderingsPath, "AvailableRenderingsPath", errors);
                await ValidateRequiredPath(args, args.SiteConfig.Site.SitePlaceholderPath, "SitePlaceholderPath", errors);

                // Validate optional SiteDefaults paths (only if they are not empty)
                if (args.SiteConfig.Site.Defaults != null)
                {
                    await ValidateOptionalPath(args, args.SiteConfig.Site.Defaults.PageBaseTemplate, "SiteDefaults.PageBaseTemplate", errors);
                    await ValidateOptionalPath(args, args.SiteConfig.Site.Defaults.PageWorkflow, "SiteDefaults.PageWorkflow", errors);
                    await ValidateOptionalPath(args, args.SiteConfig.Site.Defaults.DatasourceWorkflow, "SiteDefaults.DatasourceWorkflow", errors);
                }

                if (errors.Any())
                {
                    args.IsValid = false;
                    args.ValidationMessage = $"Site settings paths validation failed with {errors.Count} error(s): " + string.Join("; ", errors);
                    
                    _logger.LogError("Site settings paths validation failed:");
                    foreach (var error in errors)
                    {
                        _logger.LogError($"  - {error}");
                    }
                }
                else
                {
                    _logger.LogInformation("All site settings paths validated successfully");
                    _logger.LogDebug("Site settings paths validation completed successfully");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during site settings paths validation");
                args.IsValid = false;
                args.ValidationMessage = $"Error validating site settings paths: {ex.Message}";
                throw;
            }
        }

        private async Task ValidateRequiredPath(AutoArgs args, string path, string pathName, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                var error = $"{pathName} is required but not configured";
                errors.Add(error);
                _logger.LogError(error);
                return;
            }

            await ValidatePathExists(args, path, pathName, errors, isRequired: true);
        }

        private async Task ValidateOptionalPath(AutoArgs args, string path, string pathName, List<string> errors)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                _logger.LogDebug($"{pathName} is not configured (optional)");
                return;
            }

            await ValidatePathExists(args, path, pathName, errors, isRequired: false);
        }

        private async Task ValidatePathExists(AutoArgs args, string path, string pathName, List<string> errors, bool isRequired)
        {
            try
            {
                _logger.LogDebug($"Validating {pathName}: {path}");

                var item = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, path, verbose: true);

                if (item == null)
                {
                    var error = $"{pathName} path '{path}' does not exist in Sitecore";
                    errors.Add(error);
                    _logger.LogError(error);
                }
                else
                {
                    _logger.LogInformation($"✓ {pathName}: {path} (Found: {item.Name})");
                }
            }
            catch (Exception ex)
            {
                var error = $"Error validating {pathName} path '{path}': {ex.Message}";
                errors.Add(error);
                _logger.LogError(ex, error);
            }
        }
    }
}
