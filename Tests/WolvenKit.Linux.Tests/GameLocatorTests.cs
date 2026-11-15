using WolvenKit.Linux.Services;
using Xunit;

namespace WolvenKit.Linux.Tests;

public class GameLocatorTests
{
    [Fact]
    public void Finds_steam_install_in_second_library_once_despite_symlinked_root()
    {
        using var home = new TempDir();
        var steam = home.Sub(".local", "share", "Steam", "steamapps");
        var library = home.Sub("mnt", "games");
        File.WriteAllText(Path.Combine(steam, "libraryfolders.vdf"),
            $"\"libraryfolders\"\n{{\n\t\"0\"\n\t{{\n\t\t\"path\"\t\t\"{home.Path}/.local/share/Steam\"\n\t}}\n\t\"1\"\n\t{{\n\t\t\"path\"\t\t\"{library}\"\n\t}}\n}}");
        File.WriteAllText(Path.Combine(home.Sub("mnt", "games", "steamapps"), "appmanifest_1091500.acf"),
            "\"AppState\"\n{\n\t\"installdir\"\t\t\"CP2077 Custom Dir\"\n}");
        var game = home.FakeGame("mnt", "games", "steamapps", "common", "CP2077 Custom Dir");
        // ~/.steam/steam -> ~/.local/share/Steam, as the Steam client sets it up
        Directory.CreateSymbolicLink(Path.Combine(home.Sub(".steam"), "steam"), Path.Combine(home.Path, ".local", "share", "Steam"));

        var installs = GameLocator.FindInstalls(home.Path);

        var install = Assert.Single(installs);
        Assert.Equal(game, install.RootPath);
        Assert.Equal("Steam", install.Source);
    }

    [Fact]
    public void Finds_heroic_gog_install_and_ignores_other_games()
    {
        using var home = new TempDir();
        var game = home.FakeGame("Games", "Heroic", "Cyberpunk 2077");
        var other = home.Sub("Games", "Heroic", "Other Game");
        File.WriteAllText(Path.Combine(home.Sub(".config", "heroic", "gog_store"), "installed.json"),
            $$"""{"installed":[{"appName":"1","install_path":"{{other}}"},{"appName":"1423049311","install_path":"{{game}}"}]}""");

        var install = Assert.Single(GameLocator.FindInstalls(home.Path));

        Assert.Equal(game, install.RootPath);
        Assert.Equal("Heroic", install.Source);
        Assert.Equal(Path.Combine(game, "bin", "x64", "Cyberpunk2077.exe"), install.ExecutablePath);
    }

    [Fact]
    public void Broken_manifest_and_empty_home_give_no_installs()
    {
        using var home = new TempDir();
        File.WriteAllText(Path.Combine(home.Sub(".config", "heroic", "gog_store"), "installed.json"), "{not json");

        Assert.Empty(GameLocator.FindInstalls(home.Path));
    }

    [Fact]
    public void Archives_are_listed_base_then_ep1_then_mod()
    {
        using var home = new TempDir();
        var game = home.FakeGame("game");
        File.WriteAllText(Path.Combine(game, "archive", "pc", "content", "basegame_1.archive"), "");
        File.WriteAllText(Path.Combine(home.Sub("game", "archive", "pc", "mod"), "my.archive"), "");
        File.WriteAllText(Path.Combine(home.Sub("game", "archive", "pc", "ep1"), "ep1_1.archive"), "");

        var names = new GameInstall(game, "Manual").EnumerateArchives().Select(Path.GetFileName);

        Assert.Equal(["basegame_1.archive", "ep1_1.archive", "my.archive"], names);
    }
}
