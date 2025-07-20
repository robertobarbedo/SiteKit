using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SiteKit.CLI.Services.Validate
{
    public class _ValidatePageDefaultLayouts : IRun
    {
        private readonly ILogger _logger;

        public _ValidatePageDefaultLayouts(ILogger logger)
        {
            _logger = logger;
        }

        public void Run(AutoArgs args)
        {
            _logger.LogDebug("Starting page type layout validation");

            var errors = new List<string>();

            foreach (var pageType in args.PageTypesConfig.PageTypes)
            {
                _logger.LogDebug($"Validating layout for page type: {pageType.Name}");

                if (pageType.Layout != null && pageType.Layout.Any())
                {
                    ValidateLayoutComponents(pageType.Layout, args.ComponentConfig.Components, 
                        pageType.Name, "", errors);
                }
            }

            if (errors.Any())
            {
                _logger.LogError("Page type layout validation failed:");
                foreach (var error in errors)
                {
                    _logger.LogError($"  - {error}");
                }
                throw new InvalidOperationException($"Page type layout validation failed with {errors.Count} error(s). " +
                    "Only components without fields are allowed in page type layouts.");
            }

            _logger.LogDebug("Page type layout validation completed successfully");
        }

        private void ValidateLayoutComponents(List<SiteKit.Types.LayoutComponent> layoutComponents, 
            List<SiteKit.Types.ComponentDefinition> componentDefinitions, 
            string pageTypeName, string parentPath, List<string> errors)
        {
            foreach (var layoutComponent in layoutComponents)
            {
                var currentPath = string.IsNullOrEmpty(parentPath) 
                    ? layoutComponent.Component 
                    : $"{parentPath} > {layoutComponent.Component}";

                _logger.LogDebug($"Validating component: {layoutComponent.Component} in path: {currentPath}");

                // Find the component definition
                var componentDefinition = componentDefinitions
                    .FirstOrDefault(c => c.Name.Equals(layoutComponent.Component, StringComparison.OrdinalIgnoreCase));

                if (componentDefinition == null)
                {
                    errors.Add($"Page type '{pageTypeName}' layout path '{currentPath}': " +
                        $"Component '{layoutComponent.Component}' not found in component definitions");
                    continue;
                }

                // Check if component has fields
                if (componentDefinition.HasFields())
                {
                    var fieldNames = componentDefinition.Fields.Select(f => f.Name).ToList();
                    errors.Add($"Page type '{pageTypeName}' layout path '{currentPath}': " +
                        $"Component '{layoutComponent.Component}' has fields ({string.Join(", ", fieldNames)}) " +
                        "but only components without fields are allowed in page type layouts");
                }

                // Recursively validate children
                if (layoutComponent.Children != null && layoutComponent.Children.Any())
                {
                    ValidateLayoutComponents(layoutComponent.Children, componentDefinitions, 
                        pageTypeName, currentPath, errors);
                }
            }
        }
    }
}
