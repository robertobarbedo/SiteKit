using Sitecore.Data;
using Sitecore.Data.Items;
using SiteKit.Types;

namespace SiteKit.Processors
{
    public class BuildVariants : AutoBase
    {
        public void Process(AutoArgs args)
        {
            var components = args.ComponentConfig.Components;
            foreach (var component in components)
            {
                if (component.Variants != null && component.Variants.Count > 0)
                {
                    CreateOrUpdate(args, component);
                }
            }
        }

        private void CreateOrUpdate(AutoArgs args, ComponentDefinition component)
        {
            if (component.Variants != null && component.Variants.Count > 0)
            {
                // 1 - Create or Update the Variants folder
                var id = args.GetUniqueID("component_variant_folder", component.Name);
                Item folder = Database.GetItem(id);
                if (folder == null)
                {
                    var headlessVariants = Database.GetItem(args.SiteConfig.Site.SitePath + "/Presentation/Headless Variants");
                    folder = headlessVariants.Add(component.Name, new TemplateID(new ID("{49C111D0-6867-4798-A724-1F103166E6E9}")), id);
                }
                SetDefaultFields(folder);

                // 2 - Add the variants options
                int order = 0;
                foreach (var variant in component.Variants)
                {
                    var varId = args.GetUniqueID("component_variant_folder_item", variant);
                    Item variantItem = Database.GetItem(varId);
                    if (variantItem == null)
                    {
                        variantItem = folder.Add(variant, new TemplateID(new ID("{4D50CDAE-C2D9-4DE8-B080-8F992BFB1B55}")), varId);
                    }
                    variantItem.Editing.BeginEdit();
                    variantItem["__Sortorder"] = (++order).ToString();
                    variantItem.Editing.EndEdit();
                    SetDefaultFields(variantItem);
                }
            }
        }
    }
}