namespace Tactician.Combat.Domain.Common;

public abstract class ValueObject
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public override bool Equals(object? obj)
     => obj is ValueObject valueObject
        && GetType() == valueObject.GetType()
        && GetEqualityComponents().SequenceEqual(valueObject.GetEqualityComponents());

    public override int GetHashCode()
        => GetEqualityComponents().Aggregate(0, (hash, component) => HashCode.Combine(hash, component));
}
