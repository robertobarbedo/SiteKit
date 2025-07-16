using System;
using Sitecore.Data.Items;
using Sitecore.Diagnostics;
using Sitecore.Events;

namespace SiteKit.Handlers
{
    public class YamlFolderSave
    {
        public string TemplateId { get; set; }

        public void OnItemSaved(object sender, EventArgs args)
        {
            try
            {
                //null check
                if (args == null)
                    return;

                // Extract the item from the event Arguments
                Item sourceItem = Event.ExtractParameter(args, 0) as Item;
                if (sourceItem == null)
                    return;

                if (!TemplateId.Equals(sourceItem.TemplateID.ToString(), StringComparison.InvariantCultureIgnoreCase))
                    return;

                if (!sourceItem.Paths.FullPath.ToLowerInvariant().StartsWith("/sitecore/system/modules"))
                    return;

                //only fire when log get empty
                if (sourceItem["Log"] == "deploy" || string.IsNullOrEmpty(sourceItem["Log"]))
                    AutoManager.Run(sourceItem.Name, AutoManagerActions.ValidateAndDeploy);
                else if (sourceItem["Log"] == "validate")
                    AutoManager.Run(sourceItem.Name, AutoManagerActions.Validate);
            }
            catch (Exception ex)
            {
                Log.Error(ex.Message, ex, this);
            }
        }
    }
}
