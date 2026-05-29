using System;
using System.Collections.Generic;
using System.Text;

namespace SmartRentalPlatform.Contracts.PropertyImages.Requests;

    public class UpdatePropertyImageItemRequest
    {
        public Guid? Id { get; set; }
        public string ObjectKey { get; set; } = string.Empty;
        public string? Caption { get; set; }
        public bool IsCover { get; set; }
        public int SortOrder { get; set; }
    }