using System;
using System.Collections.Generic;
using System.Text;

namespace SmartRentalPlatform.Contracts.LegalDocuments.Responses;

    public class RoomingHouseLegalDocumentResponse
    {
        public Guid RoomingHouseId { get; set; }
        public Guid? FrontMediaAssetId { get; set; }
        public Guid? BackMediaAssetId { get; set; }
        public Guid? ExtraMediaAssetId { get; set; }
        public string DocumentType { get; set; } = string.Empty;
        public string FrontImageObjectKey { get; set; } = string.Empty;
        public string BackImageObjectKey { get; set; } = string.Empty;
        public string? ExtraImageObjectKey { get; set; }
        public string FrontImageUrl { get; set; } = string.Empty;
        public string BackImageUrl { get; set; } = string.Empty;
        public string? ExtraImageUrl { get; set; }
        public string DocumentNumberMasked { get; set; } = string.Empty;
        public DateTimeOffset UploadedAt { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
    }
