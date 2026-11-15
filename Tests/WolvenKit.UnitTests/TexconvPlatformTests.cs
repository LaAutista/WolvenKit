#if !WINDOWS
using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using WolvenKit.Common;
using WolvenKit.Common.DDS;

namespace WolvenKit.UnitTests;

[TestClass]
public class TexconvPlatformTests
{
    [TestMethod]
    public void UnsupportedBackend_ReportsPlatformError()
    {
        Assert.ThrowsException<PlatformNotSupportedException>(() => Texconv.GetMetadataFromTGAFile("unused.tga"));
        Assert.ThrowsException<PlatformNotSupportedException>(() => Texconv.GetMetadataFromDDSFile("unused.dds"));
        Assert.ThrowsException<PlatformNotSupportedException>(() => Texconv.GetMetadataFromDDSMemory(Array.Empty<byte>()));
        Assert.ThrowsException<PlatformNotSupportedException>(() => Texconv.ComputeRowPitch(1, 1, DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM));
        Assert.ThrowsException<PlatformNotSupportedException>(() => Texconv.ComputeSlicePitch(1, 1, DXGI_FORMAT.DXGI_FORMAT_R8G8B8A8_UNORM));
        Assert.ThrowsException<PlatformNotSupportedException>(() => Texconv.ConvertFromDdsAndSave(Stream.Null, "unused.png", TexconvNative.ESaveFileTypes.PNG, false));
        Assert.ThrowsException<PlatformNotSupportedException>(() => Texconv.ConvertFromDds(Stream.Null, EUncookExtension.png));
        Assert.ThrowsException<PlatformNotSupportedException>(() => Texconv.ConvertToDds(Stream.Null, EUncookExtension.png));
    }
}
#endif
