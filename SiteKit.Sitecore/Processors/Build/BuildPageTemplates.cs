using Sitecore;
using Sitecore.Data;
using Sitecore.Data.Items;
using System;
using System.Linq;
using SiteKit.Types;

namespace SiteKit.Processors
{
    public class BuildPageTemplates : AutoBase
    {
        public void Process(AutoArgs args)
        {
            foreach (var pagetype in args.PageTypesConfig.PageTypes)
            {
                CreateOrUpdate(args, pagetype);
            }
        }

        private void CreateOrUpdate(AutoArgs args, PageType pagetype)
        {
            var id = args.GetUniqueID("page_template", pagetype.Name);
            Item template = Database.GetItem(id);

            if (template == null)
            {
                var folder = Database.GetItem(GetSite(args).SiteTemplatePath);
                template = folder.Add(pagetype.Name, new TemplateID(Sitecore.TemplateIDs.Template), id);
            }
            template.Editing.BeginEdit();
            template[FieldIDs.BaseTemplate] = GetBaseTemplate(args, pagetype);
            template[FieldIDs.Icon] = "Office/32x32/document_text.png";
            template.Editing.EndEdit();
            SetDefaultFields(template);

            if (pagetype.Fields != null && pagetype.Fields.Count() > 0)
            {
                // Ensure "Data" section exists
                var dataid = args.GetUniqueID("page_template", pagetype.Name + "/Data");
                var dataSection = Database.GetItem(dataid);
                if (dataSection == null)
                {
                    dataSection = template.Add("Data", new TemplateID(Sitecore.TemplateIDs.TemplateSection), dataid);
                }
                SetDefaultFields(dataSection);

                // Add fields under "Data"
                foreach (var field in pagetype.Fields)
                {
                    var fieldid = args.GetUniqueID("page_template", pagetype.Name + "/Data/" + field.Name);
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
        }

        private string GetBaseTemplate(AutoArgs args, PageType pagetype)
        {
            try
            {
                if (!string.IsNullOrWhiteSpace(args.SiteConfig.Site.Defaults.PageBaseTemplate))
                {
                    return GetId(args.SiteConfig.Site.Defaults.PageBaseTemplate);
                }
                else
                {
                    return GetId(GetSite(args).SiteTemplatePath + "/Page");
                }
            }
            catch
            {
                return "";
            }
        }
    }
}