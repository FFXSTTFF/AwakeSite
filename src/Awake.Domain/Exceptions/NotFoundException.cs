namespace Awake.Domain.Exceptions;

public class NotFoundException(string entity, object key)
    : DomainException($"{entity} с идентификатором '{key}' не найден.");
