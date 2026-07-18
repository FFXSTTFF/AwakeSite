using Awake.Application.Common.Interfaces.Repositories;
using Awake.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Awake.Infrastructure.Persistence.Repositories;

public class SquadRepository(AppDbContext context) : ISquadRepository
{
    public async Task<Squad?> GetByIdAsync(Guid id, CancellationToken ct = default)
        => await context.Squads.FindAsync([id], ct);

    public async Task<Squad?> GetByNumberAsync(int number, CancellationToken ct = default)
        => await context.Squads.FirstOrDefaultAsync(s => s.Number == number, ct);

    public async Task<IReadOnlyList<Squad>> GetAllWithMembersAsync(CancellationToken ct = default)
        => await context.Squads
            .Include(s => s.Members)
            .ThenInclude(m => m.User)
            .ToListAsync(ct);

    public async Task<int> GetMemberCountAsync(Guid squadId, CancellationToken ct = default)
        => await context.SquadMembers.CountAsync(m => m.SquadId == squadId, ct);

    public async Task AddMemberAsync(SquadMember member, CancellationToken ct = default)
    {
        await context.SquadMembers.AddAsync(member, ct);
        await context.SaveChangesAsync(ct);
    }

    public async Task RemoveMemberAsync(Guid squadId, Guid userId, CancellationToken ct = default)
    {
        var member = await context.SquadMembers
            .FirstOrDefaultAsync(m => m.SquadId == squadId && m.UserId == userId, ct);

        if (member is not null)
        {
            context.SquadMembers.Remove(member);
            await context.SaveChangesAsync(ct);
        }
    }

    public async Task MoveMemberAsync(Guid? fromSquadId, SquadMember member, CancellationToken ct = default)
    {
        if (fromSquadId is not null)
        {
            var existing = await context.SquadMembers
                .FirstOrDefaultAsync(m => m.SquadId == fromSquadId && m.UserId == member.UserId, ct);

            if (existing is not null)
                context.SquadMembers.Remove(existing);
        }

        await context.SquadMembers.AddAsync(member, ct);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateMemberAsync(SquadMember member, CancellationToken ct = default)
    {
        context.SquadMembers.Update(member);
        await context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(Squad squad, CancellationToken ct = default)
    {
        context.Squads.Update(squad);
        await context.SaveChangesAsync(ct);
    }

    public async Task<Squad?> GetByIdWithMembersAsync(Guid id, CancellationToken ct = default)
        => await context.Squads
            .Include(s => s.Members)
            .ThenInclude(m => m.User)
            .FirstOrDefaultAsync(s => s.Id == id, ct);

    public async Task<bool> IsUserInAnySquadAsync(Guid userId, CancellationToken ct = default)
        => await context.SquadMembers.AnyAsync(m => m.UserId == userId, ct);

    public async Task<SquadMember?> GetMembershipByUserIdAsync(Guid userId, CancellationToken ct = default)
        => await context.SquadMembers
            .Include(m => m.Squad)
            .FirstOrDefaultAsync(m => m.UserId == userId, ct);
}
