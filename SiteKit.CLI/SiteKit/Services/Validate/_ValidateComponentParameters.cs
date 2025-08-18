using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace SiteKit.CLI.Services.Validate
{
    public class _ValidateComponentParameters : IRun
    {
        private readonly IGraphQLService _graphQLService;
        private readonly ILogger _logger;

        public _ValidateComponentParameters(IGraphQLService graphQLService, ILogger logger)
        {
            _graphQLService = graphQLService;
            _logger = logger;
        }

        public void Run(AutoArgs args)
        {
            if (args.ComponentConfig?.Components == null)
            {
                _logger.LogInformation("No components found to validate.");
                return;
            }

            var hasErrors = false;

            foreach (var component in args.ComponentConfig.Components)
            {
                if (component.Parameters != null && component.Parameters.Any())
                {
                    foreach (var parameter in component.Parameters)
                    {
                        // Validate that parameter has all required properties
                        if (string.IsNullOrWhiteSpace(parameter.Name))
                        {
                            _logger.LogError($"Component '{component.Name}': Parameter is missing required 'name' property.");
                            hasErrors = true;
                        }

                        if (string.IsNullOrWhiteSpace(parameter.Type))
                        {
                            _logger.LogError($"Component '{component.Name}': Parameter '{parameter.Name}' is missing required 'type' property.");
                            hasErrors = true;
                        }

                        // Validate that parameter has styles property and it's not empty
                        if (parameter.Styles == null || !parameter.Styles.Any())
                        {
                            _logger.LogError($"Component '{component.Name}': Parameter '{parameter.Name}' is missing required 'styles' property or styles list is empty. Parameters must have at least one style with name and value.");
                            hasErrors = true;
                        }
                        else
                        {
                            // Validate each style has required properties
                            foreach (var style in parameter.Styles)
                            {
                                if (string.IsNullOrWhiteSpace(style.Name))
                                {
                                    _logger.LogError($"Component '{component.Name}': Parameter '{parameter.Name}' has a style missing required 'name' property.");
                                    hasErrors = true;
                                }

                                if (string.IsNullOrWhiteSpace(style.Value))
                                {
                                    _logger.LogError($"Component '{component.Name}': Parameter '{parameter.Name}' has a style '{style.Name}' missing required 'value' property.");
                                    hasErrors = true;
                                }
                            }
                        }
                    }
                }
            }

            if (!hasErrors)
            {
                _logger.LogInformation("All component parameters validation passed.");
            }
            else
            {
                args.IsValid = false;
                args.ValidationMessage += "Component parameters validation failed. ";
            }
        }
    }
}
