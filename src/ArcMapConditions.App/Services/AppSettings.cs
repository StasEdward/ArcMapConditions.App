using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ArcMapConditions.App.Services;

/// <summary>
/// Small persisted settings file at
/// %APPDATA%\ArcMapConditions\settings.json (per-user, no admin needed).
/// </summary>
public sealed class AppSettings
{
    /// <summary>Restore the last window position on startup (default on).</summary>
    public bool RememberPosition { get; set; } = true;

    /// <summary>Last window left, in WPF units across the whole virtual desktop.</summary>
    public double? WindowLeft { get; set; }

    /// <summary>Last window top, in WPF units across the whole virtual desktop.</summary>
    public double? WindowTop { get; set; }

    /// <summary>Screen ID where window was last positioned (to handle monitor changes).</summary>
    public string? LastScreenId { get; set; }

    [JsonIgnore]
    private static string Dir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "ArcMapConditions");

    [JsonIgnore]
    private static string FilePath => Path.Combine(Dir, "settings.json");

    public static AppSettings Load()
    {
        try
        {
            if (File.Exists(FilePath))
            {
                AppSettings? loaded = JsonSerializer.Deserialize<AppSettings>(File.ReadAllText(FilePath));
                if (loaded is not null)
                    return loaded;
            }
        }
        catch
        {
            // Corrupt or unreadable — fall back to defaults.
        }

        return new AppSettings();
    }

    public void Save()
    {
        try
        {
            Directory.CreateDirectory(Dir);
            File.WriteAllText(FilePath,
                JsonSerializer.Serialize(this, new JsonSerializerOptions { WriteIndented = true }));
        }
        catch
        {
            // Best-effort — never let a settings write crash the app.
        }
    }
}
