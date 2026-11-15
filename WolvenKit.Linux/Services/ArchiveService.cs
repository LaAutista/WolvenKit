using WolvenKit.Common.Services;
using WolvenKit.RED4.Archive;
using WolvenKit.RED4.CR2W;

namespace WolvenKit.Linux.Services;

/// <summary>One archive entry, with the path in Linux form.</summary>
public sealed record ArchiveEntry(string Path, ulong Hash, uint Size, Archive Archive)
{
    public string SizeText => Size < 1024 ? $"{Size} B" : Size < 1024 * 1024 ? $"{Size / 1024.0:F1} KB" : $"{Size / 1048576.0:F1} MB";
}

/// <summary>Thin wrapper over the existing WolvenKit archive reader and extractor. Archives are opened read-only.</summary>
public sealed class ArchiveService
{
    // Loading the hash database takes seconds, so it is created on first use (off the UI thread).
    private readonly Lazy<(HashService Hashes, Red4ParserService Parser)> _services = new(() =>
    {
        var hashes = new HashService();
        return (hashes, new Red4ParserService(hashes, new SerilogWrapper(), new HookService()));
    });

    public IReadOnlyList<ArchiveEntry> ReadEntries(string archivePath)
    {
        var (hashes, parser) = _services.Value;
        var archive = parser.ReadRed4Archive(archivePath, hashes)
            ?? throw new InvalidDataException($"Not a readable REDengine archive: {archivePath}");

        return archive.Files.Values
            .OfType<FileEntry>()
            // entry names use Windows separators; show and filter them as Linux paths
            .Select(f => new ArchiveEntry(f.FileName.Replace('\\', '/'), f.NameHash64, f.Size, archive))
            .OrderBy(e => e.Path, StringComparer.Ordinal)
            .ToList();
    }

    /// <summary>
    /// Extracts entries below <paramref name="outDir"/>. Every destination is validated first (see
    /// <see cref="SafePaths.ResolveDestination"/>), so nothing is written if any entry is unsafe.
    /// </summary>
    /// <returns>Number of files written.</returns>
    /// <exception cref="InvalidOperationException">An entry would escape the folder or land in a game install.</exception>
    public static int Extract(IReadOnlyCollection<ArchiveEntry> entries, string outDir)
    {
        var realOut = SafePaths.RealPath(outDir);
        var plan = entries.Select(e => (Entry: e, Dest: SafePaths.ResolveDestination(realOut, e.Path))).ToList();
        foreach (var (entry, dest) in plan)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            using var fs = new FileStream(dest, FileMode.Create, FileAccess.Write);
            ((FileEntry)entry.Archive.Files[entry.Hash]).Extract(fs);
        }
        return plan.Count;
    }
}
