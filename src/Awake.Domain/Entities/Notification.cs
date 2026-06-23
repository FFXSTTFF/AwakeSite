namespace Awake.Domain.Entities;

public class Notification
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public Guid UserId { get; init; }
    public string Title { get; init; } = string.Empty;
    public string Body { get; init; } = string.Empty;
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;
}
