namespace Tactician.Combat.Domain.Common;

public abstract class Entity
{
    public Guid Id { get; protected set; }

    protected Entity(Guid id) => Id = id;

    public override bool Equals(object? obj)
        => obj is Entity entity && entity.GetType() == GetType() && entity.Id == Id;

    public override int GetHashCode() => Id.GetHashCode();
}
