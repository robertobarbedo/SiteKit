using Microsoft.Extensions.Logging;
using SiteKit.CLI.Services;
using SiteKit.CLI.Services.Shared;

namespace SiteKit.CLI.Services.Navigation;

public class _CreateMenuDomain : IRun
{
    private readonly IGraphQLService _graphQLService;
    private readonly ILogger _logger;

    public _CreateMenuDomain(IGraphQLService graphQLService, ILogger logger)
    {
        _graphQLService = graphQLService;
        _logger = logger;
    }

    public void Run(AutoArgs args)
    {
        try
        {
            var templateId = args.NavigationConfig?.Navigation?.Templates?.MenuDomain;
            if (string.IsNullOrEmpty(templateId))
            {
                args.IsValid = false;
                args.ValidationMessage = "Menu Domain template ID not found in navigation.yaml";
                return;
            }

            var sitePath = args.SiteConfig?.Site?.SitePath;
            if (string.IsNullOrEmpty(sitePath))
            {
                args.IsValid = false;
                args.ValidationMessage = "site_path not found in sitesettings.yaml";
                return;
            }

            var itemId = CreateMenuDomainItem(args, sitePath, templateId).Result;
            if (string.IsNullOrEmpty(itemId))
            {
                args.IsValid = false;
                args.ValidationMessage = "Failed to create Menu Domain item";
                return;
            }

            _logger.LogInformation($"✓ Menu Domain item created (ID: {itemId})");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating Menu Domain item");
            args.IsValid = false;
            args.ValidationMessage = $"Error creating Menu Domain item: {ex.Message}";
        }
    }

    private async Task<string?> CreateMenuDomainItem(AutoArgs args, string sitePath, string templateId)
    {
        var itemPath = $"{sitePath}/Menus";
        _logger.LogDebug($"Checking if Menu Domain item exists at: {itemPath}");

        var existingItem = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, itemPath, verbose: true);

        if (existingItem != null)
        {
            _logger.LogInformation($"✓ Menu Domain item already exists (ID: {existingItem.ItemId})");
            return existingItem.ItemId;
        }

        _logger.LogInformation("Creating Menu Domain item...");

        var siteItem = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, sitePath, verbose: true);
        if (siteItem == null)
        {
            _logger.LogError($"Site path not found: {sitePath}");
            return null;
        }

        var itemId = await _graphQLService.CreateItemAsync(
            args.Endpoint,
            args.AccessToken,
            "Menus",
            templateId,
            siteItem.ItemId,
            verbose: true);

        return itemId;
    }
}
