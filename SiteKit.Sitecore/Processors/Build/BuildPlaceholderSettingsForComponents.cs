using Sitecore.Data;
using Sitecore.Data.Items;
using System;
using System.Collections.Generic;
using System.Linq;
using SiteKit.Types;

namespace SiteKit.Processors
{
    public class BuildPlaceholderSettingsForComponents : AutoBase
    {
        public void Process(AutoArgs args)
        {
            var components = args.CompositionConfig.Composition.Components;
            foreach (var key in components.Keys)
            {
                var ph = components[key];
                foreach (var phKey in ph.Keys)
                {
                    CreateOrUpdate(args, key, phKey, components[key][phKey]);
                }
                
            }
        }

        private void CreateOrUpdate(AutoArgs args, string componentName, string phName, List<string> components)
        {
            var component = args.ComponentConfig.Components.Where(c => c.Name == componentName).FirstOrDefault();
            if (component == null)
            {
                return;
            }

            var name = component.Name.ToLower().Replace(" ", "-") + "-" + phName.ToLowerInvariant();
            {
                var id = args.GetUniqueID("component_placeholder_setting", name);
                Item item = Database.GetItem(id);
                if (item == null)
                {
                    var folder = Database.GetItem(GetSite(args).PlaceholderPath + "/" + component.Category);
                    item = folder.Add(name, new TemplateID(new ID("{D2A6884C-04D5-4089-A64E-D27CA9D68D4C}")), id);
                }
                item.Editing.BeginEdit();
                item["Placeholder Key"] = name + "-{*}";
                item["Allowed Controls"] = string.Join("|", GetControls(args, components));
                item.Editing.EndEdit();
                SetDefaultFields(item);

                //update rendering
                var rendering = Database.GetItem(args.GetRenderingId(component));
                var placeholders = rendering["Placeholders"] ?? "";
                if (placeholders.IndexOf(id.ToString()) == -1) {
                    rendering.Editing.BeginEdit();
                    rendering["Placeholders"] = placeholders + (placeholders == "" ? "" : "|") + id.ToString();
                    rendering.Editing.EndEdit();
                }
            }
            {
                var id = args.GetUniqueID("component_placeholder_setting_in_site", name);
                Item item = Database.GetItem(id);
                if (item == null)
                {
                    var folder = Database.GetItem(GetSite(args).SitePath + "/Presentation/Placeholder Settings/" + component.Category);
                    item = folder.Add(name, new TemplateID(new ID("{D2A6884C-04D5-4089-A64E-D27CA9D68D4C}")), id);
                }
                item.Editing.BeginEdit();
                item["Placeholder Key"] = name + "*";
                item["Allowed Controls"] = string.Join("|", GetControls(args, components));
                item.Editing.EndEdit();
                SetDefaultFields(item);
            }
        }

        private IEnumerable<string> GetControls(AutoArgs args, List<string> components)
        {
            foreach (var componentName in components)
            {
                var component = args.ComponentConfig.Components.Where(c => c.Name == componentName).FirstOrDefault();
                if (component == null)
                {
                    continue;
                }
                yield return args.GetRenderingId(component).ToString();
            }
        }
    }
}