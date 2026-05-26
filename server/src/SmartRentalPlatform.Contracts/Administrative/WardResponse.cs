using System;
using System.Collections.Generic;
using System.Text;

namespace SmartRentalPlatform.Contracts.Administrative
{
    public class WardResponse
    {
        public string Code { get; set; } = string.Empty;
        public string ProvinceCode { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
    }
}
