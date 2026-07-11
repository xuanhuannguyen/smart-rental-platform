using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.Domain.Entities.RentalContracts
{
    public class ContractFile
    {
        public Guid Id { get; set; }

        public Guid RentalContractId { get; set; }

        public Guid? RentalContractAppendixId { get; set; }

        public string StorageObjectKey { get; set; } = string.Empty;

        public ContractFilePurpose Purpose { get; set; } = ContractFilePurpose.Preview;

        public string ContentType { get; set; } = "application/pdf";

        public string? FileUrl { get; set; }

        public string? Sha256Hash { get; set; }

        public bool IsLegallySigned { get; set; }

        public Guid? ContractSigningEnvelopeId { get; set; }

        public DateTimeOffset CreatedAt { get; set; }

        public RentalContract RentalContract { get; set; } = null!;

        public ContractAppendix? RentalContractAppendix { get; set; }

        public ContractSigningEnvelope? ContractSigningEnvelope { get; set; }
    }
}
