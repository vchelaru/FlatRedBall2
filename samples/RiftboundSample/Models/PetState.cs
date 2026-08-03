namespace RiftboundSample.Models;

public enum PetTier { Basic, Advanced, Ultimate }

public class PetState
{
    public string Id { get; set; } = "";
    public string Name { get; set; } = "";
    public string OwnerId { get; set; } = "";
    public float Satiety { get; set; }
    public float Training { get; set; }
    public float Bond { get; set; }
    public float BattleGauge { get; set; }
    public bool IsAlive { get; set; } = true;
    public bool IsDead => !IsAlive;
    public bool IsEvolved { get; set; }
    public string? EvolvedName { get; set; }

    public PetTier CurrentTier => Bond switch
    {
        >= 80 => PetTier.Ultimate,
        >= 40 => PetTier.Advanced,
        _ => PetTier.Basic
    };

    public static PetState FromData(PetData data) => new()
    {
        Id = data.Id,
        Name = data.Name,
        OwnerId = data.OwnerCharacterId,
        Satiety = data.Satiety,
        Training = data.Training,
        Bond = data.Bond,
    };
}
