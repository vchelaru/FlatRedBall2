namespace RiftboundSample.Models;

public class PetTierAbilities
{
    public List<string> Basic { get; set; } = [];
    public List<string> Advanced { get; set; } = [];
    public List<string> Ultimate { get; set; } = [];
}

public class PetData
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string OwnerCharacterId { get; set; } = "";

    // Care stats (0-100)
    public float Satiety { get; set; } = 100f;
    public float Training { get; set; } = 50f;
    public float Bond { get; set; } = 50f;

    // Decay rates per real-time second (for game balance, applied per game tick)
    public float SatietyDecayRate { get; set; } = 0.1f;
    public float TrainingDecayRate { get; set; } = 0.05f;
    public float BondDecayRate { get; set; } = 0.02f;

    public PetTierAbilities Abilities { get; set; } = new();
}
