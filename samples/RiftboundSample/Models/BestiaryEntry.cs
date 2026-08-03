namespace RiftboundSample.Models;

public class BestiaryEntry
{
    public string EnemyId { get; set; } = "";
    public string Name { get; set; } = "";
    public int TimesDefeated { get; set; }

    /// <summary>Unlocked after defeating this enemy 3+ times.</summary>
    public bool StatsRevealed => TimesDefeated >= 3;

    /// <summary>Unlocked after defeating this enemy 10+ times.</summary>
    public bool DropsRevealed => TimesDefeated >= 10;
}
