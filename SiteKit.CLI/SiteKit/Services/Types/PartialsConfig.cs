using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using YamlDotNet.Serialization;

namespace SiteKit.Types
{
    public class PartialsConfig
    {
        [YamlMember(Alias = "partials")]
        public List<Partial> Partials { get; set; }
    }

    public class Partial
    {
        public string Name { get; set; }
        public List<LayoutComponent> Layout { get; set; }
        public string? Description { get; set; }
    }
}
