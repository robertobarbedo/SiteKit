using Sitecore.Data;
using Sitecore.Data.Items;
using SiteKit.Types;

namespace SiteKit.Processors
{
    public class BuildRenderingsPageContainers : AutoBase
    {
        public void Process(AutoArgs args)
        {
            var pagetypes = args.PageTypesConfig.PageTypes;
            foreach (var pagetype in pagetypes)
            {
                CreateOrUpdate(args, pagetype);
            }
        }

        private void CreateOrUpdate(AutoArgs args, PageType pagetype)
        {
            var name = pagetype.Name;
            var id = args.GetRenderingId(pagetype);
            Item rendering = Database.GetItem(id);

            if (rendering == null)
            {
                var folder = Database.GetItem(GetSite(args).RenderingPath);
                rendering = folder.Add(name, new TemplateID(new ID("{04646A89-996F-4EE7-878A-FFDBF1F0EF0D}")), id);
            }
            rendering.Editing.BeginEdit();
            rendering["componentName"] = name.Replace(" ", "");
            rendering["OtherProperties"] = "IsRenderingsWithDynamicPlaceholders=true";
            rendering["Parameters Template"] = "{45130BDB-BAE3-4EDC-AEA8-7B68B9D94C7F}";
            rendering.Editing.EndEdit();
            SetDefaultFields(rendering);
        }

    }
}