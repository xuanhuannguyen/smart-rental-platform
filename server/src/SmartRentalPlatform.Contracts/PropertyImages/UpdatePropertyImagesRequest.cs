using System;
using System.Collections.Generic;
using System.Text;

namespace SmartRentalPlatform.Contracts.PropertyImages
{
    public class UpdatePropertyImagesRequest
    {
        public List<UpdatePropertyImageItemRequest> Images { get; set; } = new List<UpdatePropertyImageItemRequest>();
    }
}
