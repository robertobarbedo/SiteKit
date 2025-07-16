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
            if (!string.IsNullOrWhiteSpace(component.Icon))
            {
                template[FieldIDs.Icon] = component.Icon;
            }
            template.Editing.EndEdit();
            SetDefaultFields(template);



            // Add the fields of the data source template
            if (component.Fields != null && component.Fields.Count() > 0)
            {
                //prepare sections
                foreach (var field in component.Fields)
                    if (string.IsNullOrWhiteSpace(field.Section))
                        field.Section = "Content"; //Default

                // Group fields by section
                var fieldsBySection = component.Fields.GroupBy(f => f.Section);

                foreach (var sectionGroup in fieldsBySection)
                {
                    var sectionName = sectionGroup.Key;

                    // Ensure section exists
                    var sectionId = args.GetUniqueID("component_datasource_section", component.Name + "/" + sectionName);
                    var section = Database.GetItem(sectionId);
                    if (section == null)
                    {
                        section = template.Add(sectionName, new TemplateID(Sitecore.TemplateIDs.TemplateSection), sectionId);
                    }
                    SetDefaultFields(section);

                    // Add fields under this section
                    foreach (var field in sectionGroup)
                    {
                        var fieldid = args.GetUniqueID("component_datasource", component.Name + "/" + sectionName + "/" + field.Name);
                        var fieldItem = Database.GetItem(fieldid);
                        if (fieldItem == null)
                        {
                            fieldItem = section.Add(field.Name, new TemplateID(Sitecore.TemplateIDs.TemplateField), fieldid);
                        }
                        SetDefaultFields(fieldItem);
                        fieldItem.Editing.BeginEdit();
                        fieldItem["Type"] = field.Type;
                        if (!string.IsNullOrWhiteSpace(field.Source))
                            fieldItem["Source"] = field.Source;
                        fieldItem.Editing.EndEdit();
                    }
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
            foreach (var field in component.Fields)
            {
                if (!string.IsNullOrEmpty(field.Default))
                    standardValues[field.Name] = field.Default;
            }
            standardValues.Editing.EndEdit();
            SetDefaultFields(standardValues);


            //Folder
            {
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
                    folderTemplate.Editing.BeginEdit();
                    folderTemplate[FieldIDs.StandardValues] = stdfolderid.ToString();
                    folderTemplate.Editing.EndEdit();
                }
                standardValuesFolder.Editing.BeginEdit();
                standardValuesFolder["__Masters"] = template.ID.ToString() + "|" + folderTemplate.ID.ToString();
                standardValuesFolder.Editing.EndEdit();

                SetDefaultFields(standardValuesFolder);
            }
        }

    }
}