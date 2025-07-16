using Sitecore;
using Sitecore.Data;
using Sitecore.Data.Items;
using System;
using System.Linq;
using SiteKit.Types;

namespace SiteKit.Processors
{
    public class BuildPageTemplatesStdValues : AutoBase
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
                return;

            // Create __Standard Values if not exists
            
            var stdid = args.GetUniqueID("page_template", pagetype.Name + "/__Standard Values");
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
            standardValues[FieldIDs.DefaultWorkflow] = GetId(args.SiteConfig.Site.Defaults.PageWorkflow);
            standardValues[FieldIDs.EnableItemFallback] = args.SiteConfig.Site.Defaults.LanguageFallback ? "1" : "0";
            if (pagetype.Fields != null)
            {
                foreach (var field in pagetype.Fields)
                {
                    if (!string.IsNullOrEmpty(field.Default))
                        standardValues[field.Name] = field.Default;
                }
            }
            standardValues.Editing.EndEdit();
            SetDefaultFields(standardValues);

        }
    }
}