using System;
using System.Collections.Generic;
using System.Text;

namespace SmartRentalPlatform.Contracts.PropertyImages.Requests;

    public class UpdatePropertyImagesRequest
    {
        public List<UpdatePropertyImageItemRequest> Images { get; set; } = new List<UpdatePropertyImageItemRequest>();
    }