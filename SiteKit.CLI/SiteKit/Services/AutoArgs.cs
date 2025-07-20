using System.Security.Cryptography;
using System.Text;
using SiteKit.Types;

namespace SiteKit
{
    public class AutoArgs
    {
        public string SiteName { get; private set; }
        public string Salt { get; set; }
        public Dictionary<string, string> Yamls { get; private set; }
        public SiteConfig SiteConfig { get; set; }
        public ComponentConfig ComponentConfig { get; set; }
        public PageTypesConfig PageTypesConfig { get; set; }
        public CompositionConfig CompositionConfig { get; set; }
        public bool IsValid { get; set; }
        public string ValidationMessage { get; set; }
        public  string Directory { get;set; }
        public string AccessToken { get; set; }
        public string Endpoint { get; set; }

        public AutoArgs(string siteName)
        {
            SiteName = siteName;
            Salt = "RB2502kksidoe99";
            SiteConfig = new SiteConfig();
            Yamls = new Dictionary<string, string>();
            IsValid = true;
            ValidationMessage = string.Empty;
        }

        //public ID GetUniqueID(string objecttype, string value)
        //{
        //    var md5Hasher = MD5.Create();
        //    var data = md5Hasher.ComputeHash(Encoding.Default.GetBytes(Salt + objecttype + value));
        //    return new ID(new Guid(data));
        //}
        //
        //public ID GetRenderingId(ComponentDefinition component)
        //{
        //    return GetUniqueID("component_rendering", component.Name);
        //}
        //
        //public ID GetRenderingId(PageType component)
        //{
        //    return GetUniqueID("component_page_container_rendering", component.Name);
        //}

    }
}
