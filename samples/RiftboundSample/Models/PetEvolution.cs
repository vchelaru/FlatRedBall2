namespace RiftboundSample.Models;

public class PetEvolution
{
    public string PetId { get; set; } = "";
    public string EvolvedName { get; set; } = "";
    public string EvolvedAbilityBasic { get; set; } = "";
    public string EvolvedAbilityAdvanced { get; set; } = "";
    public string EvolvedAbilityUltimate { get; set; } = "";

    /// <summary>Multiplier for pet's combat effectiveness after evolving.</summary>
    public float StatBoost { get; set; } = 1.5f;
}
