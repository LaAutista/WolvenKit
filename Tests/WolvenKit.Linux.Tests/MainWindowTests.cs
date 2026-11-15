using Avalonia.Controls;
using Avalonia.Headless;
using Avalonia.Threading;
using WolvenKit.Common.Services;
using WolvenKit.Linux.Services;
using WolvenKit.Linux.ViewModels;
using WolvenKit.Linux.Views;
using WolvenKit.RED4.Archive.IO;
using Xunit;

namespace WolvenKit.Linux.Tests;

public class MainWindowTests
{
    /// <summary>Packs two generated files into an archive with the real writer. No game assets involved.</summary>
    private static string PackFixture(TempDir tmp)
    {
        var src = tmp.Sub("src", "base", "linux_port_test");
        File.WriteAllText(Path.Combine(src, "hello.json"), "hello from linux");
        File.WriteAllBytes(Path.Combine(src, "zeros.csv"), new byte[64 * 1024]); // compressible, goes through Kraken (.txt is not packable, .bin is stored uncompressed)
        var archive = Path.Combine(tmp.Sub("out"), "fixture.archive");
        using (var fs = File.Create(archive))
        {
            Assert.True(new ArchiveWriter(new HashService(), new SerilogWrapper()).WriteArchive(new DirectoryInfo(Path.Combine(tmp.Path, "src")), fs));
        }
        Assert.True(new FileInfo(archive).Length < 32 * 1024, "zeros.csv was not compressed");
        return archive;
    }

    [Fact]
    public async Task Window_lists_archive_entries_filters_and_extracts_them_intact()
    {
        using var tmp = new TempDir();
        var archive = PackFixture(tmp);
        var outDir = tmp.Sub("extracted");

        using var session = HeadlessUnitTestSession.StartNew(typeof(App));
        await session.Dispatch(async () =>
        {
            var vm = new MainViewModel();
            var window = new MainWindow { DataContext = vm };
            window.Show();

            vm.OpenArchiveFile(archive);
            await vm.LoadTask;
            Dispatcher.UIThread.RunJobs();

            var list = window.FindControl<ListBox>("EntryList")!;
            Assert.Equal(2, list.ItemCount);
            Assert.Contains("fixture.archive: 2 files", vm.Status);

            vm.Filter = "HELLO";
            Dispatcher.UIThread.RunJobs();
            var entry = Assert.IsType<ArchiveEntry>(Assert.Single(list.Items));
            Assert.Equal("base/linux_port_test/hello.json", entry.Path);

            vm.Filter = "";
            await vm.ExtractAsync(vm.Entries, outDir);
            Assert.Contains("Extracted 2/2", vm.Status);
            window.Close();
            return true; // a result type selects the Dispatch overload that awaits this lambda
        }, CancellationToken.None);

        Assert.Equal("hello from linux", File.ReadAllText(Path.Combine(outDir, "base", "linux_port_test", "hello.json")));
        Assert.Equal(new byte[64 * 1024], File.ReadAllBytes(Path.Combine(outDir, "base", "linux_port_test", "zeros.csv")));
    }

    [Fact]
    public async Task Extracting_into_the_game_folder_is_refused()
    {
        using var tmp = new TempDir();
        var game = tmp.FakeGame("game");
        var archive = PackFixture(tmp);
        var vm = new MainViewModel();
        Assert.True(vm.AddManualInstall(game));
        vm.OpenArchiveFile(archive);
        await vm.LoadTask;

        var target = Path.Combine(game, "archive", "pc", "mod");
        await vm.ExtractAsync(vm.Entries, target);

        Assert.Contains("Refusing", vm.Status);
        Assert.False(Directory.Exists(target));
    }

    [Fact]
    public void Folder_that_is_not_a_game_is_rejected()
    {
        using var tmp = new TempDir();
        var vm = new MainViewModel();

        Assert.False(vm.AddManualInstall(tmp.Path));
        Assert.Empty(vm.Installs);
    }
}
