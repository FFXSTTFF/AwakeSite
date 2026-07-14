using Awake.Application.Common.Models;
using MediatR;

namespace Awake.Application.Features.Squads.Commands.MoveMember;

/// <summary>Переносит игрока в отряд, автоматически убирая из текущего. Идемпотентна.</summary>
public record MoveSquadMemberCommand(Guid SquadId, Guid UserId) : IRequest<Result<bool>>;
