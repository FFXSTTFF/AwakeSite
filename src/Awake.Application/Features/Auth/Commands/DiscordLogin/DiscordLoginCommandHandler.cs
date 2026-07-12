using Awake.Application.Common.Interfaces;
using Awake.Application.Common.Interfaces.Repositories;
using Awake.Application.Common.Models;
using Awake.Application.Features.Auth.Commands.Login;
using Awake.Domain.Entities;
using Awake.Domain.Enums;
using MediatR;

namespace Awake.Application.Features.Auth.Commands.DiscordLogin;

public class DiscordLoginCommandHandler(
    IUserRepository userRepository,
    ITicketRepository ticketRepository,
    ITokenService tokenService
) : IRequestHandler<DiscordLoginCommand, Result<LoginResponse>>
{
    public async Task<Result<LoginResponse>> Handle(
        DiscordLoginCommand request, CancellationToken cancellationToken)
    {
        var info = request.DiscordUser;

        var user = await userRepository.GetByDiscordUserIdAsync(info.Id, cancellationToken);
        var isNew = user is null;

        if (user is null)
        {
            var baseUsername = info.GlobalName ?? info.Username;
            var username = baseUsername;

            // global_name у Discord не уникален — дизамбигуируем последними 4 символами id
            if (await userRepository.ExistsByUsernameAsync(username, cancellationToken))
            {
                username = $"{baseUsername}_{info.Id[^4..]}";

                // Крайне маловероятно, но и это может совпасть — берём id целиком (гарантированно уникален)
                if (await userRepository.ExistsByUsernameAsync(username, cancellationToken))
                    username = $"{baseUsername}_{info.Id}";
            }

            user = new User
            {
                Username = username,
                Rank = UserRank.Guest,
                DiscordUserId = info.Id,
            };
        }

        // Обновляем Discord-инфо при каждом входе (ник/аватар могли смениться)
        user.DiscordUsername = info.Username;
        user.DiscordAvatarUrl = info.AvatarUrl;

        // Связывание висячих заявок: только AuthorId == null → идемпотентно
        var hangingTickets = await ticketRepository
            .GetUnlinkedByDiscordUserIdAsync(info.Id, cancellationToken);

        if (isNew)
            await userRepository.AddAsync(user, cancellationToken);

        foreach (var ticket in hangingTickets)
        {
            ticket.AuthorId = user.Id;
            await ticketRepository.UpdateAsync(ticket, cancellationToken);
        }

        // GameNickname — из самой свежей заявки (репозиторий сортирует по убыванию CreatedAt)
        var nicknameUpdated = false;
        if (hangingTickets.Count > 0)
        {
            user.GameNickname = hangingTickets[0].GameNickname;
            nicknameUpdated = true;
        }

        if (!isNew)
            await userRepository.UpdateAsync(user, cancellationToken);
        else if (nicknameUpdated)
            await userRepository.UpdateAsync(user, cancellationToken);

        var accessToken = tokenService.GenerateAccessToken(user);
        return Result<LoginResponse>.Success(
            new LoginResponse(accessToken, user.Username, user.Rank, user.Id.ToString()));
    }
}
