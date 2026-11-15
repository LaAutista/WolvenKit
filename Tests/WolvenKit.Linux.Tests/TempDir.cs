namespace WolvenKit.Linux.Tests;

internal sealed class TempDir : IDisposable
{
    public string Path { get; } = Directory.CreateTempSubdirectory("wkit-linux-tests-").FullName;

    public string Sub(params string[] parts)
    {
        var dir = System.IO.Path.Combine([Path, .. parts]);
        Directory.CreateDirectory(dir);
        return dir;
    }

    /// <summary>Creates the files GameLocator looks for and returns the game root.</summary>
    public string FakeGame(params string[] parts)
    {
        var root = Sub(parts);
        File.WriteAllText(System.IO.Path.Combine(Sub([.. parts, "bin", "x64"]), "Cyberpunk2077.exe"), "");
        Sub([.. parts, "archive", "pc", "content"]);
        return root;
    }

    public void Dispose() => Directory.Delete(Path, true);
}
