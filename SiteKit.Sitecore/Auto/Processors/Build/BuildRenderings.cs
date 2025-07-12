using Sitecore.Data;
using Sitecore.Data.Items;
using SiteKit.Types;

namespace SiteKit.Processors
{
    public class BuildRenderings : AutoBase
    {
        public void Process(AutoArgs args)
        {
            var components = args.ComponentConfig.Components;
            foreach (var component in components)
            {
                CreateOrUpdate(args, component);
            }
        }

        private void CreateOrUpdate(AutoArgs args, ComponentDefinition component)
        {
            var id = args.GetRenderingId(component);
            Item rendering = Database.GetItem(id);

            if (rendering == null)
            {
                var folder = Database.GetItem(GetSite(args).RenderingPath + "/" + component.Category);
                rendering = folder.Add(component.Name, new TemplateID(new ID("{04646A89-996F-4EE7-878A-FFDBF1F0EF0D}")), id);
            }
            rendering.Editing.BeginEdit();
            rendering["componentName"] = component.Name.Replace(" ", "");
            rendering["OtherProperties"] = "IsAutoDatasourceRendering=true&IsRenderingsWithDynamicPlaceholders=true";
            rendering["Parameters Template"] = "{45130BDB-BAE3-4EDC-AEA8-7B68B9D94C7F}";
            if (component.HasFields())
            {
                rendering["Datasource Location"] = $"query:$site/*[@@name='Data']/*[@@templatename='{component.Name} Folder']|query:$sharedSites/*[@@name='Data']/*[@@templatename='{component.Name} Folder']";
                rendering["Datasource Template"] = Database.GetItem(GetSite(args).DatasourceTemplatePath + "/" + component.Category + "/" + component.Name).Paths.FullPath;
            }

            //other props
            if (component.Rendering != null)
            {
                foreach (var extraField in component.Rendering)
                {
                    rendering[extraField.Name] = extraField.Value;
                }
            }

            rendering.Editing.EndEdit();
            SetDefaultFields(rendering);
        }

    }
}