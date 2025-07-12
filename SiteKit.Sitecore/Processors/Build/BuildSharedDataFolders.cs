using Sitecore.Data;
using Sitecore.Data.Items;
using SiteKit.Types;

namespace SiteKit.Processors
{
    public class BuildSharedDataFolders : AutoBase
    {
        public void Process(AutoArgs args)
        {
            var components = args.ComponentConfig.Components;
            foreach (var component in components)
            {
                if (component.HasFields())
                    CreateOrUpdate(args, component);
            }
        }

        private void CreateOrUpdate(AutoArgs args, ComponentDefinition component)
        {
            var id = args.GetUniqueID("component_shared_datasource", component.Name);
            Item sharedFolder = Database.GetItem(id);

            if (sharedFolder == null)
            {
                ID folderTemplateId = Database.GetItem(GetSite(args).DatasourceTemplatePath + "/" + component.Category + "/" + component.Name + " Folder").ID;
                var folder = Database.GetItem(GetSite(args).SitePath + "/Data");
                sharedFolder = folder.Add(component.Name + " Shared Folder", new TemplateID(folderTemplateId), id);
            }
            SetDefaultFields(sharedFolder);
        }

    }
}