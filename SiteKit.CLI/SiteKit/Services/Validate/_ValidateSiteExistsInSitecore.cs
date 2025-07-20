using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SiteKit.CLI.Services.Validate
{
    public class _ValidateSiteExistsInSitecore : IRun
    {
        private readonly IGraphQLService _graphQLService;
        private readonly ILogger _logger;

        public _ValidateSiteExistsInSitecore(IGraphQLService graphQLService, ILogger logger)
        {
            _graphQLService = graphQLService;
            _logger = logger;
        }

        public void Run(AutoArgs args)
        {
            _logger.LogDebug($"Starting site existence validation for: {args.SiteName}");

            try
            {
                var task = Task.Run(async () => await ValidateSiteExistsAsync(args));
                task.Wait();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error validating site existence for: {args.SiteName}");
                args.IsValid = false;
                args.ValidationMessage = $"Error validating site existence: {ex.Message}";
            }
        }

        private async Task ValidateSiteExistsAsync(AutoArgs args)
        {
            try
            {
                var siteResponse = await _graphQLService.GetSiteAsync(args.Endpoint, args.AccessToken, args.SiteName, verbose: true);

                if (siteResponse == null)
                {
                    args.IsValid = false;
                    args.ValidationMessage = $"Site '{args.SiteName}' does not exist in Sitecore or is not configured properly.";
                    _logger.LogError($"Site '{args.SiteName}' not found in Sitecore");
                    return;
                }

                _logger.LogInformation($"Site '{args.SiteName}' found in Sitecore:");
                _logger.LogInformation($"  - Name: {siteResponse.Name}");
                _logger.LogInformation($"  - Domain: {siteResponse.Domain}");
                _logger.LogInformation($"  - Root Path: {siteResponse.RootPath}");
                _logger.LogInformation($"  - Start Path: {siteResponse.StartPath}");
                _logger.LogInformation($"  - Browser Title: {siteResponse.BrowserTitle}");
                _logger.LogInformation($"  - Cache HTML: {siteResponse.CacheHtml}");
                _logger.LogInformation($"  - Cache Media: {siteResponse.CacheMedia}");
                _logger.LogInformation($"  - Enable Preview: {siteResponse.EnablePreview}");

                _logger.LogDebug($"Site existence validation completed successfully for: {args.SiteName}");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error during site validation for: {args.SiteName}");
                args.IsValid = false;
                args.ValidationMessage = $"Error validating site: {ex.Message}";
                throw;
            }
        }
    }
}
