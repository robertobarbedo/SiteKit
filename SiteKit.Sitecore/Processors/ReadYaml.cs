using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Sitecore.Data.Items;

namespace SiteKit.Processors
{
    public class ReadYaml: AutoBase
    {
        public string ModulePath = "/sitecore/system/Modules/SiteKit/";
        public void Process(AutoArgs args)
        {
            var folder = Database.GetItem(ModulePath + args.SiteName);
            if (folder == null)
            {
                args.IsValid = false;
                args.ValidationMessage = "'" + args.SiteName + "' does not exists in /sitecore/system/Modules/SiteKit";
            }
            foreach (Item item in folder.GetChildren())
            {
                args.Yamls.Add(item.Name, item["YAML"]);
            }
        }
    }
}
