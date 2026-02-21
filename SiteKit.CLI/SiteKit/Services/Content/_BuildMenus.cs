using Microsoft.Extensions.Logging;
using SiteKit.Types;
using SiteKit.CLI.Services;

namespace SiteKit.CLI.Services.Content
{
    public class _BuildMenus : IRun
    {
        private readonly IGraphQLService _graphQLService;
        private readonly ILogger _logger;

        public _BuildMenus(IGraphQLService graphQLService, ILogger logger)
        {
            _graphQLService = graphQLService;
            _logger = logger;
        }

        public void Run(AutoArgs args)
        {
            Task.Run(async () => await ProcessAsync(args)).Wait();
        }

        public async Task ProcessAsync(AutoArgs args)
        {
            var menuTemplateId = args.NavigationConfig?.Navigation?.Templates?.Menu;
            if (string.IsNullOrEmpty(menuTemplateId))
            {
                _logger.LogDebug("Menu template ID not set in navigation.yaml, skipping menu creation");
                return;
            }

            var menuItemTemplateId = args.NavigationConfig?.Navigation?.Templates?.MenuItem;
            if (string.IsNullOrEmpty(menuItemTemplateId))
            {
                _logger.LogDebug("Menu Item template ID not set in navigation.yaml, skipping menu creation");
                return;
            }

            var menus = args.NavigationConfig?.Navigation?.Menus;
            if (menus == null || !menus.Any())
            {
                _logger.LogInformation("No menus found in navigation.yaml");
                return;
            }

            var sitePath = args.SiteConfig?.Site?.SitePath;
            if (string.IsNullOrEmpty(sitePath))
            {
                _logger.LogError("site_path not configured in site settings");
                return;
            }

            var menusPath = $"{sitePath}/Menus";

            var menusFolder = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, menusPath, verbose: true);
            if (menusFolder == null)
            {
                _logger.LogError($"Menus folder not found at: {menusPath}. Run 'sitekit navigation' first.");
                return;
            }

            _logger.LogInformation($"Processing {menus.Count} menu(s)");

            foreach (var menu in menus)
            {
                var menuName = menu.Key;
                var menuItems = menu.Value;

                if (menuItems == null || !menuItems.Any())
                {
                    _logger.LogWarning($"Menu '{menuName}' has no items, skipping");
                    continue;
                }

                await CreateMenuAsync(args, menusPath, menusFolder.ItemId, menuName, menuTemplateId, menuItemTemplateId, menuItems);
            }

            _logger.LogInformation("Menu processing completed");
        }

        private async Task CreateMenuAsync(AutoArgs args, string menusPath, string menusParentId,
            string menuName, string menuTemplateId, string menuItemTemplateId, List<MenuItem> items)
        {
            try
            {
                var menuPath = $"{menusPath}/{menuName}";

                var existingMenu = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, menuPath, verbose: true);
                string? menuId;

                if (existingMenu != null)
                {
                    _logger.LogInformation($"✓ Menu '{menuName}' already exists (ID: {existingMenu.ItemId})");
                    menuId = existingMenu.ItemId;
                }
                else
                {
                    _logger.LogInformation($"Creating menu: {menuName}");
                    menuId = await _graphQLService.CreateItemAsync(
                        args.Endpoint,
                        args.AccessToken,
                        menuName,
                        menuTemplateId,
                        menusParentId,
                        verbose: true);

                    if (menuId == null)
                    {
                        _logger.LogError($"Failed to create menu: {menuName}");
                        return;
                    }

                    _logger.LogInformation($"✓ Created menu '{menuName}' (ID: {menuId})");
                }

                await CreateMenuItemsAsync(args, menuPath, menuId, menuItemTemplateId, items);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating menu '{menuName}'");
            }
        }

        private async Task CreateMenuItemsAsync(AutoArgs args, string parentPath, string parentId,
            string menuItemTemplateId, List<MenuItem> items)
        {
            foreach (var item in items)
            {
                if (string.IsNullOrEmpty(item.Label))
                {
                    _logger.LogWarning("Skipping menu item with empty label");
                    continue;
                }

                await CreateMenuItemAsync(args, parentPath, parentId, menuItemTemplateId, item);
            }
        }

        private async Task CreateMenuItemAsync(AutoArgs args, string parentPath, string parentId,
            string menuItemTemplateId, MenuItem item)
        {
            try
            {
                var itemName = item.Label!;
                var itemPath = $"{parentPath}/{itemName}";

                var existingItem = await _graphQLService.GetItemByPathAsync(args.Endpoint, args.AccessToken, itemPath, verbose: true);
                string? itemId;

                if (existingItem != null)
                {
                    _logger.LogDebug($"Menu item '{itemName}' already exists, updating fields");
                    itemId = existingItem.ItemId;
                }
                else
                {
                    _logger.LogDebug($"Creating menu item: {itemName}");
                    itemId = await _graphQLService.CreateItemAsync(
                        args.Endpoint,
                        args.AccessToken,
                        itemName,
                        menuItemTemplateId,
                        parentId,
                        verbose: true);

                    if (itemId == null)
                    {
                        _logger.LogError($"Failed to create menu item: {itemName}");
                        return;
                    }
                }

                var linkXml = $"<link text=\"{EscapeXmlAttribute(item.Label!)}\" linktype=\"external\" url=\"{EscapeXmlAttribute(item.Link ?? "")}\" anchor=\"\" target=\"\" />";

                var fields = new Dictionary<string, string>
                {
                    { "menuLabel", item.Label! },
                    { "menuLink", linkXml }
                };

                var updateResult = await _graphQLService.UpdateItemAsync(args.Endpoint, args.AccessToken, itemId, fields, verbose: true);

                if (updateResult != null)
                {
                    _logger.LogDebug($"✓ Menu item '{itemName}' fields updated");
                }
                else
                {
                    _logger.LogError($"Failed to update fields for menu item: {itemName}");
                }

                if (item.Children != null && item.Children.Any())
                {
                    await CreateMenuItemsAsync(args, itemPath, itemId, menuItemTemplateId, item.Children);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Error creating menu item '{item.Label}'");
            }
        }

        private static string EscapeXmlAttribute(string value)
        {
            return value
                .Replace("&", "&amp;")
                .Replace("\"", "&quot;")
                .Replace("<", "&lt;")
                .Replace(">", "&gt;");
        }
    }
}
