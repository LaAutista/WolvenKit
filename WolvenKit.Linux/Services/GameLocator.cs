using System.Text.Json;
using System.Text.RegularExpressions;

namespace WolvenKit.Linux.Services;

/// <param name="RootPath">Game root, the directory that contains <c>bin</c> and <c>archive</c>.</param>
/// <param name="Source">Where it was found: Steam, Heroic or Manual.</param>
public sealed record GameInstall(string RootPath, string Source)
{
    public string RootPath { get; } = Path.TrimEndingDirectorySeparator(Path.GetFullPath(RootPath));

    // the game resolves its own paths case-insensitively, so accept any spelling found on disk
    public string ExecutablePath => SafePaths.ResolveIgnoreCase(RootPath, "bin", "x64", "Cyberpunk2077.exe")
        ?? Path.Combine(RootPath, "bin", "x64", "Cyberpunk2077.exe");

    /// <summary>Base game, expansion and mod archives, in that order.</summary>
    public IEnumerable<string> EnumerateArchives()
    {
        foreach (var sub in new[] { "content", "ep1", "mod" })
        {
            if (SafePaths.ResolveIgnoreCase(RootPath, "archive", "pc", sub) is { } dir && Directory.Exists(dir))
            {
                foreach (var f in Directory.EnumerateFiles(dir, "*.archive", s_ignoreCase).Order(StringComparer.Ordinal))
                {
                    yield return f;
                }
            }
        }
    }

    private static readonly EnumerationOptions s_ignoreCase = new() { MatchCasing = MatchCasing.CaseInsensitive };

    public override string ToString() => $"{RootPath}  ({Source})";
}

/// <summary>Finds Cyberpunk 2077 installs on Linux (the game itself runs under Proton/Wine, its files are plain directories).</summary>
public static partial class GameLocator
{
    private const string SteamAppId = "1091500";

    public static bool IsGameRoot(string dir) =>
        File.Exists(SafePaths.ResolveIgnoreCase(dir, "bin", "x64", "Cyberpunk2077.exe")) &&
        Directory.Exists(SafePaths.ResolveIgnoreCase(dir, "archive", "pc", "content"));

    public static IReadOnlyList<GameInstall> FindInstalls(string? home = null)
    {
        home ??= Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        var found = new List<GameInstall>();

        void Add(string? dir, string source)
        {
            if (string.IsNullOrWhiteSpace(dir) || !IsGameRoot(dir))
            {
                return;
            }
            // real path, so the same install reached through two aliases is listed once
            var install = new GameInstall(TryRealPath(dir), source);
            if (found.All(g => g.RootPath != install.RootPath))
            {
                found.Add(install);
            }
        }

        // Native, Flatpak and Snap Steam. ~/.steam/steam is usually a symlink to the first one.
        var steamRoots = new[]
        {
            ".local/share/Steam", ".steam/steam", ".steam/root",
            ".var/app/com.valvesoftware.Steam/.local/share/Steam",
            "snap/steam/common/.local/share/Steam",
        }.Select(r => TryRealPath(Path.Combine(home, r))).Distinct();

        foreach (var steam in steamRoots)
        {
            var vdf = Path.Combine(steam, "steamapps", "libraryfolders.vdf");
            var libraries = ParseSteamLibraries(TryRead(vdf)).Append(steam);
            foreach (var lib in libraries.Distinct())
            {
                var apps = Path.Combine(lib, "steamapps");
                var manifest = Path.Combine(apps, $"appmanifest_{SteamAppId}.acf");
                var installDir = InstallDirRegex().Match(TryRead(manifest)).Groups[1].Value;
                Add(Path.Combine(apps, "common", installDir.Length > 0 ? installDir : "Cyberpunk 2077"), "Steam");
            }
        }

        // Heroic (GOG and Epic), native and Flatpak.
        foreach (var heroic in new[] { ".config/heroic", ".var/app/com.heroicgameslauncher.hgl/config/heroic" })
        {
            var cfg = Path.Combine(home, heroic);
            foreach (var json in new[] { "gog_store/installed.json", "legendaryConfig/legendary/installed.json" })
            {
                foreach (var path in ReadInstallPaths(Path.Combine(cfg, json)))
                {
                    Add(path, "Heroic");
                }
            }
        }

        return found;
    }

    internal static IEnumerable<string> ParseSteamLibraries(string vdf) =>
        LibraryPathRegex().Matches(vdf).Select(m => m.Groups[1].Value.Replace(@"\\", @"\"));

    /// <summary>Collects every "install_path" string in a launcher manifest, whatever its nesting.</summary>
    internal static IEnumerable<string> ReadInstallPaths(string jsonFile)
    {
        if (!File.Exists(jsonFile))
        {
            return [];
        }
        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllText(jsonFile));
            var result = new List<string>();
            Walk(doc.RootElement, result);
            return result;
        }
        catch (Exception e) when (e is JsonException or IOException or UnauthorizedAccessException)
        {
            return [];
        }

        static void Walk(JsonElement el, List<string> result)
        {
            if (el.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in el.EnumerateObject())
                {
                    if (p.NameEquals("install_path") && p.Value.ValueKind == JsonValueKind.String)
                    {
                        result.Add(p.Value.GetString()!);
                    }
                    else
                    {
                        Walk(p.Value, result);
                    }
                }
            }
            else if (el.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in el.EnumerateArray())
                {
                    Walk(item, result);
                }
            }
        }
    }

    /// <summary>A missing or unreadable launcher file must not stop discovery of the other launchers.</summary>
    private static string TryRead(string file)
    {
        try
        {
            return File.Exists(file) ? File.ReadAllText(file) : "";
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return "";
        }
    }

    private static string TryRealPath(string path)
    {
        try
        {
            return SafePaths.RealPath(path);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            return Path.GetFullPath(path);
        }
    }

    [GeneratedRegex("\"path\"\\s+\"([^\"]+)\"")]
    private static partial Regex LibraryPathRegex();

    [GeneratedRegex("\"installdir\"\\s+\"([^\"]+)\"")]
    private static partial Regex InstallDirRegex();
}
