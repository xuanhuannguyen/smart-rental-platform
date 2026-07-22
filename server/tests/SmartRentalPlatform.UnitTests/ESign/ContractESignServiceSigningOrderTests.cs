using System.Reflection;
using SmartRentalPlatform.Application.Common.Exceptions;
using SmartRentalPlatform.Application.RentalContracts;
using SmartRentalPlatform.Domain.Entities.RentalContracts;
using SmartRentalPlatform.Domain.Enums.RentalContracts;

namespace SmartRentalPlatform.UnitTests.ESign;

public class ContractESignServiceSigningOrderTests
{
    [Fact]
    public void LegacyZeroOrders_LandlordIsStillTheNextSigner()
    {
        var landlordUserId = Guid.NewGuid();
        var tenantUserId = Guid.NewGuid();
        var landlordSignature = Signature(landlordUserId, ContractSignerRole.Landlord);
        var envelope = new ContractSigningEnvelope
        {
            Signatures =
            [
                Signature(tenantUserId, ContractSignerRole.Tenant),
                landlordSignature
            ]
        };

        var result = InvokeGetNextSignatureForUser(envelope, landlordUserId);

        Assert.Same(landlordSignature, result);
    }

    [Fact]
    public void LegacyZeroOrders_TenantMustWaitForLandlord()
    {
        var landlordUserId = Guid.NewGuid();
        var tenantUserId = Guid.NewGuid();
        var envelope = new ContractSigningEnvelope
        {
            Signatures =
            [
                Signature(tenantUserId, ContractSignerRole.Tenant),
                Signature(landlordUserId, ContractSignerRole.Landlord)
            ]
        };

        var exception = Assert.Throws<TargetInvocationException>(
            () => InvokeGetNextSignatureForUser(envelope, tenantUserId));

        Assert.IsType<ConflictException>(exception.InnerException);
    }

    [Fact]
    public void MissingProviderEvidence_ReturnsBusinessConflict()
    {
        var signature = Signature(Guid.NewGuid(), ContractSignerRole.Landlord);
        var method = typeof(ContractESignService).GetMethod(
            "ReadEvidence",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Evidence reader was not found.");

        var exception = Assert.Throws<TargetInvocationException>(
            () => method.Invoke(null, [signature]));

        Assert.IsType<ConflictException>(exception.InnerException);
    }

    private static ContractSignature InvokeGetNextSignatureForUser(
        ContractSigningEnvelope envelope,
        Guid userId)
    {
        var method = typeof(ContractESignService).GetMethod(
            "GetNextSignatureForUser",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("Signing order guard was not found.");

        return (ContractSignature)method.Invoke(null, [envelope, userId])!;
    }

    private static ContractSignature Signature(Guid userId, ContractSignerRole role) => new()
    {
        SignerUserId = userId,
        SignerRole = role,
        SigningOrder = 0,
        Status = (ContractSignatureStatus)0
    };
}
