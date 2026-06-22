namespace Awake.Domain.Exceptions;

public class ForbiddenException(string message = "Недостаточно прав.")
    : DomainException(message);
