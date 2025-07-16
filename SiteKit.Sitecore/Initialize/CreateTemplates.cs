using System;
using System.Web;
using Sitecore;
using Sitecore.Data;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Pipelines;

namespace SiteKit.Initialize
{
    public class CreateTemplates
    {
        private static readonly Database Database = Sitecore.Configuration.Factory.GetDatabase("master");
        
        public void Process(PipelineArgs args)
        {
            try
            {
                CreateYamlConfigTemplate();
                CreateYamlFolderTemplate();
                CreateSiteKitItem();
            }
            catch (Exception ex)
            {
                Log.Error($"Error in CreateTemplates processor: {ex.Message}", ex, this);
            }
        }

        private void CreateYamlConfigTemplate()
        {
            var templateId = new ID("{ED55F363-5614-48C4-8F90-573535CBC6E3}");
            var template = Database.GetItem(templateId);
            
            if (template == null)
            {
                Log.Info("Creating YamlConfig template", this);
                
                var userDefinedFolder = Database.GetItem("/sitecore/templates/User Defined");
                if (userDefinedFolder != null)
                {
                    // Create template
                    template = userDefinedFolder.Add("YamlConfig", new TemplateID(Sitecore.TemplateIDs.Template), templateId);
                    
                    // Create Data section
                    var dataSectionId = ID.NewID;
                    var dataSection = template.Add("Data", new TemplateID(Sitecore.TemplateIDs.TemplateSection), dataSectionId);
                    
                    // Create YAML field
                    var yamlFieldId = ID.NewID;
                    var yamlField = dataSection.Add("YAML", new TemplateID(Sitecore.TemplateIDs.TemplateField), yamlFieldId);
                    
                    yamlField.Editing.BeginEdit();
                    yamlField["Type"] = "Multi-Line Text";
                    yamlField.Editing.EndEdit();
                    
                    Log.Info("YamlConfig template created successfully", this);
                }
                else
                {
                    Log.Error("User Defined templates folder not found", this);
                }
            }
            else
            {
                Log.Info("YamlConfig template already exists", this);
            }
        }

        private void CreateYamlFolderTemplate()
        {
            var templateId = new ID("{34D7B2A4-C599-4E5A-B7BF-8AEBD7D18B15}");
            var template = Database.GetItem(templateId);
            
            if (template == null)
            {
                Log.Info("Creating YamlFolder template", this);
                
                var userDefinedFolder = Database.GetItem("/sitecore/templates/User Defined");
                if (userDefinedFolder != null)
                {
                    // Create template
                    template = userDefinedFolder.Add("YamlFolder", new TemplateID(Sitecore.TemplateIDs.Template), templateId);
                    
                    // Create Data section
                    var dataSectionId = ID.NewID;
                    var dataSection = template.Add("Data", new TemplateID(Sitecore.TemplateIDs.TemplateSection), dataSectionId);
                    
                    // Create Log field
                    var logFieldId = ID.NewID;
                    var logField = dataSection.Add("Log", new TemplateID(Sitecore.TemplateIDs.TemplateField), logFieldId);
                    
                    logField.Editing.BeginEdit();
                    logField["Type"] = "Multi-Line Text";
                    logField.Editing.EndEdit();
                    
                    Log.Info("YamlFolder template created successfully", this);
                }
                else
                {
                    Log.Error("User Defined templates folder not found", this);
                }
            }
            else
            {
                Log.Info("YamlFolder template already exists", this);
            }
        }

        private void CreateSiteKitItem()
        {
            var siteKitItem = Database.GetItem("/sitecore/system/Modules/SiteKit");
            
            if (siteKitItem == null)
            {
                Log.Info("Creating SiteKit item", this);
                
                var modulesFolder = Database.GetItem("/sitecore/system/Modules");
                if (modulesFolder != null)
                {
                    var yamlFolderTemplateId = new ID("{34D7B2A4-C599-4E5A-B7BF-8AEBD7D18B15}");
                    
                    // Create SiteKit item using YamlFolder template
                    siteKitItem = modulesFolder.Add("SiteKit", new TemplateID(yamlFolderTemplateId));
                    
                    Log.Info("SiteKit item created successfully", this);
                }
                else
                {
                    Log.Error("Modules folder not found", this);
                }
            }
            else
            {
                Log.Info("SiteKit item already exists", this);
            }
        }
    }
}
