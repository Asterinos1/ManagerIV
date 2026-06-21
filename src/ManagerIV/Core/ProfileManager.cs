using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ManagerIV.Core;

/// <summary>
/// Handles saving and loading profile configurations to and from local JSON files.
/// </summary>
public class ProfileManager
{
    private readonly JsonSerializerOptions _jsonOptions;

    public ProfileManager()
    {
        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true,
            Converters = { new JsonStringEnumConverter() }
        };
    }

    /// <summary>
    /// Serializes and saves a Profile object to the specified file path.
    /// </summary>
    public void SaveProfile(string filePath, Profile profile)
    {
        if (profile == null) throw new ArgumentNullException(nameof(profile));
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("File path cannot be null or whitespace.", nameof(filePath));

        string? dir = Path.GetDirectoryName(filePath);
        if (dir != null && !Directory.Exists(dir))
        {
            Directory.CreateDirectory(dir);
        }

        string json = JsonSerializer.Serialize(profile, _jsonOptions);
        File.WriteAllText(filePath, json);
    }

    /// <summary>
    /// Loads and deserializes a Profile object from the specified file path.
    /// </summary>
    public Profile LoadProfile(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath)) throw new ArgumentException("File path cannot be null or whitespace.", nameof(filePath));
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("Profile file not found.", filePath);
        }

        string json = File.ReadAllText(filePath);
        var profile = JsonSerializer.Deserialize<Profile>(json, _jsonOptions);
        
        return profile ?? throw new InvalidOperationException("Failed to deserialize profile JSON.");
    }
}
