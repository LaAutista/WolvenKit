using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WolvenKit.Linux.Services;

namespace WolvenKit.Linux.ViewModels;

/// <param name="settingsPath">Settings file; null keeps everything in memory (tests).</param>
public partial class MainViewModel(string? settingsPath = null) : ObservableObject
{
    private readonly ArchiveService _archives = new();
    private readonly LinuxSettings _settings = settingsPath is null ? new() : LinuxSettings.Load(settingsPath);
    private IReadOnlyList<ArchiveEntry> _allEntries = [];

    public ObservableCollection<GameInstall> Installs { get; } = [];
    public ObservableCollection<string> Archives { get; } = [];

    [ObservableProperty] private GameInstall? _selectedInstall;
    [ObservableProperty] private string? _selectedArchive;
    [ObservableProperty] private string _filter = "";
    [ObservableProperty] private IReadOnlyList<ArchiveEntry> _entries = [];
    [ObservableProperty] private string _status = "";
    [ObservableProperty] private bool _isBusy;
    private int _running; // operations in flight; IsBusy stays true until all are done

    /// <summary>The load started by the last archive selection; awaited by tests.</summary>
    public Task LoadTask { get; private set; } = Task.CompletedTask;

    public void DetectInstalls()
    {
        Installs.Clear();
        foreach (var install in GameLocator.FindInstalls())
        {
            Installs.Add(install);
        }
        foreach (var dir in _settings.GameFolders.Where(d => GameLocator.IsGameRoot(d) && Installs.All(i => i.RootPath != d)))
        {
            Installs.Add(new GameInstall(dir, "Manual"));
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
        var manual = new GameInstall(dir, "Manual");
        var install = Installs.FirstOrDefault(i => i.RootPath == manual.RootPath) ?? manual;
        if (!Installs.Contains(install))
        {
            Installs.Add(install);
            if (settingsPath is not null && !_settings.GameFolders.Contains(dir))
            {
                _settings.GameFolders.Add(dir);
                try
                {
                    _settings.Save(settingsPath);
                }
                catch (Exception e) when (e is IOException or UnauthorizedAccessException)
                {
                    Status = $"Could not save settings: {e.Message}";
                }
            }
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
        try
        {
            foreach (var archive in value?.EnumerateArchives() ?? [])
            {
                Archives.Add(archive);
            }
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            Status = $"Could not list archives: {e.Message}";
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

        Begin();
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
            if (SelectedArchive == path)
            {
                Status = $"Could not read {path}: {e.Message}";
            }
        }
        finally
        {
            End();
        }
    }

    private void ApplyFilter() =>
        Entries = Filter.Length == 0
            ? _allEntries
            : _allEntries.Where(e => e.Path.Contains(Filter, StringComparison.OrdinalIgnoreCase)).ToList();

    private bool _extracting;

    public async Task ExtractAsync(IReadOnlyList<ArchiveEntry> entries, string outDir)
    {
        if (entries.Count == 0 || _extracting)
        {
            return;
        }

        _extracting = true;
        Begin();
        Status = $"Extracting {entries.Count} file(s)…";
        try
        {
            var written = await Task.Run(() => ArchiveService.Extract(entries, outDir));
            Status = $"Extracted {written}/{entries.Count} file(s) to {outDir}";
        }
        catch (Exception e)
        {
            Status = e is InvalidOperationException ? e.Message : $"Extract failed: {e.Message}";
        }
        finally
        {
            _extracting = false;
            End();
        }
    }

    private void Begin() => IsBusy = ++_running > 0;

    private void End() => IsBusy = --_running > 0;
}
