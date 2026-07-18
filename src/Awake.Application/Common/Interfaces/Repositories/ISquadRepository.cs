using Awake.Domain.Entities;

namespace Awake.Application.Common.Interfaces.Repositories;

public interface ISquadRepository
{
    Task<Squad?> GetByIdAsync(Guid id, CancellationToken ct = default);
    Task<Squad?> GetByNumberAsync(int number, CancellationToken ct = default);
    Task<IReadOnlyList<Squad>> GetAllWithMembersAsync(CancellationToken ct = default);
    Task<int> GetMemberCountAsync(Guid squadId, CancellationToken ct = default);
    Task AddMemberAsync(SquadMember member, CancellationToken ct = default);
    Task RemoveMemberAsync(Guid squadId, Guid userId, CancellationToken ct = default);
    Task MoveMemberAsync(Guid? fromSquadId, SquadMember member, CancellationToken ct = default);
    Task UpdateMemberAsync(SquadMember member, CancellationToken ct = default);
    Task UpdateAsync(Squad squad, CancellationToken ct = default);
    Task<Squad?> GetByIdWithMembersAsync(Guid id, CancellationToken ct = default);
    Task<bool> IsUserInAnySquadAsync(Guid userId, CancellationToken ct = default);
    Task<SquadMember?> GetMembershipByUserIdAsync(Guid userId, CancellationToken ct = default);
}
