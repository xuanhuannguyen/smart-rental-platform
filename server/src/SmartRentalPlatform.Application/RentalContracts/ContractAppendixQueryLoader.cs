using Microsoft.EntityFrameworkCore;
using SmartRentalPlatform.Application.Common.Interfaces;
using SmartRentalPlatform.Domain.Entities.RentalContracts;

namespace SmartRentalPlatform.Application.RentalContracts;

public sealed class ContractAppendixQueryLoader
{
    private readonly IAppDbContext context;

    public ContractAppendixQueryLoader(IAppDbContext context)
    {
        this.context = context;
    }

    public IQueryable<RentalContract> Contracts()
    {
        return context.RentalContracts
            .Include(x => x.MainTenantUser)
                .ThenInclude(x => x.UserProfile)
            .Include(x => x.Room)
                .ThenInclude(x => x.RoomingHouse)
                    .ThenInclude(x => x.Landlord)
                        .ThenInclude(x => x.UserProfile)
            .Include(x => x.Room)
                .ThenInclude(x => x.PriceTiers)
            .Include(x => x.Room)
                .ThenInclude(x => x.RoomingHouse)
                    .ThenInclude(x => x.RentalPolicy)
            .Include(x => x.Appendices)
                .ThenInclude(x => x.Changes)
            .Include(x => x.Occupants)
                .ThenInclude(x => x.Documents)
            .Include(x => x.Occupants)
                .ThenInclude(x => x.User)
                    .ThenInclude(x => x!.UserProfile);
    }

    public IQueryable<ContractAppendix> Appendices()
    {
        return context.ContractAppendices
            .Include(x => x.RentalContract)
                .ThenInclude(x => x.MainTenantUser)
                    .ThenInclude(x => x.UserProfile)
            .Include(x => x.RentalContract)
                .ThenInclude(x => x.Room)
                    .ThenInclude(x => x.RoomingHouse)
                        .ThenInclude(x => x.Landlord)
                            .ThenInclude(x => x.UserProfile)
            .Include(x => x.RentalContract)
                .ThenInclude(x => x.Room)
                    .ThenInclude(x => x.PriceTiers)
            .Include(x => x.RentalContract)
                .ThenInclude(x => x.Occupants)
                    .ThenInclude(x => x.Documents)
            .Include(x => x.RentalContract)
                .ThenInclude(x => x.Occupants)
                    .ThenInclude(x => x.User)
                        .ThenInclude(x => x!.UserProfile)
            .Include(x => x.RentalContract)
                .ThenInclude(x => x.Appendices)
                    .ThenInclude(x => x.Changes)
            .Include(x => x.Changes)
            .Include(x => x.Signatures)
            .Include(x => x.Files);
    }
}
