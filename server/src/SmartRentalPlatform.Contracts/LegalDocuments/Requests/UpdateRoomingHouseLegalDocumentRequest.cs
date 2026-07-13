using System;
using System.Collections.Generic;
using System.Text;

namespace SmartRentalPlatform.Contracts.LegalDocuments.Requests;

    public class UpdateRoomingHouseLegalDocumentRequest
    {
        public string DocumentType { get; set; } = string.Empty;
        public Guid? FrontMediaAssetId { get; set; }
        public string FrontImageObjectKey { get; set; } = string.Empty;
        public Guid? BackMediaAssetId { get; set; }
        public string BackImageObjectKey { get; set; } = string.Empty;
        public Guid? ExtraMediaAssetId { get; set; }
        public string? ExtraImageObjectKey { get; set; }
        public string DocumentNumber { get; set; } = string.Empty;
    }
