using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using WolvenKit.Linux.ViewModels;
using WolvenKit.Linux.Views;

namespace WolvenKit.Linux;

public class App : Application
{
    public override void Initialize() => AvaloniaXamlLoader.Load(this);

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var vm = new MainViewModel();
            vm.DetectInstalls();
            // "wolvenkit some.archive" opens that archive (file manager "Open with")
            if (desktop.Args?.FirstOrDefault(File.Exists) is { } archive)
            {
                vm.OpenArchiveFile(Path.GetFullPath(archive));
            }
            desktop.MainWindow = new MainWindow { DataContext = vm };
        }
        base.OnFrameworkInitializationCompleted();
    }
}
