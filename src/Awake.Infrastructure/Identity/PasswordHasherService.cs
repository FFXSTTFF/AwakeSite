using Awake.Application.Common.Interfaces;
using Awake.Domain.Entities;
using Microsoft.AspNetCore.Identity;

namespace Awake.Infrastructure.Identity;

public class PasswordHasherService : IPasswordHasher
{
    private static readonly PasswordHasher<User> _hasher = new();

    public string Hash(string password)
        => _hasher.HashPassword(null!, password);

    public bool Verify(string hash, string password)
        => _hasher.VerifyHashedPassword(null!, hash, password) != PasswordVerificationResult.Failed;
}
