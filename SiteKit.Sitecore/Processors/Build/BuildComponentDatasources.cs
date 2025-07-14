using Sitecore;
using Sitecore.Data;
using Sitecore.Data.Items;
using System;
using System.Linq;
using SiteKit.Types;

namespace SiteKit.Processors
{
    public class BuildComponentDatasources : AutoBase
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
            var id = args.GetUniqueID("component_datasource", component.Name);
            Item template = Database.GetItem(id);

            // Create the datasource template
            if (template == null)
            {
                var folder = Database.GetItem(GetSite(args).DatasourceTemplatePath + "/" + component.Category);
                template = folder.Add(component.Name, new TemplateID(Sitecore.TemplateIDs.Template), id);
            }
            template.Editing.BeginEdit();
            //add icon 
            var icon = component.Rendering?.Where(c => c.Name.Equals("__Icon", StringComparison.InvariantCultureIgnoreCase)).FirstOrDefault();
            if (icon != null)
            {
                template[FieldIDs.Icon] = icon.Value;
            }
            template.Editing.EndEdit();
            SetDefaultFields(template);

            // Add the fields of the data source template
            if (component.Fields != null && component.Fields.Count() > 0)
            {
                // Ensure "Data" section exists
                var dataid = args.GetUniqueID("component_datasource", component.Name + "/Data");
                var dataSection = Database.GetItem(dataid);
                if (dataSection == null)
                {
                    dataSection = template.Add("Data", new TemplateID(Sitecore.TemplateIDs.TemplateSection), dataid);
                }
                SetDefaultFields(dataSection);

                // Add fields under "Data"
                foreach (var field in component.Fields)
                {
                    var fieldid = args.GetUniqueID("component_datasource", component.Name + "/Data/" + field.Name);
                    var fieldItem = Database.GetItem(fieldid);
                    if (fieldItem == null)
                    {
                        fieldItem = dataSection.Add(field.Name, new TemplateID(Sitecore.TemplateIDs.TemplateField), fieldid);
                    }
                    SetDefaultFields(fieldItem);
                    fieldItem.Editing.BeginEdit();
                    fieldItem["Type"] = field.Type;
                    fieldItem.Editing.EndEdit();
                }
            }

            // Create __Standard Values if not exists
            var stdid = args.GetUniqueID("component_datasource", component.Name + "/__Standard Values");
            var standardValues = Database.GetItem(stdid);
            if (standardValues == null)
            {
                standardValues = template.Add("__Standard Values", new TemplateID(template.ID), stdid);

                //set in the template
                template.Editing.BeginEdit();
                template[FieldIDs.StandardValues] = stdid.ToString();
                template.Editing.EndEdit();
            }
            standardValues.Editing.BeginEdit();
            standardValues[FieldIDs.DefaultWorkflow] = GetId(args.SiteConfig.Site.Defaults.DatasourceWorkflow);
            standardValues[FieldIDs.EnableItemFallback] = args.SiteConfig.Site.Defaults.LanguageFallback ? "1" : "0";
            standardValues.Editing.EndEdit();
            SetDefaultFields(standardValues);

            //Create Datasource Folder
            var folderId = args.GetUniqueID("component_datasource_folder", component.Name);
            Item folderTemplate = Database.GetItem(folderId);

            if (folderTemplate == null)
            {
                var folder = Database.GetItem(GetSite(args).DatasourceTemplatePath + "/" + component.Category);
                folderTemplate = folder.Add(component.Name + " Folder", new TemplateID(Sitecore.TemplateIDs.Template), folderId);
            }
            folderTemplate.Editing.BeginEdit();
            folderTemplate[FieldIDs.Icon] = "office/32x32/folder_window.png";
            folderTemplate.Editing.EndEdit();
            SetDefaultFields(folderTemplate);

            // Create __Standard Values if not exists
            var stdfolderid = args.GetUniqueID("component_datasource_folder", component.Name + "/__Standard Values");
            var standardValuesFolder = Database.GetItem(stdfolderid);
            if (standardValuesFolder == null)
            {
                standardValuesFolder = folderTemplate.Add("__Standard Values", new TemplateID(folderTemplate.ID), stdfolderid);
            }
            standardValuesFolder.Editing.BeginEdit();
            standardValuesFolder["__Masters"] = template.ID.ToString() + "|" + folderTemplate.ID.ToString();
            standardValuesFolder.Editing.EndEdit();
            SetDefaultFields(standardValuesFolder);
        }

    }
}