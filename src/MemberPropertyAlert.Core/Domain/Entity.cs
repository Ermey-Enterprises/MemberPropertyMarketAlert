using System;

namespace MemberPropertyAlert.Core.Domain;

public abstract class Entity
{
    public string Id { get; protected set; }
    public DateTimeOffset CreatedAtUtc { get; protected set; }
    public DateTimeOffset UpdatedAtUtc { get; protected set; }

    protected Entity(string id, DateTimeOffset? createdAtUtc = null, DateTimeOffset? updatedAtUtc = null)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            throw new ArgumentException("Entity id cannot be null or empty", nameof(id));
        }

        Id = id;
        CreatedAtUtc = createdAtUtc ?? DateTimeOffset.UtcNow;
        UpdatedAtUtc = updatedAtUtc ?? CreatedAtUtc;
    }

    protected Entity()
    {
        Id = string.Empty;
        CreatedAtUtc = DateTimeOffset.UtcNow;
        UpdatedAtUtc = CreatedAtUtc;
    }

    public void Touch() => UpdatedAtUtc = DateTimeOffset.UtcNow;
}
