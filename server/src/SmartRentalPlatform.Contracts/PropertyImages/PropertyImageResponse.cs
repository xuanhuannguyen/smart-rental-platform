using System;
using System.Collections.Generic;
using System.Text;

namespace SmartRentalPlatform.Contracts.PropertyImages
{
    public class PropertyImageResponse
    {
        public Guid Id { get; set; }
        public string ObjectKey { get; set; } = string.Empty;
        public string ImageUrl { get; set; } = string.Empty;
        public string? Caption { get; set; }
        public bool IsCover { get; set; }
        public int SortOrder { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
