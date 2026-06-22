using Awake.Domain.Enums;

namespace Awake.Application.Common.Interfaces;

public interface ICurrentUserService
{
    Guid UserId { get; }
    UserRank Rank { get; }
    bool IsAuthenticated { get; }
}
