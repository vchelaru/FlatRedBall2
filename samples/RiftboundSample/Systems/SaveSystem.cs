using System.Diagnostics;
using System.Text.Json;
using RiftboundSample.Models;

namespace RiftboundSample.Systems;

public class SaveSystem
{
    private const int MaxSlots = 20;
    private const string AutosaveSlot = "autosave";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
    };

    private static string SaveDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Riftbound", "saves");

    public static bool Save(SaveData data, int slot)
    {
        if (slot < 0 || slot > MaxSlots) return false;
        return WriteFile(data, $"save_{slot:D2}.json");
    }

    public static bool Autosave(SaveData data)
        => WriteFile(data, $"{AutosaveSlot}.json");

    public static SaveData? Load(int slot)
    {
        if (slot < 0 || slot > MaxSlots) return null;
        return ReadFile($"save_{slot:D2}.json");
    }

    public static SaveData? LoadAutosave()
        => ReadFile($"{AutosaveSlot}.json");

    /// <summary>Returns metadata for all save slots (index 0..MaxSlots + autosave at end). Null entries are empty slots.</summary>
    public static List<SaveSlotInfo?> GetAllSlots()
    {
        var slots = new List<SaveSlotInfo?>();
        for (int i = 0; i <= MaxSlots; i++)
        {
            var data = Load(i);
            slots.Add(data != null ? SaveSlotInfo.FromData(data, i) : null);
        }

        // Autosave
        var auto = LoadAutosave();
        slots.Add(auto != null ? SaveSlotInfo.FromData(auto, -1, isAutosave: true) : null);

        return slots;
    }

    private static bool WriteFile(SaveData data, string fileName)
    {
        try
        {
            data.SaveTime = DateTime.Now;
            string dir = SaveDirectory;
            Directory.CreateDirectory(dir);
            string path = Path.Combine(dir, fileName);
            string json = JsonSerializer.Serialize(data, JsonOptions);
            File.WriteAllText(path, json);
            return true;
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Save failed: {ex.Message}");
            return false;
        }
    }

    private static SaveData? ReadFile(string fileName)
    {
        try
        {
            string path = Path.Combine(SaveDirectory, fileName);
            if (!File.Exists(path)) return null;
            string json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<SaveData>(json, JsonOptions);
        }
        catch (Exception ex)
        {
            Debug.WriteLine($"Load failed: {ex.Message}");
            return null;
        }
    }
}

public class SaveSlotInfo
{
    public int SlotIndex { get; set; }
    public bool IsAutosave { get; set; }
    public string CurrentMap { get; set; } = "";
    public DateTime SaveTime { get; set; }
    public TimeSpan PlayTime { get; set; }
    public string DisplayName { get; set; } = "";

    public static SaveSlotInfo FromData(SaveData data, int slot, bool isAutosave = false) => new()
    {
        SlotIndex = slot,
        IsAutosave = isAutosave,
        CurrentMap = data.CurrentMap,
        SaveTime = data.SaveTime,
        PlayTime = data.PlayTime,
        DisplayName = isAutosave ? "Autosave" : $"Slot {slot + 1}",
    };
}
