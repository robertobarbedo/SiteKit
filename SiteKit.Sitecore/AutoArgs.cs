using Sitecore.Configuration;
using Sitecore.Data;
using Sitecore.Diagnostics;
using Sitecore.Pipelines;
using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using SiteKit.Types;

namespace SiteKit
{
    public class AutoArgs : PipelineArgs
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

        public AutoArgs(string siteName)
        {
            SiteName = siteName;
            Salt = Settings.GetSetting("SiteKit.IdGenSalt", "RB2502kksidoe99");
            SiteConfig = new SiteConfig();
            Yamls = new Dictionary<string, string>();
            IsValid = true;
            ValidationMessage = string.Empty;
        }

        public ID GetUniqueID(string objecttype, string value)
        {
            Assert.ArgumentNotNull(value, "value");
            var md5Hasher = MD5.Create();
            var data = md5Hasher.ComputeHash(Encoding.Default.GetBytes(Salt + objecttype + value));
            return new ID(new Guid(data));
        }

        public ID GetRenderingId(ComponentDefinition component)
        {
            return GetUniqueID("component_rendering", component.Name);
        }

        public ID GetRenderingId(PageType component)
        {
            return GetUniqueID("component_page_container_rendering", component.Name);
        }

    }
}
