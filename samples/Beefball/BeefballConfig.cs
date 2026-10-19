using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Xna.Framework;

namespace Beefball;

/// <summary>
/// Every tunable number and palette color, loaded from Content/beefball.json. Missing keys keep
/// the defaults below, so a partial or broken file never prevents the game from starting.
/// </summary>
public sealed class BeefballConfig
{
    public PlayerConfig Player { get; set; } = new();
    public PuckConfig Puck { get; set; } = new();
    public CollisionConfig Collision { get; set; } = new();
    public MatchConfig Match { get; set; } = new();
    public GlowConfig Glow { get; set; } = new();
    public PaletteConfig Palette { get; set; } = new();

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        Converters = { new HexColorConverter() },
    };

    /// <summary>Reads the copy under the build output (the engine's hot reload keeps it fresh).</summary>
    public static BeefballConfig Load()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Content", "beefball.json");
        try
        {
            return JsonSerializer.Deserialize<BeefballConfig>(File.ReadAllText(path), Options) ?? new BeefballConfig();
        }
        catch (Exception e) when (e is IOException or JsonException or UnauthorizedAccessException)
        {
            // Mid-save or hand-edit typo during hot reload: keep playing with defaults.
            Console.Error.WriteLine($"beefball.json not loaded ({e.Message}); using defaults.");
            return new BeefballConfig();
        }
    }
}

public sealed class PlayerConfig
{
    public float Radius { get; set; } = 16f;
    public float Acceleration { get; set; } = 340f;
    public float Drag { get; set; } = 1f;
    public float DashSpeed { get; set; } = 600f;
    public float DashCooldownSeconds { get; set; } = 2f;
    public float Mass { get; set; } = 1f;
    public float CooldownIndicatorRadius { get; set; } = 11f;
}

public sealed class PuckConfig
{
    public float Radius { get; set; } = 6f;
    public float Drag { get; set; } = 0.4f;
    public float Mass { get; set; } = 0.3f;
}

public sealed class CollisionConfig
{
    public float WallElasticity { get; set; } = 1f;
    public float PlayerPlayerElasticity { get; set; } = 1f;
    public float PlayerPuckElasticity { get; set; } = 1f;
}

public sealed class MatchConfig
{
    public int ScoreToWin { get; set; } = 5;
    public float GoalPauseSeconds { get; set; } = 1.5f;
}

public sealed class GlowConfig
{
    /// <summary>Global multiplier on every glow alpha. 0 turns glow off.</summary>
    public float Strength { get; set; } = 1f;
    public RadialGlowConfig Player { get; set; } = new() { RadiusScale = 2.6f, Alpha = 0.24f };
    public RadialGlowConfig Puck { get; set; } = new() { RadiusScale = 5f, Alpha = 0.40f };
    public LineGlowConfig Wall { get; set; } = new() { Width = 30f, Alpha = 0.20f };
    /// <summary>Alpha at the back wall of an idle goal pocket; it fades to nothing at the mouth.</summary>
    public float PocketAlpha { get; set; } = 0.14f;
}

/// <summary>A radial glow: outer radius is the object radius times <see cref="RadiusScale"/>.</summary>
public sealed class RadialGlowConfig
{
    public float RadiusScale { get; set; }
    public float Alpha { get; set; }
}

/// <summary>A glow strip along a wall: <see cref="Width"/> world units each side, fading out from <see cref="Alpha"/>.</summary>
public sealed class LineGlowConfig
{
    public float Width { get; set; }
    public float Alpha { get; set; }
}

public sealed class PaletteConfig
{
    public Color Background { get; set; } = new(4, 5, 11);
    public Color Team1 { get; set; } = new(0x35, 0xf0, 0xff);
    public Color Team2 { get; set; } = new(0xff, 0x3e, 0xa5);
    public Color Puck { get; set; } = new(0xff, 0x6a, 0x3d);
    public Color PuckCore { get; set; } = new(0xff, 0xf1, 0xe8);
    public Color TrailEnd { get; set; } = new(0xff, 0x2a, 0x3a);
    public Color PlayerBody { get; set; } = new(0x0a, 0x0d, 0x1c);
    public Color Text { get; set; } = new(0xe9, 0xf3, 0xff);

    public Color TeamColor(int team) => team == 1 ? Team1 : Team2;
}

/// <summary>Reads "#rrggbb" strings.</summary>
public sealed class HexColorConverter : JsonConverter<Color>
{
    public override Color Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var s = reader.GetString() ?? throw new JsonException("Color must be a string like \"#35f0ff\".");
        s = s.TrimStart('#');
        if (s.Length != 6 || !int.TryParse(s, System.Globalization.NumberStyles.HexNumber, null, out int rgb))
            throw new JsonException($"Bad color \"{s}\"; expected #rrggbb.");
        return new Color((rgb >> 16) & 0xff, (rgb >> 8) & 0xff, rgb & 0xff);
    }

    public override void Write(Utf8JsonWriter writer, Color value, JsonSerializerOptions options)
        => writer.WriteStringValue($"#{value.R:x2}{value.G:x2}{value.B:x2}");
}
