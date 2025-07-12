using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Data.Items;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SiteKit.Types;

namespace SiteKit.Processors
{
    public class AutoBase
    {
        public AutoBase()
        {
            Database = Sitecore.Configuration.Factory.GetDatabase("master");
        }

        public void SetDefaultFields(Item item)
        {
            item.Editing.BeginEdit();
            item[Sitecore.FieldIDs.Style] = "color:navy;";
            item.Editing.EndEdit();
        }

        public SiteDefinition GetSite(AutoArgs args)
        {
            return args.SiteConfig.Site;
        }

        protected Database Database;
    }
}
