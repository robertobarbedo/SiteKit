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
                await ValidatePathAndCreateIfNotExists(args, args.SiteConfig.Site.DatasourceTemplatePath, "DatasourceTemplatePath", errors, "{0437FEE2-44C9-46A6-ABE9-28858D9FEE8C}");
                await ValidatePathAndCreateIfNotExists(args, args.SiteConfig.Site.RenderingPath, "RenderingPath", errors, "{7EE0975B-0698-493E-B3A2-0B2EF33D0522}");
                await ValidatePathAndCreateIfNotExists(args, args.SiteConfig.Site.PlaceholderPath, "PlaceholderPath", errors, "{C3B037A0-46E5-4B67-AC7A-A144B962A56F}");
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

        private async Task ValidatePathAndCreateIfNotExists(AutoArgs args, string path, string pathName, List<string> errors, string templateId)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                var error = $"{pathName} is required but not configured";
                errors.Add(error);
                _logger.LogError(error);
                return;
            }

            // Validate required parameters
            if (string.IsNullOrEmpty(args.Endpoint) || string.IsNullOrEmpty(args.AccessToken))
            {
                var error = $"Endpoint or AccessToken is missing for {pathName} validation";
                errors.Add(error);
                _logger.LogError(error);
                return;
            }

            try
            {
                _logger.LogDebug($"Validating and ensuring path exists: {pathName}: {path}");

                // First check if the full path already exists
                var item = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, path, verbose: true);
                if (item != null)
                {
                    _logger.LogInformation($"✓ {pathName}: {path} (Already exists: {item.Name})");
                    return;
                }

                // Path doesn't exist, need to find the existing parent and create missing segments
                await CreateMissingPathSegments(args, path, pathName, templateId);
                
                _logger.LogInformation($"✓ {pathName}: {path} (Created successfully)");
            }
            catch (Exception ex)
            {
                var error = $"Error validating/creating {pathName} path '{path}': {ex.Message}";
                errors.Add(error);
                _logger.LogError(ex, error);
            }
        }

        private async Task CreateMissingPathSegments(AutoArgs args, string fullPath, string pathName, string templateId)
        {
            // Validate required parameters
            if (string.IsNullOrEmpty(args.Endpoint) || string.IsNullOrEmpty(args.AccessToken))
            {
                throw new InvalidOperationException("Endpoint or AccessToken is missing");
            }

            // Split the path into segments
            var pathSegments = fullPath.Split('/', StringSplitOptions.RemoveEmptyEntries);
            
            if (pathSegments.Length == 0)
            {
                throw new InvalidOperationException($"Invalid path format: {fullPath}");
            }

            // Find the deepest existing parent path
            string? existingParentPath = null;
            string? existingParentId = null;
            int existingSegmentCount = 0;

            // Start from the full path and work backwards to find an existing parent
            for (int i = pathSegments.Length; i > 0; i--)
            {
                var testPath = "/" + string.Join("/", pathSegments.Take(i));
                
                _logger.LogDebug($"Checking if path exists: {testPath}");
                
                var testItem = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, testPath, verbose: true);
                if (testItem != null)
                {
                    existingParentPath = testPath;
                    existingParentId = testItem.ItemId;
                    existingSegmentCount = i;
                    _logger.LogDebug($"Found existing parent: {existingParentPath} (ID: {existingParentId})");
                    break;
                }
            }

            if (string.IsNullOrEmpty(existingParentId) || string.IsNullOrEmpty(existingParentPath))
            {
                throw new InvalidOperationException($"Could not find any existing parent path for: {fullPath}");
            }

            // Create missing segments one by one
            string currentParentId = existingParentId;
            string currentPath = existingParentPath;

            for (int i = existingSegmentCount; i < pathSegments.Length; i++)
            {
                var segmentName = pathSegments[i];
                var newPath = currentPath + "/" + segmentName;
                
                _logger.LogDebug($"Creating path segment: {newPath} under parent ID: {currentParentId}");
                
                var newItemId = await _graphQLService.CreateItemAsync(
                    args.Endpoint,
                    args.AccessToken,
                    segmentName,
                    templateId,
                    currentParentId,
                    verbose: true);

                if (string.IsNullOrEmpty(newItemId))
                {
                    throw new InvalidOperationException($"Failed to create path segment: {newPath}");
                }

                _logger.LogInformation($"Created path segment: {newPath} (ID: {newItemId})");
                
                // Update for next iteration
                currentParentId = newItemId;
                currentPath = newPath;
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
            // Validate required parameters
            if (string.IsNullOrEmpty(args.Endpoint) || string.IsNullOrEmpty(args.AccessToken))
            {
                var error = $"Endpoint or AccessToken is missing for {pathName} validation";
                errors.Add(error);
                _logger.LogError(error);
                return;
            }

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
