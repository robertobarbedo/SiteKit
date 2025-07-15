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
using Sitecore.Services.Core.ComponentModel;

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
                var compositionPageTypeKey = args.CompositionConfig.Composition.Pages.Keys.Where(c => c == pagetype.Name).FirstOrDefault();
                if (compositionPageTypeKey == null)
                    throw new Exception("Composition Page Type Key " + pagetype.Name + " not found");
                var compositionPageType = args.CompositionConfig.Composition.Pages[compositionPageTypeKey];

                if (pagetype.Layout != null)
                {
                    string previousComponentName1 = "";
                    int indexComponent1 = 0;
                    string accumulatedPlaceholder1 = "/" + Settings.GetSetting("SiteKit.Layout.MainPlaceholder", "headless-main");
                    accumulatedPlaceholder1 += "/" + pagetype.Name.Replace(" ", "-") + "-" + compositionPageType.Keys.FirstOrDefault() + "-" + phId;
                    accumulatedPlaceholder1 = accumulatedPlaceholder1.ToLowerInvariant();
                    foreach (var l1 in pagetype.Layout)
                    {
                        //find component
                        var componentDefinition1 = args.ComponentConfig.Components.Where(c => c.Name == l1.Component).FirstOrDefault();
                        if (componentDefinition1 == null)
                            throw new Exception("Component " + l1.Component + " not found");

                        //find compostion
                        var compositionComponentKey1 = args.CompositionConfig.Composition.Components.Keys.Where(c => c == l1.Component).FirstOrDefault();
                        if (compositionComponentKey1 == null)
                            throw new Exception("Composition component key " + l1.Component + " not found");
                        var compositionComponent1 = args.CompositionConfig.Composition.Components[compositionComponentKey1];
                        if (compositionComponent1 == null)
                            throw new Exception("Composition component " + l1.Component + " not found");

                        if (previousComponentName1 != l1.Component)
                        {
                            indexComponent1 = 0;
                            previousComponentName1 = l1.Component;
                            phId++;
                            AddRendering(layoutDefinition, args.GetRenderingId(componentDefinition1), accumulatedPlaceholder1, phId);
                        }
                        else
                        {
                            indexComponent1++;
                        }
                        var keys1 = compositionComponent1.Keys.ToList()[indexComponent1];

                        accumulatedPlaceholder1 += "/" + l1.Component.Replace(" ", "-") + "-" + keys1 + "-" + (phId - indexComponent1).ToString();
                        accumulatedPlaceholder1 = accumulatedPlaceholder1.ToLowerInvariant();
                        
                        if (l1.Children != null && l1.Children.Count > 0)
                        {
                            phId = ProcessLayoutComponentsRecursive(layoutDefinition, args, l1.Children, accumulatedPlaceholder1, l1.Component, phId);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message, ex, this);
            }
        }

        private int ProcessLayoutComponentsRecursive(Sitecore.Layouts.LayoutDefinition layoutDefinition, AutoArgs args, List<LayoutComponent> components, string accumulatedPlaceholder, string parentComponentName, int phId)
        {
            string previousComponentName = "";
            int indexComponent = 0;
            
            foreach (var component in components)
            {
                // Find component
                var componentDefinition = args.ComponentConfig.Components.Where(c => c.Name == component.Component).FirstOrDefault();
                if (componentDefinition == null)
                    throw new Exception("Component " + component.Component + " not found");

                // Find composition
                var compositionComponentKey = args.CompositionConfig.Composition.Components.Keys.Where(c => c == component.Component).FirstOrDefault();
                if (compositionComponentKey == null)
                    throw new Exception("Composition component key " + component.Component + " not found");
                var compositionComponent = args.CompositionConfig.Composition.Components[compositionComponentKey];
                if (compositionComponent == null)
                    throw new Exception("Composition component " + component.Component + " not found");

                if (previousComponentName != component.Component)
                {
                    indexComponent = 0;
                    previousComponentName = component.Component;
                    phId++;
                    AddRendering(layoutDefinition, args.GetRenderingId(componentDefinition), accumulatedPlaceholder, phId);
                }
                else
                {
                    indexComponent++;
                }
                
                var keys = compositionComponent.Keys.ToList()[indexComponent];
                string childPlaceholder = accumulatedPlaceholder + "/" + component.Component.Replace(" ", "-") + "-" + keys + "-" + (phId - indexComponent).ToString();
                childPlaceholder = childPlaceholder.ToLowerInvariant();
                
                // Recursively process children if they exist
                if (component.Children != null && component.Children.Count > 0)
                {
                    phId = ProcessLayoutComponentsRecursive(layoutDefinition, args, component.Children, childPlaceholder, component.Component, phId);
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