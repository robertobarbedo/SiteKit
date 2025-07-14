using Sitecore;
using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Fields;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Layouts;
using SiteKit.Types;
using System;
using System.Linq;
using System.Collections.Generic;

namespace SiteKit.Processors
{
    public class BuildPageTemplatesStdValuesLayout : AutoBase
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
            SetDefaultFields(standardValues);


            standardValues.Editing.BeginEdit();
            standardValues.Fields[FieldIDs.LayoutField].Reset();
            standardValues.Editing.EndEdit();


            //start
            string renderingsXml = LayoutField.GetFieldValue(standardValues.Fields[FieldIDs.LayoutField]);
            var layoutDefinition = LayoutDefinition.Parse(renderingsXml);

            if (layoutDefinition.Devices == null || layoutDefinition.Devices.Count == 0)
                throw new Exception("No available devices");

            BuildLayoutXML(layoutDefinition, args, pagetype, standardValues);

            standardValues.Editing.BeginEdit();
            LayoutField.SetFieldValue(standardValues.Fields[FieldIDs.LayoutField], layoutDefinition.ToXml());
            standardValues.Editing.EndEdit();
        }

        private void BuildLayoutXML(Sitecore.Layouts.LayoutDefinition layoutDefinition, AutoArgs args, PageType pagetype, Item standardValues)
        {
            try
            {
                int phId = 1;
                AddRendering(layoutDefinition, args.GetRenderingId(pagetype), Settings.GetSetting("SiteKit.Layout.MainPlaceholder", "headless-main"), phId);

                if (pagetype.Layout != null)
                {
                    string basePlaceholder = "/" + Settings.GetSetting("SiteKit.Layout.MainPlaceholder", "headless-main");
                    string parentComponentName = pagetype.Name;
                    
                    phId = ProcessLayoutComponents(layoutDefinition, args, pagetype.Layout, basePlaceholder, parentComponentName, phId);
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message, ex, this);
            }
        }

        private int ProcessLayoutComponents(Sitecore.Layouts.LayoutDefinition layoutDefinition, AutoArgs args, List<LayoutComponent> components, string basePlaceholder, string parentComponentName, int phId)
        {
            foreach (var component in components)
            {
                // Find the component definition in composition
                var compositionComponent = args.CompositionConfig.Composition.Components.Where(c => c.Key == component.Component).FirstOrDefault();
                
                if (compositionComponent.Value != null)
                {
                    // Get all instances of this component type at this level to determine the index
                    var componentList = components.Where(c => c.Component == component.Component).ToList();
                    var componentIndex = componentList.IndexOf(component);

                    // Get the placeholder keys for this component (e.g., "left-column", "right-column" for Two Columns)
                    var placeholderKeys = compositionComponent.Value.Keys.ToList();
                    
                    // Use modulo to cycle through available placeholders if there are more instances than placeholders
                    string compositionKey = placeholderKeys[componentIndex % placeholderKeys.Count];

                    // Build placeholder for this component
                    string placeholder = (basePlaceholder + "/" + parentComponentName.Replace(" ", "-") + "-" + compositionKey + "-" + phId).ToLowerInvariant();
                    
                    // Get component definition and add rendering
                    var componentDefinition = args.ComponentConfig.Components.Where(c => c.Name == component.Component).FirstOrDefault();
                    phId++;
                    AddRendering(layoutDefinition, args.GetRenderingId(componentDefinition), placeholder, phId);

                    // Recursively process children if they exist
                    if (component.Children != null && component.Children.Count > 0)
                    {
                        phId = ProcessLayoutComponents(layoutDefinition, args, component.Children, placeholder, component.Component, phId);
                    }
                }
            }

            return phId;
        }

        public void AddRendering(Sitecore.Layouts.LayoutDefinition layoutDefinition, ID renderingId, string placeholder, int phId)
        {


            var renderingDefinition = new Sitecore.Layouts.RenderingDefinition
            {
                ItemID = renderingId.ToString(),
                Placeholder = placeholder,
                Parameters = "DynamicPlaceholderId=" + phId
            };

            foreach (DeviceDefinition device in layoutDefinition.Devices)
            {
                device.AddRendering(renderingDefinition);
            }


        }
    }
}