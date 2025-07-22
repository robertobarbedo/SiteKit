using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SiteKit.CLI.Services.Validate
{
    public class _ValidatePartialsLayouts : IRun
    {
        private readonly ILogger _logger;

        public _ValidatePartialsLayouts(ILogger logger)
        {
            _logger = logger;
        }

        public void Run(AutoArgs args)
        {
            _logger.LogDebug("Starting partials layout validation");

            var errors = new List<string>();

            if (args.PartialsConfig?.Partials == null || !args.PartialsConfig.Partials.Any())
            {
                _logger.LogDebug("No partials found to validate");
                return;
            }

            foreach (var partial in args.PartialsConfig.Partials)
            {
                _logger.LogDebug($"Validating layout for partial: {partial.Name}");

                if (partial.Layout != null && partial.Layout.Any())
                {
                    ValidateLayoutComponents(partial.Layout, args.ComponentConfig.Components, 
                        partial.Name, "", errors);
                }
            }

            if (errors.Any())
            {
                _logger.LogError("Partials layout validation failed:");
                foreach (var error in errors)
                {
                    _logger.LogError($"  - {error}");
                }
                throw new InvalidOperationException($"Partials layout validation failed with {errors.Count} error(s). " +
                    "Only components without fields are allowed in partial layouts.");
            }

            _logger.LogDebug("Partials layout validation completed successfully");
        }

        private void ValidateLayoutComponents(List<SiteKit.Types.LayoutComponent> layoutComponents, 
            List<SiteKit.Types.ComponentDefinition> componentDefinitions, 
            string partialName, string parentPath, List<string> errors)
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
                    errors.Add($"Partial '{partialName}' layout path '{currentPath}': " +
                        $"Component '{layoutComponent.Component}' not found in component definitions");
                    continue;
                }

                // Check if component has fields
                if (componentDefinition.HasFields())
                {
                    var fieldNames = componentDefinition.Fields.Select(f => f.Name).ToList();
                    errors.Add($"Partial '{partialName}' layout path '{currentPath}': " +
                        $"Component '{layoutComponent.Component}' has fields ({string.Join(", ", fieldNames)}) " +
                        "but only components without fields are allowed in partial layouts");
                }

                // Recursively validate children
                if (layoutComponent.Children != null && layoutComponent.Children.Any())
                {
                    ValidateLayoutComponents(layoutComponent.Children, componentDefinitions, 
                        partialName, currentPath, errors);
                }
            }
        }
    }
}
