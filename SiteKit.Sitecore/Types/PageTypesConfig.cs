using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace SiteKit.Types
{
    public class PageTypesConfig
    {
        [YamlMember(Alias = "pagetypes")]
        public List<PageType> PageTypes { get; set; }
    }

    public class PageType
    {
        public string Name { get; set; }
        public List<FieldDefinition> Fields { get; set; }
    }
}
