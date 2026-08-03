namespace Strikers1945Sample.Entities;

/// <summary>
/// Defines a selectable plane type with unique shot patterns and special attacks.
/// </summary>
public record PlaneData(
    string Name,
    string SpriteName,
    string Description
)
{
    public static readonly PlaneData[] AllPlanes =
    {
        new("P-38 Lightning", "ship_0000", "Twin vulcan + Fork Lightning charge"),
        new("Spitfire",       "ship_0001", "Focused triple + Piercing Lance charge"),
        new("Mosquito",       "ship_0002", "Wide spread + Homing Salvo charge"),
        new("Zero",           "ship_0003", "Rapid vulcan + Blade Wave charge"),
    };

    /// <summary>
    /// Returns gun offsets for the given weapon level (1-4).
    /// Each float[] is { xOffset, angleOffset }.
    /// </summary>
    public float[][] GetShotPattern(int weaponLevel) => Name switch
    {
        "P-38 Lightning" => weaponLevel switch
        {
            1 => new[] { new[] { -12f, 0f }, new[] { 12f, 0f } },
            2 => new[] { new[] { -14f, 0f }, new[] { 0f, 0f }, new[] { 14f, 0f } },
            3 => new[] { new[] { -16f, 0f }, new[] { -6f, 0f }, new[] { 6f, 0f }, new[] { 16f, 0f } },
            _ => new[] { new[] { -18f, -0.15f }, new[] { -8f, 0f }, new[] { 0f, 0f }, new[] { 8f, 0f }, new[] { 18f, 0.15f } },
        },
        "Spitfire" => weaponLevel switch
        {
            1 => new[] { new[] { -5f, 0f }, new[] { 0f, 0f }, new[] { 5f, 0f } },
            2 => new[] { new[] { -6f, 0f }, new[] { -2f, 0f }, new[] { 2f, 0f }, new[] { 6f, 0f } },
            3 => new[] { new[] { -8f, -0.05f }, new[] { -3f, 0f }, new[] { 3f, 0f }, new[] { 8f, 0.05f } },
            _ => new[] { new[] { -10f, -0.08f }, new[] { -4f, 0f }, new[] { 0f, 0f }, new[] { 4f, 0f }, new[] { 10f, 0.08f } },
        },
        "Mosquito" => weaponLevel switch
        {
            1 => new[] { new[] { -16f, -0.15f }, new[] { -6f, -0.05f }, new[] { 0f, 0f }, new[] { 6f, 0.05f }, new[] { 16f, 0.15f } },
            2 => new[] { new[] { -18f, -0.18f }, new[] { -8f, -0.06f }, new[] { 0f, 0f }, new[] { 8f, 0.06f }, new[] { 18f, 0.18f } },
            3 => new[] { new[] { -20f, -0.22f }, new[] { -12f, -0.1f }, new[] { -4f, 0f }, new[] { 4f, 0f }, new[] { 12f, 0.1f }, new[] { 20f, 0.22f } },
            _ => new[] { new[] { -22f, -0.25f }, new[] { -14f, -0.12f }, new[] { -6f, -0.04f }, new[] { 0f, 0f }, new[] { 6f, 0.04f }, new[] { 14f, 0.12f }, new[] { 22f, 0.25f } },
        },
        "Zero" => weaponLevel switch
        {
            1 => new[] { new[] { 0f, 0f } },
            2 => new[] { new[] { -4f, 0f }, new[] { 4f, 0f } },
            3 => new[] { new[] { -6f, 0f }, new[] { 0f, 0f }, new[] { 6f, 0f } },
            _ => new[] { new[] { -8f, 0f }, new[] { -3f, 0f }, new[] { 3f, 0f }, new[] { 8f, 0f } },
        },
        _ => new[] { new[] { 0f, 0f } },
    };

    /// <summary>
    /// Fire rate for this plane. Zero fires fastest.
    /// </summary>
    public float FireRate => Name switch
    {
        "Zero" => 0.09f,  // rapid vulcan
        "Mosquito" => 0.21f,
        _ => 0.15f,
    };
}
