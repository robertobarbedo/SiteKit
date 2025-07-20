using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SiteKit.CLI.Services.Validate
{
    public class _ValidateCompositionComponents : IRun
    {
        private readonly ILogger _logger;

        public _ValidateCompositionComponents(ILogger logger)
        {
            _logger = logger;
        }

        public void Run(AutoArgs args)
        {
            _logger.LogDebug("Starting composition components validation");

            var errors = new List<string>();

            // Validate Pages exist in PageTypes
            ValidatePageReferences(args, errors);

            // Validate Components exist in ComponentsConfig
            ValidateComponentReferences(args, errors);

            if (errors.Any())
            {
                _logger.LogError("Composition components validation failed:");
                foreach (var error in errors)
                {
                    _logger.LogError($"  - {error}");
                }
                throw new InvalidOperationException($"Composition components validation failed with {errors.Count} error(s). " +
                    "All pages and components referenced in composition must exist in their respective configurations.");
            }

            _logger.LogDebug("Composition components validation completed successfully");
        }

        private void ValidatePageReferences(AutoArgs args, List<string> errors)
        {
            _logger.LogDebug("Validating page references in composition");

            var pageTypeNames = args.PageTypesConfig.PageTypes
                .Select(p => p.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            foreach (var compositionPageKey in args.CompositionConfig.Composition.Pages.Keys)
            {
                if (!pageTypeNames.Contains(compositionPageKey))
                {
                    errors.Add($"Composition page '{compositionPageKey}' not found in page types configuration");
                    _logger.LogDebug($"Missing page type: {compositionPageKey}");
                }
                else
                {
                    _logger.LogDebug($"Found page type: {compositionPageKey}");
                }
            }
        }

        private void ValidateComponentReferences(AutoArgs args, List<string> errors)
        {
            _logger.LogDebug("Validating component references in composition");

            var componentNames = args.ComponentConfig.Components
                .Select(c => c.Name)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            // Check components directly referenced in composition.components
            foreach (var compositionComponentKey in args.CompositionConfig.Composition.Components.Keys)
            {
                if (!componentNames.Contains(compositionComponentKey))
                {
                    errors.Add($"Composition component '{compositionComponentKey}' not found in components configuration");
                    _logger.LogDebug($"Missing component: {compositionComponentKey}");
                }
                else
                {
                    _logger.LogDebug($"Found component: {compositionComponentKey}");
                }
            }

            // Check components referenced in groups
            foreach (var group in args.CompositionConfig.Composition.Groups)
            {
                _logger.LogDebug($"Validating group: {group.Key}");
                
                foreach (var componentName in group.Value)
                {
                    if (!componentNames.Contains(componentName))
                    {
                        errors.Add($"Component '{componentName}' in group '{group.Key}' not found in components configuration");
                        _logger.LogDebug($"Missing component in group {group.Key}: {componentName}");
                    }
                    else
                    {
                        _logger.LogDebug($"Found component in group {group.Key}: {componentName}");
                    }
                }
            }

            // Check components referenced within other component compositions
            foreach (var component in args.CompositionConfig.Composition.Components)
            {
                _logger.LogDebug($"Validating composition for component: {component.Key}");
                
                foreach (var placeholder in component.Value)
                {
                    foreach (var groupOrComponentName in placeholder.Value)
                    {
                        // Check if it's a group reference
                        if (args.CompositionConfig.Composition.Groups.ContainsKey(groupOrComponentName))
                        {
                            _logger.LogDebug($"Found group reference: {groupOrComponentName}");
                            continue;
                        }

                        // Check if it's a direct component reference
                        if (!componentNames.Contains(groupOrComponentName))
                        {
                            errors.Add($"Component '{groupOrComponentName}' referenced in component '{component.Key}' placeholder '{placeholder.Key}' not found in components configuration");
                            _logger.LogDebug($"Missing component reference in {component.Key}.{placeholder.Key}: {groupOrComponentName}");
                        }
                        else
                        {
                            _logger.LogDebug($"Found component reference in {component.Key}.{placeholder.Key}: {groupOrComponentName}");
                        }
                    }
                }
            }
        }
    }
}
