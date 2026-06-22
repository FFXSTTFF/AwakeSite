using Awake.Domain.Entities;

namespace Awake.Application.Common.Interfaces;

public interface IDiscordNotifier
{
    Task NotifyNewTicketAsync(Ticket ticket, CancellationToken ct = default);
    Task NotifyTicketDecisionAsync(Ticket ticket, CancellationToken ct = default);
}
