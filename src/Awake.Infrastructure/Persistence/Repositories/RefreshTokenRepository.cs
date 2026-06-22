using Awake.Application.Common.Interfaces.Repositories;
using Awake.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Awake.Infrastructure.Persistence.Repositories;

public class RefreshTokenRepository(AppDbContext context) : IRefreshTokenRepository
{
    public async Task<RefreshToken?> GetByTokenAsync(string token, CancellationToken ct = default)
        => await context.RefreshTokens
            .FirstOrDefaultAsync(r => r.Token == token && !r.IsRevoked, ct);

    public async Task AddAsync(RefreshToken refreshToken, CancellationToken ct = default)
    {
        await context.RefreshTokens.AddAsync(refreshToken, ct);
        await context.SaveChangesAsync(ct);
    }

    public async Task RevokeAsync(string token, CancellationToken ct = default)
    {
        var stored = await context.RefreshTokens.FirstOrDefaultAsync(r => r.Token == token, ct);
        if (stored is not null)
        {
            stored.IsRevoked = true;
            await context.SaveChangesAsync(ct);
        }
    }

    public async Task RevokeAndAddAsync(string oldToken, RefreshToken newToken, CancellationToken ct = default)
    {
        var stored = await context.RefreshTokens.FirstOrDefaultAsync(r => r.Token == oldToken, ct);
        if (stored is not null)
            stored.IsRevoked = true;

        await context.RefreshTokens.AddAsync(newToken, ct);
        await context.SaveChangesAsync(ct);
    }
}
