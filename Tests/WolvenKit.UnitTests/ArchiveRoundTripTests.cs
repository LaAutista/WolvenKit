using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using WolvenKit.Common;
using WolvenKit.Common.FNV1A;
using WolvenKit.Common.Services;
using WolvenKit.Core.Compression;
using WolvenKit.Core.Interfaces;
using WolvenKit.Core.Services;
using WolvenKit.Modkit.RED4;
using WolvenKit.RED4.Archive;
using WolvenKit.RED4.Archive.IO;
using WolvenKit.RED4.CR2W;
using WolvenKit.RED4.Types;

namespace WolvenKit.UnitTests;

[TestClass]
[DoNotParallelize]
public class ArchiveRoundTripTests
{
    [DataTestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task PackAndExtract_GeneratedFiles_WithKraken(bool extractAsync)
    {
        var root = Directory.CreateTempSubdirectory("wolvenkit-roundtrip-");
        var settings = CompressionSettings.Get();
        var previousUseOodle = settings.UseOodle;
        var previousLevel = settings.CompressionLevel;
        try
        {
            settings.UseOodle = false;
            settings.CompressionLevel = Oodle.CompressionLevel.Normal;

            var input = root.CreateSubdirectory("input");
            var output = root.CreateSubdirectory("packed");
            var extracted = root.CreateSubdirectory("extracted");
            var fixtures = new Dictionary<string, byte[]>
            {
                [Path.Combine("nested", "compressible.json")] = Encoding.UTF8.GetBytes("{\"text\":\"" + new string('a', 65536) + "\"}"),
                [Path.Combine("nested", "tiny.json")] = Encoding.UTF8.GetBytes("{\"tiny\":1}"),
                [Path.Combine("nested", "raw.bin")] = Enumerable.Range(0, 1024).Select(i => (byte)i).ToArray()
            };
            foreach (var (path, bytes) in fixtures)
            {
                var fullPath = Path.Combine(input.FullName, path);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                File.WriteAllBytes(fullPath, bytes);
            }

            var hashService = Mock.Of<IHashService>();
            var logger = Mock.Of<ILoggerService>();
            var hooks = Mock.Of<IHookService>();
            var modTools = new ModTools(logger, Mock.Of<IProgressService<double>>(), hashService,
                new Red4ParserService(hashService, logger, hooks), Mock.Of<IArchiveManager>(), hooks);

            Assert.IsTrue(modTools.Pack(input, output, "generated"));
            var archivePath = Path.Combine(output.FullName, "generated.archive");
            Assert.IsFalse(File.Exists(Path.ChangeExtension(archivePath, "tmp")));
            var result = new ArchiveReader().ReadArchive(archivePath, hashService, out var archive);
            using (archive)
            {
                Assert.AreEqual(WolvenKit.RED4.Archive.IO.EFileReadErrorCodes.NoError, result);
                Assert.AreEqual(fixtures.Count, archive.Files.Count);
                foreach (var (path, bytes) in fixtures)
                {
                    var hash = FNV1A64HashAlgorithm.HashString(ResourcePath.SanitizePath(path));
                    var entry = (FileEntry)archive.Files[hash];
                    Assert.AreEqual(Path.GetFileName(path), entry.ShortName);
                    Assert.AreEqual((uint)bytes.Length, entry.Size);
                    if (path.EndsWith("compressible.json", StringComparison.Ordinal))
                    {
                        Assert.IsTrue(entry.ZSize < entry.Size, "The fixture must actually be compressed.");
                    }
                    else
                    {
                        Assert.AreEqual(entry.Size, entry.ZSize, "Small files and .bin files are stored uncompressed.");
                    }

                    var count = extractAsync
                        ? await ModTools.ExtractSingleAsync(archive, hash, extracted)
                        : ModTools.ExtractSingle(archive, hash, extracted);
                    Assert.AreEqual(1, count);
                    CollectionAssert.AreEqual(bytes, File.ReadAllBytes(Path.Combine(extracted.FullName, path)));
                }
                Assert.AreEqual(fixtures.Count, extracted.GetFiles("*", SearchOption.AllDirectories).Length);
            }
        }
        finally
        {
            settings.UseOodle = previousUseOodle;
            settings.CompressionLevel = previousLevel;
            root.Delete(true);
        }
    }
}
