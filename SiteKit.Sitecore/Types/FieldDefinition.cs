﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SiteKit.Types
{
    public class FieldDefinition
    {
        public string Name { get; set; }
        public string Type { get; set; }
        public string Source { get; set; }
        public string Default { get; set; }
        public string Section { get; set; }
    }
}
