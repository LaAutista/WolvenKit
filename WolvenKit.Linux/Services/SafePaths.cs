namespace WolvenKit.Linux.Services;

/// <summary>Path checks that keep writes inside the folder the user chose and out of game installs.</summary>
public static class SafePaths
{
    /// <summary>Absolute path with every symlink resolved, for the part of the path that exists.</summary>
    public static string RealPath(string path) => RealPath(path, 0);

    private static string RealPath(string path, int depth)
    {
        if (depth > 40) // same limit the kernel uses for nested links
        {
            throw new IOException($"Too many levels of symbolic links: {path}");
        }
        var current = Path.GetPathRoot(Path.GetFullPath(path))!;
        foreach (var part in Path.GetFullPath(path).Split(Path.DirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries))
        {
            current = Path.Combine(current, part);
            // ResolveLinkTarget throws IOException on link cycles; callers treat that as "unsafe"
            var info = new DirectoryInfo(current);
            if (info.LinkTarget is not null && info.ResolveLinkTarget(true) is { } target) // LinkTarget is null for paths that do not exist yet
            {
                current = RealPath(target.FullName, depth + 1);
            }
        }
        return current;
    }

    /// <summary>Finds <paramref name="parts"/> below <paramref name="root"/> ignoring case, returning the spelling on disk.</summary>
    public static string? ResolveIgnoreCase(string root, params string[] parts)
    {
        var current = root;
        foreach (var part in parts)
        {
            var exact = Path.Combine(current, part);
            if (Path.Exists(exact))
            {
                current = exact;
                continue;
            }
            try
            {
                var match = Directory.Exists(current)
                    ? Directory.EnumerateFileSystemEntries(current).FirstOrDefault(e => Path.GetFileName(e).Equals(part, StringComparison.OrdinalIgnoreCase))
                    : null;
                if (match is null)
                {
                    return null;
                }
                current = match;
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                return null;
            }
        }
        return current;
    }

    /// <summary>
    /// Destination for an archive entry below <paramref name="realOutDir"/> (already passed through <see cref="RealPath"/>).
    /// </summary>
    /// <exception cref="InvalidOperationException">The entry would be written outside the folder, through a symlink, or into a game install.</exception>
    public static string ResolveDestination(string realOutDir, string entryPath)
    {
        var dest = Path.GetFullPath(Path.Combine(realOutDir, entryPath.Replace('\\', Path.DirectorySeparatorChar)));
        if (!dest.StartsWith(Path.TrimEndingDirectorySeparator(realOutDir) + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            throw new InvalidOperationException($"Entry \"{entryPath}\" points outside the output folder.");
        }

        // nothing between the output folder and the file may be a symlink, or the write would land somewhere else
        for (var p = dest; p.Length > realOutDir.Length; p = Path.GetDirectoryName(p)!)
        {
            if (new FileInfo(p).LinkTarget is not null)
            {
                throw new InvalidOperationException($"\"{p}\" is a symbolic link; refusing to extract through it.");
            }
        }

        for (var dir = Path.GetDirectoryName(dest); dir is not null; dir = Path.GetDirectoryName(dir))
        {
            if (GameLocator.IsGameRoot(dir))
            {
                throw new InvalidOperationException($"Refusing to extract into the game folder {dir}. Pick another folder.");
            }
        }
        return dest;
    }
}
