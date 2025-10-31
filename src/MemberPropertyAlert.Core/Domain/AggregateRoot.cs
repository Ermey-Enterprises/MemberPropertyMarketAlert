using System.Collections.Generic;

namespace MemberPropertyAlert.Core.Domain;

public abstract class AggregateRoot : Entity
{
    private readonly List<object> _domainEvents = new();

    protected AggregateRoot(string id) : base(id)
    {
    }

    protected AggregateRoot(string id, DateTimeOffset? createdAtUtc, DateTimeOffset? updatedAtUtc) : base(id, createdAtUtc, updatedAtUtc)
    {
    }

    public IReadOnlyCollection<object> DomainEvents => _domainEvents.AsReadOnly();

    protected void RaiseDomainEvent(object domainEvent)
    {
        _domainEvents.Add(domainEvent);
    }

    public void ClearDomainEvents() => _domainEvents.Clear();
}
