using Microsoft.EntityFrameworkCore.Storage;
using SmartRentalPlatform.Application.Common;
using System;
using System.Collections.Generic;
using System.Text;

namespace SmartRentalPlatform.Infrastructure.Persistence
{
    public class AppDbContextTransaction : IAppDbContextTransaction
    {
        private readonly IDbContextTransaction transaction;

        public AppDbContextTransaction(IDbContextTransaction transaction)
        {
            this.transaction = transaction;
        }
        public Task CommitAsync(CancellationToken cancellationToken = default)
        {
            return transaction.CommitAsync(cancellationToken);
        }

        public ValueTask DisposeAsync()
        {
            return transaction.DisposeAsync();
        }

        public Task RollbackAsync(CancellationToken cancellationToken = default)
        {
            return transaction.RollbackAsync(cancellationToken);
        }
    }
}
