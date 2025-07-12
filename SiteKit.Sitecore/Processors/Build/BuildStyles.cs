using Sitecore.Data;
using Sitecore.Data.Items;
using SiteKit.Types;

namespace SiteKit.Processors
{
    public class BuildStyles : AutoBase
    {
        public void Process(AutoArgs args)
        {
            var components = args.ComponentConfig.Components;
            foreach (var component in components)
            {
                if (component.Parameters != null && component.Parameters.Count > 0)
                {
                    CreateOrUpdate(args, component);
                }
            }
        }

        private void CreateOrUpdate(AutoArgs args, ComponentDefinition component)
        {
            foreach (var param in component.Parameters)
            {
                // 1 - Create or Update the Styles folder
                var id = args.GetUniqueID("component_parameters", param.Name);
                Item stylesFolder = Database.GetItem(id);
                if (stylesFolder == null)
                {
                    var folder = Database.GetItem(args.SiteConfig.Site.SitePath + "/Presentation/Styles");
                    stylesFolder = folder.Add(param.Name, new TemplateID(new ID("{C6DC7393-15BB-4CD7-B798-AB63E77EBAC4}")), id);
                }
                stylesFolder.Editing.BeginEdit();
                stylesFolder["Type"] = param.Type;
                stylesFolder.Editing.EndEdit();
                SetDefaultFields(stylesFolder);

                // 2 - Add the style options
                int order = 0;
                foreach (var option in param.Styles)
                {
                    var optionId = args.GetUniqueID("component_parameters_option", option.Name);
                    Item optionItem = Database.GetItem(optionId);
                    if (optionItem == null)
                    {
                        optionItem = stylesFolder.Add(option.Name, new TemplateID(new ID("{6B8AABEF-D650-46E0-97D0-C0B04F7F016B}")), optionId);
                    }
                    optionItem.Editing.BeginEdit();
                    optionItem["Value"] = option.Value;
                    optionItem["__Sortorder"] = (++order).ToString();
                    var allowed = optionItem["Allowed Renderings"];
                    var compoId = args.GetRenderingId(component).ToString();
                    if (allowed == "")
                        optionItem["Allowed Renderings"] = compoId;
                    else if (allowed.IndexOf(compoId) == -1)
                        optionItem["Allowed Renderings"] = allowed + "|" + compoId;

                    optionItem.Editing.EndEdit();
                    SetDefaultFields(optionItem);
                }
            }
        }
    }
}