using System;
using System.Collections.Generic;
using System.Text;

namespace SmartRentalPlatform.Contracts.LegalDocuments
{
    public class UpdateRoomingHouseLegalDocumentRequest
    {
        public string DocumentType { get; set; } = string.Empty;
        public string FrontImageObjectKey { get; set; } = string.Empty;
        public string BackImageObjectKey { get; set; } = string.Empty;
        public string? ExtraImageObjectKey { get; set; }
        public string DocumentNumber { get; set; } = string.Empty;
    }
}
