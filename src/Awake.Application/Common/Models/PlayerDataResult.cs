using Awake.Domain.ValueObjects;

namespace Awake.Application.Common.Models;

public record PlayerDataResult(string Nickname, PlayerProfile? Profile);
