using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SiteKit.Types;

namespace SiteKit.Processors
{
    public class ValidateYamlData
    {
        public void Process(AutoArgs args)
        {
            var validationErrors = new List<string>();

            // Validate YAML files exist
            if (!args.Yamls.ContainsKey("sitesettings") || string.IsNullOrEmpty(args.Yamls["sitesettings"]))
            {
                validationErrors.Add("Required YAML file 'sitesettings' is missing or empty.");
            }

            if (!args.Yamls.ContainsKey("components") || string.IsNullOrEmpty(args.Yamls["components"]))
            {
                validationErrors.Add("Required YAML file 'components' is missing or empty.");
            }

            if (!args.Yamls.ContainsKey("pagetypes") || string.IsNullOrEmpty(args.Yamls["pagetypes"]))
            {
                validationErrors.Add("Required YAML file 'pagetypes' is missing or empty.");
            }

            if (!args.Yamls.ContainsKey("composition") || string.IsNullOrEmpty(args.Yamls["composition"]))
            {
                validationErrors.Add("Required YAML file 'composition' is missing or empty.");
            }

            // Validate loaded configurations
            if (args.SiteConfig == null)
            {
                validationErrors.Add("SiteConfig failed to load properly.");
            }
            else if (args.SiteConfig.Site == null)
            {
                validationErrors.Add("SiteConfig contains no sites.");
            }

            if (args.ComponentConfig == null)
            {
                validationErrors.Add("ComponentConfig failed to load properly.");
            }
            else if (args.ComponentConfig.Components == null || args.ComponentConfig.Components.Count == 0)
            {
                validationErrors.Add("ComponentConfig contains no components.");
            }

            if (args.PageTypesConfig == null)
            {
                validationErrors.Add("PageTypesConfig failed to load properly.");
            }
            else if (args.PageTypesConfig.PageTypes == null || args.PageTypesConfig.PageTypes.Count == 0)
            {
                validationErrors.Add("PageTypesConfig contains no page types.");
            }

            if (args.CompositionConfig == null)
            {
                validationErrors.Add("CompositionConfig failed to load properly.");
            }
            else if (args.CompositionConfig.Composition == null)
            {
                validationErrors.Add("CompositionConfig contains no composition data.");
            }

            // Set validation results
            if (validationErrors.Count > 0)
            {
                args.IsValid = false;
                args.ValidationMessage = string.Join(Environment.NewLine, validationErrors);
                args.AbortPipeline();
            }
            else
            {
                args.IsValid = true;
                args.ValidationMessage = "All YAML data is valid.";
            }
        }
    }
} 