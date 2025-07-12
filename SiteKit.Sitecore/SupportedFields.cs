using Sitecore.Data;
using System.Collections.Generic;

namespace SiteKit
{
    public class SupportedFields
    {
        public ID Get(string name)
        {
            var supportedFields = new Dictionary<string, string>
            {
                // Simple Types
                { "Single-Line Text", "Single Line Text" },
                { "Date", "Date Picker" },
                { "DateTime", "Date and Time Picker" },
                { "Checkbox", "Checkbox" },
                { "File", "Custom Dialog" },
                { "Image", "Custom Dialog" },
                { "Integer", "Single Line Text" },
                { "Multi-Line Text", "Multi Line Text" },
                { "Number", "Single Line Text" },
                { "Rich Text", "Rich Text Editor" },

                // List Types
                { "Checklist", "Checkbox List" },
                { "Droplist", "Dropdown" },
                { "Multilist", "Checkbox List" },
                { "Treelist", "Tags Multi List" },
                { "Taglist", "Tags Multi List" },
                { "Multiroot Treelist", "Tags Multi List" },

                // Link Types
                { "Droplink", "Dropdown" },
                { "General Link", "Custom Link Interface" }
            };
            return new ID(supportedFields[name]);

        }
    }
}
