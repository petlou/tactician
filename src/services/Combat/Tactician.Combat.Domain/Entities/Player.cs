using Tactician.Combat.Domain.Common;
using Tactician.Combat.Domain.Exceptions;
using Tactician.Combat.Domain.ValueObjects;

namespace Tactician.Combat.Domain.Entities;

public sealed class Player : Entity
{
    public string Name { get; private set; }
    public HitPoints HitPoints { get; private set; }
    public int Level { get; private set; }
    public int ArmorClass { get; private set; }

    private Player(Guid id, string name, HitPoints hitPoints, int level, int armorClass) : base(id)
    {
        Name = name;
        HitPoints = hitPoints;
        Level = level;
        ArmorClass = armorClass;
    }

    public static Player Create(string name, int currentHitPoints, int maxHitPoints, int level, int armorClass)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Player name cannot be null or empty.");
        }

        if (level < 1)
        {
            throw new DomainException("Player level must be a positive integer.");
        }

        if (armorClass <= 0)
        {
            throw new DomainException("Player armor class must be a positive integer.");
        }

        var hitPoints = HitPoints.Create(currentHitPoints, maxHitPoints);

        return new Player(Guid.NewGuid(), name, hitPoints, level, armorClass);
    }

    public void ApplyDamage(int amount)
    {
        HitPoints = HitPoints.ApplyDamage(amount);
    }

    public int ApplyHealing(int amount)
    {
        var healingResult = HitPoints.ApplyHealing(amount);
        HitPoints = healingResult.NewHitPoints;
        return healingResult.OverHealAmount;
    }
}
