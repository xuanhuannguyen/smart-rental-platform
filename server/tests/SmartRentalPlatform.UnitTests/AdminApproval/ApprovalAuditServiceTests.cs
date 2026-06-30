using SmartRentalPlatform.Application.AdminApproval;
using SmartRentalPlatform.UnitTests.Common;

namespace SmartRentalPlatform.UnitTests.AdminApproval;

public class ApprovalAuditServiceTests : IDisposable
{
    private readonly TestDatabaseFixture _fixture = new();

    [Fact]
    public async Task LogAsync_PersistsApprovalAuditLog()
    {
        var adminId = Guid.NewGuid();
        var entityId = Guid.NewGuid();
        var service = new ApprovalAuditService(_fixture.Context);

        await service.LogAsync(
            adminId,
            "Kyc",
            entityId,
            "Approved",
            "Looks good",
            "{\"score\":95}");

        var log = Assert.Single(_fixture.Context.ApprovalAuditLogs);
        Assert.Equal(adminId, log.AdminId);
        Assert.Equal("Kyc", log.ApprovalType);
        Assert.Equal(entityId, log.EntityId);
        Assert.Equal("Approved", log.Action);
        Assert.Equal("Looks good", log.Reason);
        Assert.Equal("{\"score\":95}", log.AdditionalInfo);
        Assert.True(log.CreatedAt > DateTimeOffset.UtcNow.AddMinutes(-1));
    }

    public void Dispose()
    {
        _fixture.Dispose();
    }
}
