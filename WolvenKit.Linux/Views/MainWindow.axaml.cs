using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using WolvenKit.Linux.Services;
using WolvenKit.Linux.ViewModels;

namespace WolvenKit.Linux.Views;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    private MainViewModel Vm => (MainViewModel)DataContext!;

    // StorageProvider uses xdg-desktop-portal when available, so these are the desktop's native dialogs.
    private async Task<string?> PickFolderAsync(string title)
    {
        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions { Title = title });
        return folders.FirstOrDefault()?.TryGetLocalPath();
    }

    private async void OnAddGameFolder(object? sender, RoutedEventArgs e)
    {
        if (await PickFolderAsync("Select the Cyberpunk 2077 folder") is { } dir)
        {
            Vm.AddManualInstall(dir);
        }
    }

    private async void OnOpenArchive(object? sender, RoutedEventArgs e)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Open archive",
            FileTypeFilter = [new FilePickerFileType("REDengine archive") { Patterns = ["*.archive"] }],
        });
        if (files.FirstOrDefault()?.TryGetLocalPath() is { } path)
        {
            Vm.OpenArchiveFile(path);
        }
    }

    private async void OnExtract(object? sender, RoutedEventArgs e)
    {
        var selected = EntryList.SelectedItems?.OfType<ArchiveEntry>().ToList() ?? [];
        if (selected.Count == 0)
        {
            Vm.Status = "Select one or more files first.";
            return;
        }
        if (await PickFolderAsync("Extract to folder") is { } dir)
        {
            await Vm.ExtractAsync(selected, dir);
        }
    }
}
