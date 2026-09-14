using Tactician.Combat.Domain.Common;
using Tactician.Combat.Domain.Exceptions;

namespace Tactician.Combat.Domain.ValueObjects;

public sealed class HitPoints : ValueObject
{
    public int Current { get; }
    public int Max { get; }

    private HitPoints(int current, int max)
    {
        Current = current;
        Max = max;
    }

    public static HitPoints Create(int current, int max)
    {
        if (max <= 0)
            throw new DomainException("Max hit points must be greater than zero.");

        if (current < 0)
            throw new DomainException("Current hit points cannot be negative.");

        if (current > max)
            throw new DomainException("Current hit points cannot exceed max hit points.");

        return new HitPoints(current, max);
    }

    public HitPoints ApplyDamage(int amount)
    {
        if (amount < 0)
            throw new DomainException("Damage amount cannot be negative.");
        var newCurrent = Math.Max(Current - amount, 0);
        return new HitPoints(newCurrent, Max);
    }

    public HealingResult ApplyHealing(int amount)
    {
        if (amount < 0)
            throw new DomainException("Healing amount cannot be negative.");

        var rawNewCurrent = Current + amount;
        var overHealAmount = Math.Max(rawNewCurrent - Max, 0);
        var clampedCurrent = Math.Min(rawNewCurrent, Max);

        return new HealingResult(new HitPoints(clampedCurrent, Max), overHealAmount);
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Current;
        yield return Max;
    }
}

public sealed record HealingResult(HitPoints NewHitPoints, int OverHealAmount);
