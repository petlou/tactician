using Tactician.Combat.Domain.Common;
using Tactician.Combat.Domain.Exceptions;
using Tactician.Combat.Domain.ValueObjects;

namespace Tactician.Combat.Domain.Entities;

public sealed class Npc : Entity
{
    public string Type { get; private set; }
    public int ChalengeRating { get; private set; }
    public HitPoints HitPoints { get; private set; }

    private Npc(Guid id, string type, int challengeRating, HitPoints hitPoints) : base(id)
    {
        Type = type;
        ChalengeRating = challengeRating;
        HitPoints = hitPoints;
    }

    public static Npc Create(string type, int challengeRating, HitPoints hitPoints)
    {
        if (string.IsNullOrWhiteSpace(type))
        {
            throw new DomainException("Type cannot be null or whitespace.");
        }
        if (challengeRating < 0)
        {
            throw new DomainException("Challenge rating cannot be negative.");
        }

        return new Npc(Guid.NewGuid(), type, challengeRating, hitPoints);
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
