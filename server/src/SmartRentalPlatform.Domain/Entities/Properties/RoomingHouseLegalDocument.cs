using SmartRentalPlatform.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartRentalPlatform.Domain.Entities.Properties
{
    public class RoomingHouseLegalDocument
    {
        public Guid RoomingHouseId { get; set; }
        public LegalDocumentType DocumentType { get; set; } = LegalDocumentType.LAND_USE_CERTIFICATE;
        public string FrontImageObjectKey { get; set; } = string.Empty;
        public string BackImageObjectKey { get; set; } = string.Empty;
        public string? ExtraImageObjectKey { get; set; }
        public string DocumentNumberMasked { get; set; } = string.Empty;
        public string DocumentNumberHash { get; set; } = string.Empty;
        public DateTimeOffset UploadedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }

        
        public RoomingHouse RoomingHouse { get; set; } = null!;
    }
}
