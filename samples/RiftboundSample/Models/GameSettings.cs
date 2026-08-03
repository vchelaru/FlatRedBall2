using System.Diagnostics;
using System.Text.Json;

namespace RiftboundSample.Models;

public class GameSettings
{
    public float DefaultBattleSpeed { get; set; } = 1f;
    public float TextSpeed { get; set; } = 1f;
    public bool ShowEnemyATB { get; set; } = true;
    public bool AutoBattleDefault { get; set; }

    private static readonly string SettingsDirectory = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Riftbound");

    private static readonly string SettingsPath = Path.Combine(SettingsDirectory, "settings.json");

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    public bool Save()
    {
        try
        {
            Directory.CreateDirectory(SettingsDirectory);
            string json = JsonSerializer.Serialize(this, JsonOptions);
            File.WriteAllText(SettingsPath, json);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Settings save failed: {ex.Message}");
            return false;
        }
    }

    public static GameSettings Load()
    {
        try
        {
            if (!File.Exists(SettingsPath))
                return new GameSettings();

            string json = File.ReadAllText(SettingsPath);
            return JsonSerializer.Deserialize<GameSettings>(json, JsonOptions) ?? new GameSettings();
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Settings load failed: {ex.Message}");
            return new GameSettings();
        }
    }
}
