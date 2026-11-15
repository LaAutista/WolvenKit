using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WolvenKit.Linux.Services;

namespace WolvenKit.Linux.ViewModels;

public partial class MainViewModel : ObservableObject
{
    private readonly ArchiveService _archives = new();
    private IReadOnlyList<ArchiveEntry> _allEntries = [];

    public ObservableCollection<GameInstall> Installs { get; } = [];
    public ObservableCollection<string> Archives { get; } = [];

    [ObservableProperty] private GameInstall? _selectedInstall;
    [ObservableProperty] private string? _selectedArchive;
    [ObservableProperty] private string _filter = "";
    [ObservableProperty] private IReadOnlyList<ArchiveEntry> _entries = [];
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _isBusy;

    /// <summary>The load started by the last archive selection; awaited by tests.</summary>
    public Task LoadTask { get; private set; } = Task.CompletedTask;

    public void DetectInstalls()
    {
        Installs.Clear();
        foreach (var install in GameLocator.FindInstalls())
        {
            Installs.Add(install);
        }
        SelectedInstall = Installs.FirstOrDefault();
        Status = Installs.Count == 0
            ? "No Cyberpunk 2077 install found. Use \"Add game folder…\"."
            : $"Found {Installs.Count} install(s).";
    }

    /// <returns>false if <paramref name="dir"/> is not a game root.</returns>
    public bool AddManualInstall(string dir)
    {
        if (!GameLocator.IsGameRoot(dir))
        {
            Status = $"Not a Cyberpunk 2077 folder (bin/x64/Cyberpunk2077.exe missing): {dir}";
            return false;
        }
        var install = Installs.FirstOrDefault(i => i.RootPath == dir) ?? new GameInstall(dir, "Manual");
        if (!Installs.Contains(install))
        {
            Installs.Add(install);
        }
        SelectedInstall = install;
        return true;
    }

    /// <summary>Shows an archive that is not part of the selected install (a mod download, for example).</summary>
    public void OpenArchiveFile(string path)
    {
        if (!Archives.Contains(path))
        {
            Archives.Add(path);
        }
        SelectedArchive = path;
    }

    partial void OnSelectedInstallChanged(GameInstall? value)
    {
        Archives.Clear();
        foreach (var archive in value?.EnumerateArchives() ?? [])
        {
            Archives.Add(archive);
        }
        SelectedArchive = null;
    }

    partial void OnSelectedArchiveChanged(string? value) => LoadTask = LoadArchiveAsync(value);

    partial void OnFilterChanged(string value) => ApplyFilter();

    private async Task LoadArchiveAsync(string? path)
    {
        _allEntries = [];
        ApplyFilter();
        if (path is null)
        {
            return;
        }

        IsBusy = true;
        Status = $"Reading {Path.GetFileName(path)}…";
        try
        {
            var entries = await Task.Run(() => _archives.ReadEntries(path));
            if (SelectedArchive != path)
            {
                return; // the user picked another archive meanwhile
            }
            _allEntries = entries;
            ApplyFilter();
            Status = $"{Path.GetFileName(path)}: {entries.Count} files";
        }
        catch (Exception e)
        {
            Status = $"Could not read {path}: {e.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void ApplyFilter() =>
        Entries = Filter.Length == 0
            ? _allEntries
            : _allEntries.Where(e => e.Path.Contains(Filter, StringComparison.OrdinalIgnoreCase)).ToList();

    public async Task ExtractAsync(IReadOnlyList<ArchiveEntry> entries, string outDir)
    {
        if (entries.Count == 0)
        {
            return;
        }
        if (Installs.Any(i => IsInside(outDir, i.RootPath)))
        {
            Status = "Refusing to extract into the game folder. Pick another folder.";
            return;
        }

        IsBusy = true;
        Status = $"Extracting {entries.Count} file(s)…";
        try
        {
            var written = await Task.Run(() => ArchiveService.Extract(entries, outDir));
            Status = $"Extracted {written}/{entries.Count} file(s) to {outDir}";
        }
        catch (Exception e)
        {
            Status = $"Extract failed: {e.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private static bool IsInside(string path, string root) =>
        (Path.GetFullPath(path) + Path.DirectorySeparatorChar).StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal);
}
