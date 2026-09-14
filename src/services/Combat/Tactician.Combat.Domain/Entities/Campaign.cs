using Tactician.Combat.Domain.Common;
using Tactician.Combat.Domain.Exceptions;

namespace Tactician.Combat.Domain.Entities;

public sealed class Campaign : Entity
{
    public string Name { get; private set; }
    public string Description { get; private set; } = string.Empty;

    private Campaign(Guid id, string name, string description) : base(id)
    {
        Name = name;
        Description = description;
    }

    public static Campaign Create(string name, string description)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Campaign name cannot be null or empty.");
        }

        return new Campaign(Guid.NewGuid(), name, description);
    }
}
