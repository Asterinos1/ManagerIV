using System.IO;
using System.Runtime.CompilerServices;

namespace ManagerIV.Core;

/// <summary>
/// Model class wrapping all settings within Liberty''s Legacy trainer INI configuration file.
/// Uses a dictionary-backed store preserving comments and section layout.
/// </summary>
public class LibertyLegacyConfig : ViewModelBase
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    private string GetVal(string key, string defaultVal) => _values.TryGetValue(key, out var val) ? val : defaultVal;
    private void SetVal(string key, string val, [CallerMemberName] string propName = "")
    {
        if (!_values.TryGetValue(key, out var current) || current != val)
        {
            _values[key] = val;
            OnPropertyChanged(propName);
        }
    }

    private bool GetBool(string key, bool defaultVal)
    {
        string raw = GetVal(key, "");
        if (raw == "1" || raw.Equals("true", StringComparison.OrdinalIgnoreCase)) return true;
        if (raw == "0" || raw.Equals("false", StringComparison.OrdinalIgnoreCase)) return false;
        return defaultVal;
    }

    private void SetBool(string key, bool val, [CallerMemberName] string propName = "")
    {
        SetVal(key, val ? "1" : "0", propName);
    }

    private int GetInt(string key, int defaultVal) => int.TryParse(GetVal(key, ""), out int val) ? val : defaultVal;
    private void SetInt(string key, int val, [CallerMemberName] string propName = "") => SetVal(key, val.ToString(), propName);

    // ==========================================
    // Core Trainer Settings
    // ==========================================

    public int MenuKey
    {
        get => GetInt("MenuKey", 122);
        set => SetInt("MenuKey", value);
    }

    public bool ControllerKeyCombo
    {
        get => GetBool("ControllerKeyCombo", true);
        set => SetBool("ControllerKeyCombo", value);
    }

    public bool OpenMenuSound
    {
        get => GetBool("OpenMenuSound", true);
        set => SetBool("OpenMenuSound", value);
    }

    // ==========================================
    // Player Startup Toggles
    // ==========================================

    public bool GodMode
    {
        get => GetBool("GodMode", false);
        set => SetBool("GodMode", value);
    }

    public bool NeverWanted
    {
        get => GetBool("NeverWanted", false);
        set => SetBool("NeverWanted", value);
    }

    public bool InfiniteAmmo
    {
        get => GetBool("InfiniteAmmo", false);
        set => SetBool("InfiniteAmmo", value);
    }

    // ==========================================
    // Vehicles & HUD
    // ==========================================

    public bool Speedometer
    {
        get => GetBool("Speedometer", true);
        set => SetBool("Speedometer", value);
    }

    public int SpeedometerUnit
    {
        get => GetInt("SpeedometerUnit", 0);
        set => SetInt("SpeedometerUnit", value);
    }

    public bool VehicleSpawnInVehicle
    {
        get => GetBool("VehicleSpawnInVehicle", true);
        set => SetBool("VehicleSpawnInVehicle", value);
    }

    // ==========================================
    // World & Environment
    // ==========================================

    public bool FreezeWeather
    {
        get => GetBool("FreezeWeather", false);
        set => SetBool("FreezeWeather", value);
    }

    public bool FreezeTime
    {
        get => GetBool("FreezeTime", false);
        set => SetBool("FreezeTime", value);
    }

    // ==========================================
    // Load and Save Helpers
    // ==========================================

    public static LibertyLegacyConfig Load(string path)
    {
        var config = new LibertyLegacyConfig();
        if (!File.Exists(path)) return config;

        try
        {
            var lines = File.ReadAllLines(path);
            foreach (var line in lines)
            {
                var trimmed = line.Trim();
                if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith(";") || trimmed.StartsWith("#") || trimmed.StartsWith("["))
                    continue;

                var parts = trimmed.Split('=', 2);
                if (parts.Length == 2)
                {
                    string key = parts[0].Trim();
                    string val = parts[1].Trim();
                    config._values[key] = val;
                }
            }
        }
        catch { }

        return config;
    }

    public static void Save(string path, LibertyLegacyConfig config)
    {
        try
        {
            var lines = File.Exists(path) ? File.ReadAllLines(path).ToList() : new List<string>();
            var keysWritten = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < lines.Count; i++)
            {
                var trimmed = lines[i].Trim();
                if (trimmed.StartsWith(";") || trimmed.StartsWith("#") || trimmed.StartsWith("["))
                    continue;

                var parts = trimmed.Split('=', 2);
                if (parts.Length >= 2)
                {
                    string key = parts[0].Trim();
                    if (config._values.TryGetValue(key, out var val))
                    {
                        lines[i] = $"{key}={val}";
                        keysWritten.Add(key);
                    }
                }
            }

            // If empty file, ensure [Settings] header exists
            if (lines.Count == 0 || !lines.Any(l => l.Trim().StartsWith("[Settings]", StringComparison.OrdinalIgnoreCase)))
            {
                lines.Insert(0, "[Settings]");
            }

            foreach (var kvp in config._values)
            {
                if (!keysWritten.Contains(kvp.Key))
                {
                    lines.Add($"{kvp.Key}={kvp.Value}");
                }
            }

            var dir = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            File.WriteAllLines(path, lines);
        }
        catch { }
    }
}
