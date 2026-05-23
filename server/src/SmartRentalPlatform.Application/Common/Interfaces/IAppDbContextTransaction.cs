namespace SmartRentalPlatform.Application.Common.Interfaces;

public interface IAppDbContextTransaction : IAsyncDisposable
{
    Task CommitAsync(CancellationToken cancellationToken = default);
    Task RollbackAsync(CancellationToken cancellationToken = default);
}
