using System.Text.Json;

namespace WolvenKit.Linux.Services;

/// <summary>User settings of the Linux app, stored as JSON under $XDG_CONFIG_HOME/wolvenkit.</summary>
public sealed class LinuxSettings
{
    private static readonly JsonSerializerOptions s_json = new() { WriteIndented = true };

    /// <summary>Game folders the user added by hand.</summary>
    public List<string> GameFolders { get; set; } = [];

    public static string DefaultPath
    {
        get
        {
            var xdg = Environment.GetEnvironmentVariable("XDG_CONFIG_HOME");
            var config = !string.IsNullOrEmpty(xdg) && Path.IsPathRooted(xdg)
                ? xdg
                : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".config");
            return Path.Combine(config, "wolvenkit", "settings.json");
        }
    }

    /// <summary>A missing or unreadable file gives default settings; it is never an error at startup.</summary>
    public static LinuxSettings Load(string path)
    {
        try
        {
            return File.Exists(path) ? JsonSerializer.Deserialize<LinuxSettings>(File.ReadAllText(path)) ?? new() : new();
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return new();
        }
    }

    public void Save(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        // write beside the target and rename, so a crash cannot leave a half-written settings file
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(this, s_json));
        File.Move(tmp, path, overwrite: true);
    }
}
