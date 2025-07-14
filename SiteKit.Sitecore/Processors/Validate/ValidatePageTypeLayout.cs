using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SiteKit.Types;

namespace SiteKit.Processors
{
    public class ValidatePageTypeLayout
    {
        public void Process(AutoArgs args)
        {
            var validationErrors = new List<string>();

            // Only validate if we have valid configurations
            if (args.PageTypesConfig?.PageTypes != null && args.ComponentConfig?.Components != null)
            {
                foreach (var pageType in args.PageTypesConfig.PageTypes)
                {
                    if (pageType.Layout != null)
                    {
                        ValidateLayoutComponents(pageType.Layout, args.ComponentConfig.Components, validationErrors, pageType.Name);
                    }
                }
            }

            // Set validation results
            if (validationErrors.Count > 0)
            {
                args.IsValid = false;
                args.ValidationMessage = string.Join(Environment.NewLine, validationErrors);
                args.AbortPipeline();
            }
        }

        private void ValidateLayoutComponents(List<LayoutComponent> layoutComponents, List<ComponentDefinition> availableComponents, List<string> validationErrors, string pageTypeName)
        {
            foreach (var layoutComponent in layoutComponents)
            {
                // Check if the component exists in ComponentConfig
                var componentDefinition = availableComponents.FirstOrDefault(c => c.Name == layoutComponent.Component);
                
                if (componentDefinition == null)
                {
                    validationErrors.Add($"Page type '{pageTypeName}' layout contains component '{layoutComponent.Component}' which does not exist in ComponentConfig.");
                }
                else
                {
                    // Check if the component has fields (should not have fields)
                    if (componentDefinition.HasFields())
                    {
                        validationErrors.Add($"Page type '{pageTypeName}' layout contains component '{layoutComponent.Component}' which has fields. Only components with no data source are allowed in layout.");
                    }
                }

                // Recursively validate children
                if (layoutComponent.Children != null && layoutComponent.Children.Count > 0)
                {
                    ValidateLayoutComponents(layoutComponent.Children, availableComponents, validationErrors, pageTypeName);
                }
            }
        }
    }
} 