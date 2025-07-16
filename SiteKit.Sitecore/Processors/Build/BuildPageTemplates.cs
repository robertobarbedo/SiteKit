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
                //prepare sections
                foreach (var field in pagetype.Fields)
                    if (string.IsNullOrWhiteSpace(field.Section))
                        field.Section = "Content"; //Default

                // Group fields by section
                var fieldsBySection = pagetype.Fields.GroupBy(f => f.Section);

                foreach (var sectionGroup in fieldsBySection)
                {
                    var sectionName = sectionGroup.Key;

                    // Ensure section exists
                    var sectionId = args.GetUniqueID("page_template_section", pagetype.Name + "/" + sectionName);
                    var section = Database.GetItem(sectionId);
                    if (section == null)
                    {
                        section = template.Add(sectionName, new TemplateID(Sitecore.TemplateIDs.TemplateSection), sectionId);
                    }
                    SetDefaultFields(section);

                    // Add fields under this section
                    foreach (var field in sectionGroup)
                    {
                        var fieldid = args.GetUniqueID("page_template", pagetype.Name + "/" + sectionName + "/" + field.Name);
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