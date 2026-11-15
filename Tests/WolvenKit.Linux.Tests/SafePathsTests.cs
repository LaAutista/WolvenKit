using WolvenKit.Linux.Services;
using WolvenKit.Linux.ViewModels;
using Xunit;

namespace WolvenKit.Linux.Tests;

public class SafePathsTests
{
    [Theory]
    [InlineData("../outside.json")]
    [InlineData(@"..\outside.json")]
    [InlineData(@"base\..\..\outside.json")]
    [InlineData("/etc/outside.json")]
    public void Entry_names_cannot_leave_the_output_folder(string entry)
    {
        using var tmp = new TempDir();
        var ex = Assert.Throws<InvalidOperationException>(() => SafePaths.ResolveDestination(tmp.Sub("out"), entry));
        Assert.Contains("outside the output folder", ex.Message);
    }

    [Fact]
    public void Normal_entry_resolves_below_the_output_folder()
    {
        using var tmp = new TempDir();
        var outDir = tmp.Sub("out");
        Assert.Equal(Path.Combine(outDir, "base", "a.json"), SafePaths.ResolveDestination(outDir, @"base\a.json"));
    }

    [Fact]
    public void Symlinked_subfolder_pointing_into_a_game_is_refused()
    {
        using var tmp = new TempDir();
        var game = tmp.FakeGame("game");
        var outDir = tmp.Sub("out");
        Directory.CreateSymbolicLink(Path.Combine(outDir, "base"), game);

        var ex = Assert.Throws<InvalidOperationException>(() => SafePaths.ResolveDestination(outDir, "base/file.json"));
        Assert.Contains("symbolic link", ex.Message);
    }

    [Fact]
    public void Output_folder_that_is_a_symlink_into_a_game_is_refused()
    {
        using var tmp = new TempDir();
        var game = tmp.FakeGame("game");
        var link = Path.Combine(tmp.Path, "innocent");
        Directory.CreateSymbolicLink(link, Path.Combine(game, "archive"));

        Assert.Throws<InvalidOperationException>(() => SafePaths.ResolveDestination(SafePaths.RealPath(link), "base/file.json"));
    }

    [Fact]
    public void Parent_of_a_game_is_fine_until_an_entry_lands_inside_the_game()
    {
        using var tmp = new TempDir();
        tmp.FakeGame("games", "cp");
        var parent = Path.Combine(tmp.Path, "games");

        SafePaths.ResolveDestination(parent, "elsewhere/file.json");
        Assert.Throws<InvalidOperationException>(() => SafePaths.ResolveDestination(parent, "cp/archive/pc/mod/file.archive"));
    }

    [Fact]
    public void Game_with_different_letter_case_is_recognised_and_its_archives_listed()
    {
        using var tmp = new TempDir();
        var game = tmp.Sub("game");
        File.WriteAllText(Path.Combine(tmp.Sub("game", "Bin", "X64"), "cyberpunk2077.EXE"), "");
        File.WriteAllText(Path.Combine(tmp.Sub("game", "Archive", "PC", "Content"), "basegame_1.ARCHIVE"), "");

        Assert.True(GameLocator.IsGameRoot(game));
        var install = new GameInstall(game + "/", "Manual");
        Assert.Equal(game, install.RootPath);
        Assert.EndsWith("cyberpunk2077.EXE", install.ExecutablePath);
        Assert.Single(install.EnumerateArchives());
    }

    [Fact]
    public async Task Unregistered_game_folder_given_with_trailing_slash_is_still_protected()
    {
        using var tmp = new TempDir();
        var game = tmp.FakeGame("game");
        var vm = new MainViewModel(); // the game is NOT added to the install list
        var entry = new ArchiveEntry("base/a.json", 1, 1, null!);

        await vm.ExtractAsync([entry], Path.Combine(game, "archive") + "/");

        Assert.Contains("Refusing", vm.Status);
        Assert.False(Directory.Exists(Path.Combine(game, "archive", "base")));
    }

    [Fact]
    public void Unreadable_steam_manifest_does_not_stop_discovery()
    {
        if (Environment.UserName == "root")
        {
            return; // root ignores file modes
        }
        using var tmp = new TempDir();
        var vdf = Path.Combine(tmp.Sub(".local", "share", "Steam", "steamapps"), "libraryfolders.vdf");
        File.WriteAllText(vdf, "x");
        File.SetUnixFileMode(vdf, UnixFileMode.None);
        var game = tmp.FakeGame("Games", "cp");
        File.WriteAllText(Path.Combine(tmp.Sub(".config", "heroic", "gog_store"), "installed.json"),
            $$"""{"installed":[{"install_path":"{{game}}"}]}""");

        Assert.Equal(game, Assert.Single(GameLocator.FindInstalls(tmp.Path)).RootPath);
    }
}
